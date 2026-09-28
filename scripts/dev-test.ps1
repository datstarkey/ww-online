<#
.SYNOPSIS
  One-command local multiplayer test: 1 server + N Dolphins + N clients, with every
  process logging to logs/latest/ so the whole session can be read back afterwards.

.DESCRIPTION
  1. Stops anything from a previous run and archives its logs to logs/sessions/<time>/.
  2. Builds server + client (unless -SkipBuild).
  3. Optionally re-patches the game (-Patch = full devkitPPC compile + DOL/REL patch).
  4. Refuses to launch if the patched game doesn't match the C sources (build stamp),
     unless -AllowStale.
  5. Configures each Dolphin user dir to log OSReport/MMU output to a file and to never
     block on panic popups, then launches Dolphins and clients. Player1 HOSTS (its client
     starts the server, like the Host button) and the others join; -DedicatedServer runs
     the server as a separate process instead.

  Logs for the current run (all in logs/latest/):
    session.txt            what ran: git rev, PIDs, build-check result, log paths
    build.log              dotnet build output
    patch.log              patch pipeline output (-Patch only)
    build-check.log        game build stamp check
    server.log             relay server (joins/leaves, [room] rules, [world]/[wallet] sync,
                           [stats] relay rates every 10s) - written by Player1's hosted server
    client-<Player>.log    each client ([diag] line every 2s, [puppet] visibility changes)
    dolphin-<N>.log        each Dolphin's log (OSReport incl. [PUPPET] lines, MMU errors) -
                           copied in by -Collect / -Stop / the next run; live copy is in
                           <DolphinUserRoot>\user<N>\Logs\dolphin.log

.EXAMPLE
  .\scripts\dev-test.ps1                 # build C#, check game build, launch everything
  .\scripts\dev-test.ps1 -Patch          # also recompile the C code and re-patch the game first
  .\scripts\dev-test.ps1 -Patch -Patches skip_intro,instant_text   # ...with these optional patches
                                 # (default: each patch's @default; 'none' = no optional patches)
  .\scripts\dev-test.ps1 -Collect        # copy the live Dolphin logs into logs/latest
  .\scripts\dev-test.ps1 -Stop           # stop everything (and collect logs)
  .\scripts\dev-test.ps1 -ResetSaves     # re-copy your main Dolphin save into both test profiles
  .\scripts\dev-test.ps1 -DedicatedServer  # separate server process instead of Player1 hosting
#>
[CmdletBinding()]
param(
    [switch]$Patch,
    # Optional game patches (GameMod/src/patches/optional/<id>.asm) for -Patch and the build
    # check: "a,b,c", "none" or "default". Unset = -Patch applies the defaults and the build check
    # only verifies the sources of whatever selection the game was built with.
    [string[]]$Patches,
    [switch]$SkipBuild,
    [switch]$AllowStale,
    [switch]$Stop,
    [switch]$Collect,
    [switch]$ResetSaves,
    # Run the relay as its own process instead of Player1 hosting it from the client.
    [switch]$DedicatedServer,
    [ValidateRange(1, 4)][int]$Clients = 2,
    [int]$Port = 6969,
    [string]$DolphinExe = "$env:USERPROFILE\Documents\Dolphin-x64\Dolphin.exe",
    [string]$DolphinUserRoot = "$env:USERPROFILE\Documents\Dolphin-x64",
    # Your normal Dolphin profile - source of truth for controls, graphics, core settings and
    # the Wind Waker save the test instances start from.
    [string]$MainDolphinUser = "$env:APPDATA\Dolphin Emulator"
)

$ErrorActionPreference = 'Stop'
$Repo = Split-Path -Parent $PSScriptRoot   # scripts\ -> repo root
$LogsRoot = Join-Path $Repo 'logs'
$Latest = Join-Path $LogsRoot 'latest'
$ServerExe = Join-Path $Repo 'WWOnline.Server\bin\Debug\net9.0\WWOnline.Server.exe'
$ClientExe = Join-Path $Repo 'WWOnline.Client\bin\Debug\net9.0\WWOnline.Client.exe'
$ModConfig = Join-Path $Repo 'GameMod\config.json'   # machine-specific; copy GameMod\config.example.json
if (-not (Test-Path $ModConfig)) { throw "Missing $ModConfig - copy GameMod\config.example.json to it and fill in your paths." }
$GamePath = (Get-Content $ModConfig -Raw | ConvertFrom-Json).game_path
$MainDol = Join-Path $GamePath 'sys\main.dol'

function Write-Step($text) { Write-Host "`n=== $text" -ForegroundColor Cyan }

