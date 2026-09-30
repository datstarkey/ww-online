# Playing together

Everyone plays their own game in Dolphin on their own PC. A **room** links them up: one player hosts it from the app, and everyone else joins. There is nothing else to install or run: the host's app is the server.

## Host a room

On the **Room** page choose **Host a room**. WW-Online starts the room on your PC (port `6969`), then starts your patched game in Dolphin and links up with it. (To start Dolphin yourself instead, untick **Start Dolphin and attach when you host or join a room** in **Settings**.)

You're the **room owner**: press **Edit room** to pick **Full sync**, **Co-op** or your own mix of rules, then **Done**. The rules stay locked until you press Edit, so nothing changes by accident. See [Room rules](Room-Rules.md) for what each one does.

## Join a room

Enter the host's address in **Server host** (and the port, if it isn't `6969`), your **Player name**, then **Join**. WW-Online starts your game in Dolphin the same way. You see the room's rules and items read-only, and anything you pick up still counts for the room.

Leave **Owner key** empty unless the server's admin gave you one: it makes you the room owner on a [dedicated server](../self-hosting.md#the-room-owner).

Everyone in a room needs the same WW-Online version. If yours is different, the app says who needs to update.

## Playing over the internet

**You don't need a server.** When you press **Host a room**, your app runs the room. Your friends only need to reach your PC on port `6969`:

- **On the same home network:** they join your PC's local IP address (for example `192.168.1.20`; `ipconfig` in a command prompt shows it as the IPv4 address). Nothing else to set up.
- **[Tailscale](https://tailscale.com/)** (recommended over the internet): everyone installs it and joins the host's tailnet, then connects to the host's Tailscale IP. No router changes needed.
- **Port forwarding:** forward TCP `6969` on the host's router to the host's PC, and friends connect to the host's public IP.

If Windows asks whether to let WW-Online use the network when you host, allow it (at least for private networks), or your friends can't connect.

**A dedicated server** is optional: run the room on an always-on machine or VPS, and everyone (the host too) joins it. It's only worth it when you want the room up without the host playing, or when nobody can make their PC reachable. See [Self-hosting](../self-hosting.md).

## Where you see each other

You see another player when you're in the same room of the same area. Out on the Great Sea it goes by distance instead, and you also see everyone on the Great Sea on your sea chart (pause menu), in their tunic colour. You see up to three other players at once.

## The app's pages

- **Room**: host or join, the room's rules (**Edit room** for the owner), the **Players** list (with **Warp to** when the room allows it), what's happening in the room, the **Dungeons** card (each dungeon's small keys, map, compass, big key and boss), your items and the room's items, and the room's **Story flags**. The room owner can edit the room's items and story flags; everyone else sees them read-only.
- **Appearance**: your clothes (game default, hero's tunic or pajamas), your tunic colour and your boat's colour. Everyone in the room sees changes live. **Show player names** turns the names above the other Links on or off, for your screen only.
- **Dolphin**: the link to your game and your live stats. **Start game** starts it by hand; **Attach** links up with a Dolphin you started yourself.
- **Tools**: local-only helpers (warp, stats, memory). They only affect your own game.
- **Settings**: Dolphin, your game folders, [game patches](Game-Patches.md), item icons, updates and **Run setup again**.

## Stuck?

Shared story and world only move forward, so another player's progress can leave you with no way on. **Warp to** them: see [Softlocks and warping](../softlocks.md). For anything else, see [Troubleshooting](Troubleshooting.md).
