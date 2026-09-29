<#
.SYNOPSIS
    Fails if the repo or a release artifact contains Nintendo game files.

.DESCRIPTION
    WW-Online must never ship or commit Nintendo files (main.dol, RELS.arc, models, disc images...).
    This guard checks file NAMES, EXTENSIONS and a few file-format MAGIC numbers (RARC archives,
    Yaz0-compressed data, J3D models, GameCube disc headers), so a renamed file is caught too.

    The one exception is our own compiled puppet REL (GameMod/src/puppet_link -> d_a_puppet.rel),
    which ships inside PatchData/. It is allowed only at the exact paths in $AllowedPaths below and
    only if its header carries our module id (0x58).

    With no -Path it checks the git working tree: every tracked file plus untracked files that
    are not gitignored (so a file is flagged before it can be committed).
    With -Path it checks those folders and files instead; .zip and .nupkg files (Velopack
    packages, the server zip) are opened and their entries checked too.

    Exit code 0 = clean, 1 = Nintendo files found (or the scan failed).

.EXAMPLE
    ./scripts/check-no-nintendo-files.ps1
.EXAMPLE
    ./scripts/check-no-nintendo-files.ps1 -Path publish/client, Releases
.EXAMPLE
    pwsh -File scripts/check-no-nintendo-files.ps1 publish/client Releases