# PowerShell 5.1 turns any stderr line from a native exe into a terminating error under
# ErrorActionPreference=Stop (git's CRLF warnings, dotnet build warnings...). Run natives
# through cmd so their output is plain text and only the exit code matters.
function Invoke-Cmd([string]$commandLine) {
    & $env:ComSpec /d /c "$commandLine 2>&1"   # ComSpec: a devkitPro msys "cmd" can shadow cmd on PATH
}
function DolphinUserDir([int]$n) { Join-Path $DolphinUserRoot "user$n" }

# --- process management -------------------------------------------------------------------

function Stop-TestProcesses {
    # Only processes this script owns: our exes, `dotnet run` of our projects, and Dolphins
    # started with one of our -u user dirs. Never a blanket `taskkill dotnet.exe`.
    $userDirs = 1..4 | ForEach-Object { DolphinUserDir $_ }
    $procs = Get-CimInstance Win32_Process | Where-Object {
        $cmd = [string]$_.CommandLine
        $_.Name -in 'WWOnline.Client.exe', 'WWOnline.Server.exe' -or
        ($_.Name -eq 'dotnet.exe' -and $cmd -match 'WWOnline\.(Client|Server)') -or
        ($_.Name -eq 'Dolphin.exe' -and @($userDirs | Where-Object { $cmd -like "*$_*" }).Count -gt 0)
    }
    foreach ($p in $procs) {
        Write-Host "  stopping $($p.Name) ($($p.ProcessId))"
        Stop-Process -Id $p.ProcessId -Force -ErrorAction SilentlyContinue
    }
    if ($procs) { Start-Sleep -Milliseconds 800 }
}

function Copy-DolphinLogs([string]$dest) {
    if (-not (Test-Path $dest)) { return }
    for ($n = 1; $n -le 4; $n++) {
        $src = Join-Path (DolphinUserDir $n) 'Logs\dolphin.log'
        if (Test-Path $src) { Copy-Item $src (Join-Path $dest "dolphin-$n.log") -Force }
    }
}

function Wait-ForPort([int]$port, [int]$timeoutSec) {
    $deadline = (Get-Date).AddSeconds($timeoutSec)
    while ((Get-Date) -lt $deadline) {
        $tcp = New-Object System.Net.Sockets.TcpClient
        try { $tcp.Connect('127.0.0.1', $port); return $true } catch { Start-Sleep -Milliseconds 250 } finally { $tcp.Dispose() }
    }
    return $false
}

# --- Dolphin config ------------------------------------------------------------------------

function Set-IniValues([string]$path, [string]$section, [hashtable]$values) {
    $lines = New-Object System.Collections.Generic.List[string]
    # @() because Get-Content returns $null for an empty file (Dolphin ships empty Logger.ini files)
    if (Test-Path $path) { foreach ($l in @(Get-Content $path)) { if ($null -ne $l) { $lines.Add($l) } } }

    $start = $lines.IndexOf("[$section]")
    if ($start -lt 0) { $lines.Add("[$section]"); $start = $lines.Count - 1 }
    $end = $start + 1
    while ($end -lt $lines.Count -and -not $lines[$end].StartsWith('[')) { $end++ }

    foreach ($key in $values.Keys) {
        $entry = "$key = $($values[$key])"
        $found = $false
        for ($i = $start + 1; $i -lt $end; $i++) {
            if ($lines[$i] -match "^\s*$([regex]::Escape($key))\s*=") { $lines[$i] = $entry; $found = $true; break }
        }
        if (-not $found) { $lines.Insert($end, $entry); $end++ }
    }
    Set-Content -Path $path -Value $lines -Encoding ASCII
}

