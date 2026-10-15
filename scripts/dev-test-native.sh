#!/bin/zsh
# Two native clients on this Mac in one room: Player1 hosts the server, Player2 joins it, and each starts
# the native game (libtww) embedded after joining. macOS counterpart of dev-test.ps1 for the native backend.
#
#   scripts/dev-test-native.sh --lib <libtww.dylib> --disc <GZLE01.iso> [--stage sea:44:8 | --preset sailing|pvp] [--players 2]
#       [--input <n>:<file>]
#   scripts/dev-test-native.sh --hidden --stage sea:44:8 [--shots 900,1500] [--rupees 600:50] [--warp 1200:Player1] [--timeout 300]
#   scripts/dev-test-native.sh --stop [--log-dir <dir>]
#
# --lib / --disc default to $WWO_LIBTWW / $WWO_DISC. The libtww is the one built from seaborne/ (the vendored engine):
# by the setup's Build step (Settings > Game files shows its path, under ~/Library/Application Support/WWOnline/game/build/),
# or by hand with the same steps:
#   seaborne/native/tools/fetch_recompcore.sh <dir>/recompcore
#   cmake -S seaborne/native -B <dir>/build -G Ninja -DCMAKE_BUILD_TYPE=RelWithDebInfo -DCOS_RECOMPCORE_DIR=<dir>/recompcore
#   ninja -C <dir>/build tww                                  # <dir>/build/libtww.dylib
# --stage boots both games straight into that stage
# (COS_BOOT_STAGE, with a fresh save) instead of the title screen; the players after the first then start
# --offset <right>:<ahead> units beside the first (COS_MP_SPAWN_OFFSET, default 50:150), so each sees the other;
# --peer-stage boots the players after the first into another stage instead (no offset).
# --preset sailing boots every game on the King of Red Lions at sea (COS_BOOT_PRESET), to see each other's boats.
# --preset pvp boots every game with COS_BOOT_PRESET=pvp, the players after the first --offset
# beside the first, and Player1 hosts the room with Co-op plus PVP (the client's --pvp: everyone keeps their kit).
# --input <n>:<file> plays Player<n>'s game from that input script (COS_INPUT).
# Each player gets its own settings and saves under logs/players/Player<N> (kept between runs; --hidden uses a
# fresh logs/native/Player<N>), and its log at
# logs/native/client-Player<N>.log (the game's own output: Player<N>.out; the server's: server.log).
#
# --hidden (checks, CI): no window, no Dock icon, no focus (the clients' --hidden). Each client saves the game's
# frame and view at the --shots game frames into logs/native/Player<N>/shots, Player1 adds --rupees at a frame
# (the shared wallet must carry it to the others), Player2 presses "Warp to" on --warp's player at its frame, then the
# script stops them and prints the evidence. --log-dir puts a run's logs, saves and pids somewhere other than
# logs/native, so two hidden runs (on different --port) don't share them.
# Exit 0 when every client finished its shots.
set -eu
caller=$PWD
cd "${0:A:h}/.."

lib=${WWO_LIBTWW:-}
disc=${WWO_DISC:-}
stage=""
peer_stage=""
preset=""
players=2
port=6969
offset=50:150
hidden=0
shots=900,1500
rupees=600:50
warp=""
timeout=300
logs=logs/native
input_player=""
input_file=""

stop() {
    [[ -f $pids ]] || return 0
    # SIGTERM shuts a client down as closing its window does (the host's server stops with it).
    while read -r pid; do kill "$pid" 2>/dev/null || true; done < $pids
    for i in {1..50}; do
        alive=0
        while read -r pid; do kill -0 "$pid" 2>/dev/null && alive=1; done < $pids
        (( alive )) || break
        sleep 0.2
    done
    while read -r pid; do kill -9 "$pid" 2>/dev/null || true; done < $pids
    rm -f $pids
}

while (( $# )); do
    case $1 in
        --lib) lib=$2; shift 2 ;;
        --disc) disc=$2; shift 2 ;;
        --stage) stage=$2; shift 2 ;;
        --peer-stage) peer_stage=$2; shift 2 ;;
        --preset) preset=$2; shift 2 ;;
        --players) players=$2; shift 2 ;;
        --port) port=$2; shift 2 ;;
        --offset) offset=$2; shift 2 ;;
        --hidden) hidden=1; shift ;;
        --shots) shots=$2; shift 2 ;;
        --rupees) rupees=$2; shift 2 ;;
        --warp) warp=$2; shift 2 ;;
        --input) input_player=${2%%:*}; input_file=${2#*:}; [[ $input_file == /* ]] || input_file=$caller/$input_file; shift 2 ;;
        --log-dir) logs=$2; shift 2 ;;
        --timeout) timeout=$2; shift 2 ;;
        --stop) stop_now=1; shift ;;
        *) echo "unknown argument: $1" >&2; exit 2 ;;
    esac