#>
[CmdletBinding()]
param(
    # Folders / files to scan (several allowed: -Path a b, or -Path a,b from PowerShell).
    [Parameter(Position = 0, ValueFromRemainingArguments = $true)]
    [string[]] $Path = @()
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

# Extensions of Nintendo formats (game code, archives, models, textures, animations, disc images).
$ForbiddenExtensions = @(
    '.dol', '.rel', '.arc', '.szs', '.bdl', '.bmd', '.bti', '.bck', '.bmg', '.bfn', '.blo',
    '.iso', '.gcm', '.rvz', '.nkit', '.wbfs', '.gcz', '.wia', '.ciso', '.tgc'
)

# Exact file names from the game disc (system area + the files we patch).
$ForbiddenNames = @(
    'main.dol', 'rels.arc', 'bi2.bin', 'boot.bin', 'apploader.img', 'fst.bin', 'opening.bnr',
    'wwo-switch-table.json'   # built at patch time from the player's own stage data
)

# Our own compiled REL, and only at these exact relative paths ('/'-separated, case-sensitive):
#   PatchData/d_a_puppet.rel           client build/publish output
#   lib/app/PatchData/d_a_puppet.rel   inside a Velopack .nupkg
#   current/PatchData/d_a_puppet.rel   inside a Velopack Portable.zip
$AllowedPaths = @(
    'PatchData/d_a_puppet.rel',
    'lib/app/PatchData/d_a_puppet.rel',
    'current/PatchData/d_a_puppet.rel'
)
$PuppetRelModuleId = 0x58   # BuildRelStep.ModuleId

# Leading bytes of Nintendo container/model formats: name -> (offset, bytes).
$Magics = @(
    @{ Name = 'RARC archive';         Offset = 0;    Bytes = [byte[]](0x52, 0x41, 0x52, 0x43) },   # 'RARC'
    @{ Name = 'Yaz0-compressed data'; Offset = 0;    Bytes = [byte[]](0x59, 0x61, 0x7A, 0x30) },   # 'Yaz0'
    @{ Name = 'J3D model/animation';  Offset = 0;    Bytes = [byte[]](0x4A, 0x33, 0x44) },         # 'J3D' (bmd/bdl/bck...)
    @{ Name = 'BMG message text';     Offset = 0;    Bytes = [byte[]](0x4D, 0x45, 0x53, 0x47, 0x62, 0x6D, 0x67) },  # 'MESGbmg'
    @{ Name = 'GameCube disc image';  Offset = 0x1C; Bytes = [byte[]](0xC2, 0x33, 0x9F, 0x3D) }    # GC disc magic
)
$HeaderLength = 0x20

$violations = New-Object System.Collections.Generic.List[string]
$checked = 0

function Test-Header([byte[]] $header, [string] $display) {
    foreach ($m in $Magics) {
        $end = $m.Offset + $m.Bytes.Length
        if ($header.Length -lt $end) { continue }
        $match = $true
        for ($i = 0; $i -lt $m.Bytes.Length; $i++) {
            if ($header[$m.Offset + $i] -ne $m.Bytes[$i]) { $match = $false; break }
        }
        if ($match) { $violations.Add("${display}: looks like a $($m.Name) (file magic)") }
    }
}

function Read-Header([System.IO.Stream] $stream) {
    $buffer = New-Object byte[] $HeaderLength
    $read = 0
    while ($read -lt $HeaderLength) {
        $n = $stream.Read($buffer, $read, $HeaderLength - $read)
        if ($n -le 0) { break }
        $read += $n
    }
    if ($read -lt $HeaderLength) { return $buffer[0..([Math]::Max(0, $read - 1))] }
    return $buffer
}

# $relative: '/'-separated path used for the allow-list; $display: what to print.
function Test-Entry([string] $relative, [string] $display, [byte[]] $header) {
    $script:checked++
    $name = [System.IO.Path]::GetFileName($relative)
    $ext = [System.IO.Path]::GetExtension($name).ToLowerInvariant()

    if ($AllowedPaths -ccontains $relative) {
        # Our REL: must be a REL with our module id (big-endian u32 at offset 0).
        $id = -1
        if ($header.Length -ge 4) {
            $id = ([int]$header[0] -shl 24) -bor ([int]$header[1] -shl 16) -bor ([int]$header[2] -shl 8) -bor [int]$header[3]
        }
        if ($id -ne $PuppetRelModuleId) {
            $message = "${display}: allow-listed path but not our puppet REL (module id 0x{0:X}, expected 0x{1:X})" -f $id, $PuppetRelModuleId
            $violations.Add($message)
        }
        return
    }

    if ($ForbiddenNames -contains $name.ToLowerInvariant()) {
        $violations.Add("${display}: Nintendo file name '$name'")
    }
    elseif ($ForbiddenExtensions -contains $ext) {
        $violations.Add("${display}: Nintendo file type '$ext'")
    }
    if ($null -ne $header) { Test-Header $header $display }
}

function Test-Archive([string] $archivePath, [string] $display) {
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [System.IO.Compression.ZipFile]::OpenRead($archivePath)
    try {
        foreach ($entry in $zip.Entries) {
            if ($entry.FullName.EndsWith('/')) { continue }   # directory entry
            $relative = $entry.FullName.Replace('\', '/')
            $stream = $entry.Open()
            try { $header = Read-Header $stream } finally { $stream.Dispose() }
            Test-Entry $relative "$display!$relative" $header
        }
    }
    finally { $zip.Dispose() }
}

function Test-File([string] $fullPath, [string] $relative, [string] $display) {
    if (-not $display) { $display = $relative }
    $stream = [System.IO.File]::OpenRead($fullPath)
    try { $header = Read-Header $stream } finally { $stream.Dispose() }
    Test-Entry $relative $display $header

    $ext = [System.IO.Path]::GetExtension($fullPath).ToLowerInvariant()
    if ($ext -eq '.zip' -or $ext -eq '.nupkg') { Test-Archive $fullPath $display }
}

if ($Path.Count -eq 0) {
    # Git working tree mode: the repo this script lives in. (Not `git rev-parse --show-toplevel`:
    # an MSYS/Cygwin git on PATH prints a /home/... path PowerShell can't use.)
    $root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
    Push-Location $root
    try {
        $files = & git -c core.quotepath=off ls-files --cached --others --exclude-standard
        if ($LASTEXITCODE -ne 0) { throw "git ls-files failed in $root (pass -Path to scan folders instead)" }
        foreach ($rel in $files) {
            if ([string]::IsNullOrWhiteSpace($rel)) { continue }
            $full = Join-Path $root $rel
            if (-not (Test-Path -LiteralPath $full -PathType Leaf)) {
                # Submodule (gitlink) or a tracked file deleted in the working tree: check the name only.
                if (-not (Test-Path -LiteralPath $full -PathType Container)) { Test-Entry $rel $rel $null }
                continue
            }
            Test-File $full $rel
        }
    }
    finally { Pop-Location }
}
else {
    foreach ($p in $Path) {
        $item = Get-Item -LiteralPath $p
        if ($item.PSIsContainer) {
            $base = $item.FullName.TrimEnd('\', '/')
            foreach ($f in Get-ChildItem -LiteralPath $base -Recurse -File -Force) {
                $rel = $f.FullName.Substring($base.Length + 1).Replace('\', '/')
                Test-File $f.FullName $rel "$($item.Name)/$rel"
            }
        }
        else {
            Test-File $item.FullName $item.Name
        }
    }
}

if ($violations.Count -gt 0) {
    Write-Host "Nintendo files found ($($violations.Count)):" -ForegroundColor Red
    foreach ($v in $violations) { Write-Host "  $v" -ForegroundColor Red }
    Write-Host 'WW-Online must never commit or ship game files. Remove them (and check .gitignore).' -ForegroundColor Red
    exit 1
}

Write-Host "No Nintendo files: $checked file(s) checked." -ForegroundColor Green
exit 0
