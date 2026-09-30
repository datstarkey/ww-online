# Self-hosting a WW-Online server

A WW-Online room is a small relay server. Usually the host's app runs it: **Host a room** starts one on the host's PC. A **dedicated server** runs it on its own, on an always-on machine (a VPS, a home server, a Raspberry Pi), so the room is up even when the host isn't playing, and nobody's PC has to be reachable from the internet.

> **You probably don't need this.** If your friends can reach your PC (the same home network, a shared [Tailscale](https://tailscale.com/) network, or port `6969` forwarded on your router), just press **Host a room** in the app and they join your address. A dedicated server is only worth it when you want a room that stays up without you, or when nobody in the group can make their PC reachable.

The server is published as a Docker image for `linux/amd64` and `linux/arm64`:

```
ghcr.io/datstarkey/ww-online-server
```

It needs no game files, no GPU and almost no resources. The smallest VPS is plenty.

## Quick start

```
docker run -d --name ww-online --restart unless-stopped -p 6969:6969 ghcr.io/datstarkey/ww-online-server:latest
```

Then everyone opens WW-Online, enters the server's address in **Server host** (port `6969`) and presses **Join**. Check it's up with `curl http://<server>:6969/health` or `docker logs ww-online`.

## Docker Compose

[`deploy/docker-compose.yml`](../deploy/docker-compose.yml) is a ready-made example with every setting. Copy it to the server, edit the environment, then:

```
docker compose up -d          # start it (it restarts on reboot and after a crash)
docker compose logs -f        # watch players join
```

## Settings

Everything is set through environment variables (`-e NAME=value`, or `environment:` in compose).

| Variable | Default | What it does |
|---|---|---|
| `WWO_PORT` | `6969` | The port the server listens on inside the container. Change the `-p` mapping to match, e.g. `-p 7000:7000 -e WWO_PORT=7000`. (To only change the port players use, change the left side of the mapping instead: `-p 7000:6969`.) |
| `WWO_SHARED_WALLET` | `true` | The room's starting rules. `true`/`false` (also `1`/`0`, `yes`/`no`, `on`/`off`). The room owner can change them any time from the Room page, so these only set how a fresh room starts. `false` for the seven shared-progress rules and `WWO_ALLOW_WARPING` (with projectiles left `true`) is the Co-op preset. |
| `WWO_SHARED_WORLD` | `true` | |
| `WWO_SHARED_ITEMS` | `true` | |
| `WWO_SHARED_STORY` | `true` | Shared story: story, cutscene and side-quest event flags, the Nintendo Gallery figurines Carlov has made, the dungeon warp jars, Beedle's point card, the postbox letters and side-quest progress (counts, levels and prizes won). |
| `WWO_SHARED_BAIT` | `true` | Shared bait bag: the bait bag's All-Purpose Bait and Hyoi Pears are one room total. |
| `WWO_SHARED_SPOILS` | `true` | Shared spoils bag: the spoils bag's counts (Joy Pendants, Skull Necklaces, Chu Jellies...) are one room total. |
| `WWO_SHARED_DELIVERY` | `true` | Shared delivery bag: the delivery bag's quest items (trade goods, letters, Cabana Deed, Beedle's tickets) are one room bag, and Windfall's pedestals are the room's. |
| `WWO_SHARED_PROJECTILES` | `true` | Other players' projectiles: their bombs, boat-cannon shots and arrows are real in your world (they fly, explode and hit your enemies and walls). `false` drops them; their carried bomb and aim poses still show. On in both presets. |
| `WWO_ALLOW_WARPING` | `true` | Allow warping: players can warp to each other from the Room page's Players list (same area: next to them; anywhere else: the entrance they came in through). It gets a player unstuck when shared story or world progress closes their way forward ([softlocks](softlocks.md)). `false` hides the button and the server stops passing on where players entered their area. Off in Co-op. |
| `WWO_OWNER_KEY` | none | A secret. The player who enters it in the app's **Owner key** field becomes the room owner. See [The room owner](#the-room-owner). |
| `WWO_LOG_FILE` | none | Also write the log to this file, e.g. `/data/server.log` (mount a volume on `/data` to keep it). The log always goes to the console, which is what `docker logs` shows. |

A bad value (a port that isn't a number, a rule that isn't true or false, an owner key over 256 characters) stops the server at start-up with a message saying which variable is wrong.