done
logs=${logs:A}
pids=$logs/pids
(( ${stop_now:-0} )) && { stop; exit 0; }
[[ -f $lib ]] || { echo "libtww not found: '$lib' (--lib or WWO_LIBTWW)" >&2; exit 2; }
[[ -f $disc ]] || { echo "disc image not found: '$disc' (--disc or WWO_DISC)" >&2; exit 2; }
[[ -z $input_file || -f $input_file ]] || { echo "input script not found: '$input_file' (--input)" >&2; exit 2; }
[[ -f $pids ]] && { echo "clients from an earlier run may still be running: scripts/dev-test-native.sh --stop --log-dir $logs" >&2; exit 2; }

rm -rf $logs
mkdir -p $logs
dotnet build WWOnline.Server/WWOnline.Server.csproj -v q -nologo > /dev/null
dotnet build WWOnline.Client/WWOnline.Client.csproj -v q -nologo > /dev/null
client=WWOnline.Client/bin/Debug/net9.0/WWOnline.Client

: > $pids
for n in $(seq 1 $players); do
    dir=$logs/Player$n
    (( hidden )) || dir=$PWD/logs/players/Player$n
    mkdir -p "$dir"
    args=(--player "Player$n" --host localhost --port $port --log-file "$logs/client-Player$n.log")
    (( n == 1 )) && args+=(--host-server) || args+=(--auto-connect)
    (( hidden )) && args+=(--hidden --test-dir "$dir/shots" --test-shots "$shots")
    (( hidden && n == 1 )) && [[ -n $rupees ]] && args+=(--test-rupees "$rupees")
    (( hidden && n == 2 )) && [[ -n $warp ]] && args+=(--test-warp "$warp")
    (( n == 1 )) && [[ $preset == pvp ]] && args+=(--pvp)
    input=()
    [[ $input_player == $n ]] && input=(COS_INPUT=$input_file)
    boot=$stage
    spawn=()
    if (( n > 1 )) && [[ -n $peer_stage ]]; then
        boot=$peer_stage
    elif (( n > 1 )) && [[ -n $stage || $preset == pvp ]]; then
        spawn=(COS_MP_SPAWN_OFFSET=$offset)
    fi
    env WWO_SETTINGS_DIR=$dir WWO_LIBTWW=$lib WWO_DISC=$disc ${boot:+COS_BOOT_STAGE=$boot} ${preset:+COS_BOOT_PRESET=$preset} $spawn $input \
        "$client" "${args[@]}" > "$logs/Player$n.out" 2>&1 &
    echo $! >> $pids
    # the host's server must be listening before the others connect (they retry, but this keeps logs tidy)
    (( n == 1 )) && sleep 4
done

if (( ! hidden )); then
    echo "started $players clients; logs in $logs/; stop with scripts/dev-test-native.sh --stop --log-dir $logs"
    exit 0
fi

echo "started $players hidden clients; waiting for their shots (up to ${timeout}s)..."
ok=0
for i in $(seq 1 $timeout); do
    done_count=0
    for n in $(seq 1 $players); do
        grep -q '\[test\] done' $logs/client-Player$n.log 2>/dev/null && done_count=$(( done_count + 1 ))
        grep -q '\[test\] FAIL' $logs/client-Player$n.log 2>/dev/null && break 2
    done
    (( done_count == players )) && { ok=1; break; }
    sleep 1
done
stop

for n in $(seq 1 $players); do
    echo "--- Player$n"
    grep -hE '\[test\]|\[warp\]|\[pvp\]|\[native\] (the game|tww_init)|\[diag\] native|wallet|Started server' $logs/client-Player$n.log | tail -n 16 | cut -c 13-
    grep -hE '^\[wwo\]' $logs/Player$n.out | head -n 8
done
(( ok )) && echo "PASS: every client saved its shots ($logs/Player<N>/shots)" || echo "FAIL: see $logs/"
(( ok ))