function Initialize-DolphinUserDir([string]$dir) {
    $cfg = Join-Path $dir 'Config'
    New-Item -ItemType Directory -Force -Path $cfg, (Join-Path $dir 'Logs') | Out-Null

    # Mirror the main profile's settings every run (one source of truth: change controls or
    # graphics in your normal Dolphin, not in user1/user2). Test overrides are applied below.
    $mainCfg = Join-Path $MainDolphinUser 'Config'
    foreach ($ini in 'Dolphin.ini', 'GCPadNew.ini', 'GCKeyNew.ini', 'GFX.ini', 'Hotkeys.ini') {
        $src = Join-Path $mainCfg $ini
        if (Test-Path $src) { Copy-Item $src (Join-Path $cfg $ini) -Force }
    }

    # Saves: seed Card A from the main profile only when this test profile has no Wind Waker
    # save (or -ResetSaves), so each test player's progress persists between runs.
    $mainCard = Join-Path $MainDolphinUser 'GC\USA\Card A'
    $card = Join-Path $dir 'GC\USA\Card A'
    New-Item -ItemType Directory -Force -Path $card | Out-Null
    $hasSave = @(Get-ChildItem $card -Filter '*GZLE*.gci' -ErrorAction SilentlyContinue).Count -gt 0
    if (($ResetSaves -or -not $hasSave) -and (Test-Path $mainCard)) {
        Copy-Item (Join-Path $mainCard '*GZLE*.gci') $card -Force
        Write-Host "  seeded Wind Waker save into $card"
    }
    $sram = Join-Path $MainDolphinUser 'GC\SRAM.raw'
    if ((Test-Path $sram) -and -not (Test-Path (Join-Path $dir 'GC\SRAM.raw'))) {
        Copy-Item $sram (Join-Path $dir 'GC\SRAM.raw')
    }

    # Log to <dir>\Logs\dolphin.log. OSREPORT(_HLE) carries the REL's OSReport("[PUPPET] ...")
    # lines; MEMMAP / MASTER_LOG carry "Invalid read/write ... PC = 0x8028xxxx" MMU errors.
    $logger = Join-Path $cfg 'Logger.ini'
    Set-IniValues $logger 'Options' @{ Verbosity = 4; WriteToFile = 'True'; WriteToConsole = 'False'; WriteToWindow = 'True' }
    Set-IniValues $logger 'Logs' @{
        OSREPORT = 'True'; OSREPORT_HLE = 'True'; MEMMAP = 'True'; MASTER_LOG = 'True'
        POWERPC = 'True'; BOOT = 'True'; CORE = 'True'; OSHLE = 'True'
    }

    # Panic popups are modal and freeze the instance mid-test; with handlers off the same
    # messages go to the log instead. ConfirmStop off so -Stop / closing doesn't prompt.
    Set-IniValues (Join-Path $cfg 'Dolphin.ini') 'Interface' @{ UsePanicHandlers = 'False'; ConfirmStop = 'False' }
    Set-IniValues (Join-Path $cfg 'Dolphin.ini') 'Analytics' @{ PermissionAsked = 'True' }
    # Card A as a GCI folder, so the seeded .gci above is what the game sees.
    Set-IniValues (Join-Path $cfg 'Dolphin.ini') 'Core' @{ SlotA = '8' }

    # REQUIRED: the patched game (use_extra_memory.asm + bi2) expects 48MB MEM1. With Dolphin's
    # default 24MB it jumps to 0x0 right after DVD init and hangs on a black screen.
    Set-IniValues (Join-Path $cfg 'Dolphin.ini') 'Core' @{ RAMOverrideEnable = 'True'; MEM1Size = '0x03000000' }

    Remove-Item (Join-Path $dir 'Logs\dolphin.log') -ErrorAction SilentlyContinue
}

# --- modes ---------------------------------------------------------------------------------

if ($Collect -and -not $Stop) {
    Copy-DolphinLogs $Latest
    Write-Host "Dolphin logs copied into $Latest"
    exit 0
}

Write-Step 'Stopping previous test processes'
Stop-TestProcesses

if ($Stop) {
    Copy-DolphinLogs $Latest
    Write-Host "Stopped. Logs are in $Latest"
    exit 0
}

# Archive the previous run (with its Dolphin logs) before starting fresh.
if (Test-Path $Latest) {
    Copy-DolphinLogs $Latest
    $stamp = (Get-Item $Latest).CreationTime.ToString('yyyyMMdd-HHmmss')
    $archive = Join-Path $LogsRoot "sessions\$stamp"
    New-Item -ItemType Directory -Force -Path (Split-Path $archive) | Out-Null
    if (Test-Path $archive) { $archive += '-' + (Get-Random) }
    Move-Item $Latest $archive
    Write-Host "  previous run archived to $archive"
}
New-Item -ItemType Directory -Force -Path $Latest | Out-Null
(Get-Item $Latest).CreationTime = Get-Date