**In Docker, set the port with `WWO_PORT`, not a command-line argument.** The image's health check reads `WWO_PORT` to know which port to poll, so a port passed as an argument (`docker run ... ww-online-server 7000`) would leave the container marked unhealthy even though the server works.

**Log file on a bind mount:** the server runs as UID 1654 (the image's `app` user). A named volume on `/data` just works; a host folder mounted there (`-v ./logs:/data`) must be writable by that UID, e.g. `sudo chown 1654 ./logs`, or the server can't create `WWO_LOG_FILE` (it logs an error and carries on with the console log).

Outside Docker, the server takes the same variables, plus command-line flags, which win over the variables: `WWOnline.Server [port] [--owner-key <key>] [--log-file <path>] [--no-shared-wallet] [--no-shared-world] [--no-shared-items] [--no-shared-story] [--no-shared-bait] [--no-shared-spoils] [--no-shared-delivery] [--no-shared-projectiles] [--no-warping]`. `--help` lists them and `--version` prints the version.

## The room owner

The room owner picks the rules (Full sync, Co-op or a mix), and their game seeds the room's items, story, wallet, bait bag, spoils bag and delivery bag when the room is new.

- **Without an owner key**, the first player to join owns the room. If they leave, the player who joined earliest after them takes over.
- **With `WWO_OWNER_KEY`**, the player who enters that key in the app's **Owner key** field (under Server host, before pressing **Join**) owns the room, even if someone else joined first. The app sends it again after every reconnect, so the owner keeps the room. While the key holder isn't connected, the earliest joiner owns the room, and the first player to join an empty room seeds it. While the key holder is connected, their game seeds an empty room; if it hasn't within 30 seconds (say they're still in the menus), a joiner seeds it so nobody is stuck waiting.

Pick a long random key (`openssl rand -hex 16`) and give it only to whoever should run the room. Everyone else leaves the field empty. A wrong key is ignored (the server logs `ClaimRoomOwner rejected`): that player just joins as a normal player. After 5 wrong keys on one connection the server stops listening to that connection's claims (the player has to leave and join again to retry), so guessing is slow and can't flood the log. The app forgets the key when you change the address or leave, so it's never sent to a different server. The Room page shows the owner badge to whoever owns the room.

Without TLS (below), the key crosses the internet unencrypted, like everything else the app sends. It stops a friend from taking the room by accident. It is not a password for the server: anyone who knows the address can still join.

## Ports and firewalls

The server uses one port, TCP `6969` by default (SignalR over WebSockets, plus HTTP for `/health`). Nothing else needs opening.

- **VPS:** allow inbound TCP 6969 in the provider's firewall (security group, cloud firewall).
- **Home server:** forward TCP 6969 on the router to the machine running Docker.
- **ufw users:** Docker's published ports bypass ufw rules. `-p 6969:6969` is reachable from anywhere even if ufw denies it. To limit it, publish on one address only, e.g. `-p 100.101.102.103:6969:6969` (a Tailscale IP) or `-p 127.0.0.1:6969:6969` (behind a reverse proxy on the same machine).

## Where to run it

