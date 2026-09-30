# Room rules

A room's **rules** decide how much of the adventure its players share. The **room owner** picks them on the **Room** page (**Edit room**), and can change them at any time.

## Presets

- **Full sync** turns every rule on: one adventure for the whole room. You can split up and finish different parts of the game.
- **Co-op** turns the seven shared-progress rules and warping off, and keeps other players' projectiles on. You see each other and fight together, but everyone keeps their own progress.
- **Custom** lights up when the rules are a mix. Pick a preset to reset them.

## The rules

| Rule | What it shares |
|---|---|
| **Shared wallet** | One rupee purse. Anyone's rupees count for everyone. |
| **Shared world** | Chests, switches, pickups, small keys and sunken treasure: once someone takes it, it's gone for everyone. |
| **Shared items** | Items, upgrades, songs, pearls, Triforce shards, the magic meter, the sea chart and treasure charts, and max hearts. |
| **Shared story** | Story progress, side-quest progress, the Nintendo Gallery figurines, the dungeon warp jars, Beedle's point card and the postbox letters. |
| **Shared bait bag** | One stock of All-Purpose Bait and Hyoi Pears. |
| **Shared spoils bag** | One stock of Joy Pendants, Skull Necklaces, Chu Jellies, Knight's Crests and the other spoils. |
| **Shared delivery bag** | One bag of quest items: trade goods, letters, the Cabana Deed and Beedle's tickets, and what stands on Windfall's pedestals. |
| **Other players' projectiles** | Other players' bombs, boat-cannon shots and arrows are real in your game. |
| **Allow warping** | A **Warp to** button on each player in the Players list. |

The details of each follow.

### Shared wallet

Anyone's rupees count for everyone: a pickup adds to the room's purse, a purchase takes from it.

### Shared world

A chest anyone opens is open for everyone, and a switch anyone hits, a wall anyone bombs or a pickup anyone collects is done for everyone. When you're in the same room it happens **live**: a chest opens empty, a bombed wall vanishes, a locked door comes back unlocked, a ladder drops or a torch lights when another player clears the room. Some dungeon objects (the Forbidden Woods flower house, Dragon Roost's flame lift, Makar's trees) wait until you step away from them before they change.

- **Small keys**: a key anyone finds is everyone's, and a door anyone unlocks uses it up for everyone. The **Dungeons** card on the Room page shows each dungeon's keys.
- **Sunken treasure**: when one player salvages a treasure (from a chart, a light ring, the octoroks and so on), it's gone for the others straight away, the light and the spot included.

### Shared items

One player finding an item unlocks it for the whole room: equipment, the item menu, bags and their upgrades, songs, pearls and Triforce shards.

- **Hearts** come from the room's pieces: every Heart Container and Piece of Heart anyone gets counts once for everyone. The Room items page shows how many of the 6 Heart Containers and 44 Pieces of Heart the room has.
- **Magic** follows what the room has earned: normal magic with the Deku Leaf, double magic from the Great Fairy.
- **Sea chart and treasure charts**: the treasure and Triforce charts anyone owns, the charts anyone has had deciphered, the treasure anyone has salvaged and the squares anyone has filled in on the sea chart.
- Items only ever grow: only the room owner can take an item away (Room items page, **Edit items**).

### Shared story

- **Story progress**: the story's events, so you can split up and each finish different parts of the game. Some events move a player as well as setting a flag (leaving Outset with Tetra, for one), which can leave the others stuck: see [Softlocks and warping](../softlocks.md).
- **Nintendo Gallery**: a figurine Carlov makes for anyone is made for everyone, so you can split the Picto Box photos between you. (The photos themselves are never shared: each player gives Carlov their own.)
- **Warp jars**: a dungeon warp jar anyone opens is open for everyone.
- **Beedle's point card**: the room keeps the highest card, and everyone's is raised to it. Points are never taken away. (Two players buying at the very same moment can lose a point between them.)
- **Postboxes**: a letter one player has read is read for everyone, so you don't each unlock the same letters. Its reward reaches everyone through the other rules: treasure charts through Shared items, heart pieces through your hearts, the Complimentary ID and Fill-Up Coupon through the delivery bag, rupees through the wallet.
- **Side quests**: progress the game keeps as a count or a level is the room's too, so nobody redoes it or is paid twice: the Joy Pendants given to Mrs. Marie, Orca's lessons, Koboli's mail sorting, the prizes won at Sploosh Kaboom and barrel shooting, the Ghost Ship once it's cleared, and the pigs in Rose's pen. Hand pendants to Mrs. Marie one player at a time.

The room's story flags are on the Room page (**Story flags**), and the room owner can edit them.

### Shared bait bag, spoils bag and delivery bag

Each bag is one stock for the room. Anyone's purchase, pickup, sale, trade or use counts for everyone.

- **Delivery bag**: an item anyone receives is in everyone's bag, and one anyone hands over, posts or trades is gone from everyone's. If two players trade the same item at the same moment, only the first trade counts: nothing is ever duplicated.
- **Windfall's pedestals** belong to the delivery bag: a trade good one player sets on a pedestal stands there for everyone (you see it the next time you come to Windfall), and anyone can take it back into the bag. Decorating the town can be shared out.
- An item that only reached you from another player counts as already obtained, so you won't see its first-pickup message later.

### Other players' projectiles

Other players' bombs, boat-cannon shots and arrows (normal, fire, ice and light) are real in your game: they fly, explode and hit your enemies, walls and switches. They never hurt you, and players can't hurt each other. With the rule off you still see another player carry a bomb or aim, but nothing flies. This rule is on in both presets.

### Allow warping

A **Warp to** button next to each player in the Players list. In the same area it moves you next to them; anywhere else it takes you to the entrance they came in through. See [Softlocks and warping](../softlocks.md).

## Never shared

Health, magic, bomb and arrow counts, bottle contents, and Picto Box photos. The bait, spoils and delivery bags are only shared under their own rules.

## Joining a room

- **Joining never throws away progress.** When the room is new, the owner's game fills it (its items, story, wallet and bags). Anyone who is further ahead than the room adds their progress to it when they join.
- **The room owner** is the player who hosts the room. On a [dedicated server](../self-hosting.md#the-room-owner) it's the first player to join, or whoever enters the server's owner key. If the owner leaves, the player who joined earliest after them takes over.
- **The room lives in the server's memory.** Restarting the server (or closing the host's app) empties it, but everyone's own game keeps its progress, and the room fills up again when players rejoin. Rule changes don't come back: the room starts again from its default rules.