if (-not $SkipBuild) {
    Write-Step 'Building server + client'
    $buildLog = Join-Path $Latest 'build.log'
    foreach ($proj in 'WWOnline.Server\WWOnline.Server.csproj', 'WWOnline.Client\WWOnline.Client.csproj') {
        Invoke-Cmd "dotnet build `"$(Join-Path $Repo $proj)`" -nologo -v minimal" | Out-File $buildLog -Append -Encoding UTF8
        if ($LASTEXITCODE -ne 0) {
            Get-Content $buildLog -Tail 30
            throw "dotnet build failed for $proj - see $buildLog"
        }
    }
    Write-Host '  ok'
}

function Invoke-ClientHeadless([string]$flag, [string]$logName) {
    $log = Join-Path $Latest $logName
    $clientArgs = @($flag, '--log-file', "`"$log`"")
    # Strip spaces: Start-Process joins the arguments with spaces, so "a, b" would split in two.
    if ($Patches) { $clientArgs += @('--patches', (($Patches -join ',') -replace '\s', '')) }
    $p = Start-Process -FilePath $ClientExe -ArgumentList $clientArgs -Wait -PassThru -WindowStyle Hidden
    Get-Content $log -ErrorAction SilentlyContinue | Where-Object { $_ -match '\[(patch|build-check)\]' } | ForEach-Object { Write-Host "  $_" }
    return $p.ExitCode
}

if ($Patch) {
    Write-Step 'Patching game (devkitPPC compile + DOL/REL patch)'
    $code = Invoke-ClientHeadless '--patch' 'patch.log'
    if ($code -ne 0) { throw "Patch failed (exit $code) - see $(Join-Path $Latest 'patch.log')" }
}

Write-Step 'Checking game build matches sources'
$buildCheck = Invoke-ClientHeadless '--check-build' 'build-check.log'
if ($buildCheck -ne 0) {
    if ($AllowStale) {
        Write-Warning 'Game build does not match sources - continuing because of -AllowStale.'
    } else {
        Write-Host "`nThe patched game does not match the current C sources." -ForegroundColor Red
        Write-Host 'Re-run with -Patch to rebuild it, or -AllowStale to launch anyway.' -ForegroundColor Red
        exit 2
    }
}

$serverLog = Join-Path $Latest 'server.log'
$serverInfo = "hosted by Player1's client, port $Port, log $serverLog"
if ($DedicatedServer) {
    Write-Step "Launching dedicated server on port $Port"
    $server = Start-Process -FilePath $ServerExe -ArgumentList $Port, '--log-file', "`"$serverLog`"" -PassThru -WindowStyle Minimized
    if (-not (Wait-ForPort $Port 15)) { throw "Server did not start listening on $Port - see $serverLog" }
    Write-Host "  server pid $($server.Id)"
    $serverInfo = "dedicated, pid $($server.Id), port $Port, log $serverLog"
}

Write-Step "Launching $Clients Dolphin instance(s)"
$dolphins = @()
for ($n = 1; $n -le $Clients; $n++) {
    $dir = DolphinUserDir $n
    Initialize-DolphinUserDir $dir
    $d = Start-Process -FilePath $DolphinExe -ArgumentList '-u', "`"$dir`"", '-e', "`"$MainDol`"" -PassThru
    $dolphins += $d
    Write-Host "  Dolphin $n pid $($d.Id)  (user dir $dir)"
    Start-Sleep -Seconds 2
}

Write-Step "Launching $Clients client(s)"
$clientsInfo = @()
for ($n = 1; $n -le $Clients; $n++) {
    $name = "Player$n"
    $log = Join-Path $Latest "client-$name.log"
    $c = Start-Process -FilePath $ClientExe -PassThru -ArgumentList @(
        '--player', $name, '--host', 'localhost', '--port', $Port,
        $(if ($n -eq 1 -and -not $DedicatedServer) { '--host-server' } else { '--auto-connect' }),
        '--auto-attach', '--dolphin-pid', $dolphins[$n - 1].Id,
        '--log-file', "`"$log`"")
    $clientsInfo += "  $name client pid $($c.Id) -> Dolphin pid $($dolphins[$n - 1].Id), log $log"
    Start-Sleep -Milliseconds 500
}
$clientsInfo | ForEach-Object { Write-Host $_ }

$gitRev = Invoke-Cmd "git -C `"$Repo`" rev-parse --short HEAD" | Select-Object -First 1
$gitDirty = if (Invoke-Cmd "git -C `"$Repo`" status --porcelain -uno" | Where-Object { $_ -match '^\s?[MADRCU]' }) { ' (dirty)' } else { '' }
$session = @(
    "started      : $(Get-Date -Format o)"
    "git          : $gitRev$gitDirty"
    "game path    : $GamePath"
    "build check  : exit $buildCheck (0=fresh, 2=stale, 3=no stamp)$(if ($AllowStale) { ' [-AllowStale]' })"
    "server       : $serverInfo"
) + $clientsInfo + (1..$Clients | ForEach-Object {
    "  Dolphin $_ pid $($dolphins[$_ - 1].Id), live log $(Join-Path (DolphinUserDir $_) 'Logs\dolphin.log')"
})
Set-Content -Path (Join-Path $Latest 'session.txt') -Value $session -Encoding UTF8

Write-Step 'Running'
Write-Host "Logs: $Latest"
Write-Host 'Get both Links into the same stage + room, then check the [diag] lines in the client logs.'
Write-Host 'When done: .\scripts\dev-test.ps1 -Stop   (collects Dolphin logs too)'