- **Tailscale (easiest and private):** run the container on any machine in your tailnet and share the machine with your friends (or have them join your tailnet). They connect to its Tailscale IP or MagicDNS name, port 6969. Nothing is exposed to the internet and no router changes are needed. Publish on the Tailscale IP only (see above) to keep it off your LAN and the internet.
- **A VPS:** any small Linux VPS with Docker. arm64 machines (Oracle's free Ampere instances, a Raspberry Pi 4/5) work too: Docker pulls the right architecture.
- **Latency matters more than bandwidth.** Pick a location near your players. The server relays each player's position many times a second, but the messages are small.

## TLS with a reverse proxy (optional)

By default the app talks to the server over plain HTTP/WebSockets. To encrypt it (and hide the owner key), put the server behind a reverse proxy with a certificate, and players enter the **full URL** in Server host, e.g. `https://wwo.example.com`. With a URL there the app ignores the Port field: the port is the URL's own, or 443 for `https://` and 80 for `http://` when it has none (`https://wwo.example.com:8443` for another). Only `http://` and `https://` URLs work; the app says so for anything else (`ws://` and so on). Players on older app versions can't connect through a URL: everyone in a room needs the same version anyway.

[Caddy](https://caddyserver.com/) gets the certificate by itself and proxies WebSockets without extra settings. A `Caddyfile`:

```
wwo.example.com {
    reverse_proxy ww-online:6969
}
```

With both in one compose file (the WW-Online service needs no `ports:` then, only Caddy publishes 80 and 443):

```yaml
services:
  ww-online:
    image: ghcr.io/datstarkey/ww-online-server:latest
    restart: unless-stopped
    environment:
      WWO_OWNER_KEY: "change-me"
  caddy:
    image: caddy:2
    restart: unless-stopped
    ports: ["80:80", "443:443"]
    volumes:
      - ./Caddyfile:/etc/caddy/Caddyfile:ro
      - caddy-data:/data
volumes:
  caddy-data:
```

Other proxies (nginx, Traefik) work too if they pass WebSocket upgrades (`Upgrade`/`Connection` headers) and don't time out idle connections too quickly. SignalR pings every 15 s. A URL with a path (`https://example.com/wwo/gamehub`) is used as it is, for a proxy that serves the hub under a sub-path.

## Health check and logs

`GET /health` answers `200` with:

```json
{"status":"ok","app":"WW-Online","version":"0.6.2","commit":"<git sha>","protocol":11,"players":2}
```

The image's `HEALTHCHECK` polls it, so `docker ps` shows `healthy`. Point an uptime monitor at it if you like. `version` and `protocol` are what players' apps must match (see [Updating](#updating)).

`docker logs -f ww-online` shows joins and leaves, room rule changes, what each player added to the shared world, items and story, and a `[stats]` line every 10 seconds while players are connected.

## Updating

Pull the new image and recreate the container:

```
docker compose pull && docker compose up -d
# or, without compose:
docker pull ghcr.io/datstarkey/ww-online-server:latest
docker rm -f ww-online && docker run -d --name ww-online --restart unless-stopped -p 6969:6969 ghcr.io/datstarkey/ww-online-server:latest
```

Tags:
- `latest`: the newest stable release. Players' apps update themselves to the newest release too, so `latest` keeps the server in step with them.
- `0.1`: the newest `0.1.x`.
- `0.1.0`: exactly that release. Pre-releases (`0.2.0-beta.1`) only get their exact tag, never `latest` or `0.2`.

The server and the players' apps must speak the same protocol. A player on a different one is turned away with a message telling them who needs to update. After a release that changed the protocol, update the server when your players update.

`docker stop` (and so an update) stops the server cleanly within a second or two. Connected apps try to reconnect by themselves for about 40 seconds, so a quick update is usually just a hiccup. After that, players press **Join** again.

## Room state lives in memory

The room (rules, shared world flags, items, story flags, wallet and bags) lives in the server's memory. **A restart or update empties it.** Every player's own game still has its progress, so when players reconnect, the owner's game seeds the room again and the others' merge in. What doesn't come back: rule changes (the room starts again from the `WWO_SHARED_*` settings), the shared wallet and bag totals (they restart from the owner's game), and items the owner had removed from the room if another player still has them. Saving the room to a volume so it survives restarts is planned.

## Without Docker

Every release also has two server zips:
- `WW-Online-Server-<version>-portable.zip`: runs anywhere with the [ASP.NET Core 9 runtime](https://dotnet.microsoft.com/download/dotnet/9.0): `dotnet WWOnline.Server.dll`.
- `WW-Online-Server-<version>-win-x64.zip`: Windows, no .NET needed: `WWOnline.Server.exe`.

They take the same environment variables and flags. Use a service manager (systemd, NSSM) to keep them running. It sends SIGTERM / Ctrl+C for a clean stop.

## Building the image yourself

From the repo root (the Dockerfile is `WWOnline.Server/Dockerfile`; the build context is the whole repo, and `.dockerignore` keeps everything but the server, the shared project and the build props out):

```
docker build -f WWOnline.Server/Dockerfile -t ww-online-server --build-arg VERSION=0.6.2 --build-arg COMMIT=$(git rev-parse HEAD) .
docker run -d --name ww-online -p 6969:6969 ww-online-server
```

Both build args are optional: without `VERSION` the image reports `Directory.Build.props`'s dev version. The build stage compiles once on the build machine's own platform (the output is platform-neutral .NET), so a multi-arch build (`docker buildx build --platform linux/amd64,linux/arm64 ...`) doesn't run the SDK under emulation. The runtime image is `mcr.microsoft.com/dotnet/aspnet:9.0-alpine` and runs as the non-root `app` user (UID 1654). It has a shell for debugging: `docker exec -it ww-online sh`.
