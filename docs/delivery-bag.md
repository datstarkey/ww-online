# Shared delivery bag

Room rule **Shared delivery bag** (`RoomSettings.SharedDelivery`, protocol 6). With it on, the room has one delivery bag: a quest item anyone receives is in everyone's bag, and one anyone hands over, posts, trades, sets on Zunari's stand or throws away is gone from everyone's. Full sync turns it on, Co-op turns it off. The owner can toggle it on the Room page; everyone else sees a read-only row. Server: `WWO_SHARED_DELIVERY` / `--no-shared-delivery`. Log lines: `[delivery]`.

§0 is what is built, §1 is the research behind the choice, §2 lists the races, §3 is what only a game session can check.

Target: GZLE01. Source: `tww-decomp/` (paths below are relative to it). **Verified** means checked in the vanilla GZLE01 `main.dol` or RELs.

## 0. What is built: a presence store, not flags

The preferred design (as for small keys, `docs/small-keys.md`) was to derive the bag from synced flags: {items whose "obtained" flag is set and whose "handed over" flag isn't}. §1 shows that doesn't work here. 16 of the 19 items leave the bag without any flag being set, 12 of them can be obtained again, and for two letters the game itself defines "handed over" as "obtained and no longer in the bag". The bag is its own source of truth. So the room keeps the bag itself, like the bait and spoils bags (`SharedBagService<T>`, `BagCountsStore<T>`):

- **`DeliveryCounts`** (Shared): how many of the bag's 8 slots hold each of the 19 items 0x8C-0x9E (the same trade good can fill more than one slot), plus `Obtained` = `mReserveFlags`, the "ever obtained" bits. As a room total the counts fit the 8 slots. A delta has signed counts and the newly obtained bits.
- **Hub:** `JoinDelivery` (the owner's bag seeds the room; with no owner the first joiner does, after `OwnerSeedGate`'s 30 s any joiner; joiners adopt the room's bag), `SendDeliveryDelta` → `ReceiveDeliveryTotal` to everyone. Turning the rule off clears the store; turning it on again re-seeds.
- **`DeliveryStore`** (server) referees every delta against the room's bag:
  - **A delta that takes out an item the room doesn't have is refused as a whole**, gains included. Two players who trade the same Town Flower at once (one for a Sea Flower, the other for an Exotic Flower) both send "-1 Town Flower, +1 ...". Only the first trade counts, so the room ends with one flower, not two. The refused player's game is rewritten to the room's bag. Two players handing over the same letter just lose it once.
  - **One-of-a-kind items** (Father's Letter onwards: the letters, Cabana Deed, Complimentary ID, Fill-Up Coupon) are never held twice. Two players given the same letter before its obtained bit reached the other both send +1, and the room keeps one.
  - **Gains that overfill the 8 slots** are cut (later item numbers first). Losses are never cut.
  - **Obtained bits are OR-merged**, even from a refused delta. They only grow.
  - A refusal or cut is logged as `[delivery] <player> <delta> → <total> (refused: ... / cut: ...)`.
- **`SharedDeliveryService`** (client, 4 Hz): joins once the game has the delivery bag, sends every local change as a delta, and writes the room's bag into the game only while idle (no event, pause menu closed: `SharedBagService.IsIdle`), compare-and-swap against a fresh read of the slots. It writes the way the game does (`DeliveryBag.Apply`):
  - an item that leaves empties a slot holding it (`setReserveItemEmpty`), the highest slot first and a slot on X/Y/Z last;
  - an item that arrives takes the first empty slot (`setReserveItem`, `d_save.cpp:639-648`) and gets its obtained bit (`item_func_*` sets it with the item, `d_item.cpp:1197-1309`). Removals go first, so a trade lands in the slot the traded item left when that is the first empty one, as `setReserveItemChange` does in place;
  - an X/Y/Z button on a changed slot is fixed the way `dComIfGp_setSelectItem` does it (`d_com_inf_game.h:2890-2903`): cleared if its slot emptied, showing the new item otherwise (`BagMemory.FixButtons`, shared with the bait bag);
  - obtained bits are ORed in, never cleared. The sync point's bits are the room's, so bits this game has beyond them (its own save, or an arriving item) are sent next tick and become everyone's.
- **Joining** adopts the room's bag, like the other bags (your own items are replaced by the room's; your obtained bits are added to the room's).

No REL or ASM change: nothing needs a re-patch.

### Memory (verified)

| What | Decomp | Address |
|---|---|---|
| `mReserve[8]` (item numbers, 0xFF empty) | `dSv_player_c /* 0x076 */ mBagItem`, `dSv_player_bag_item_c /* 0x10 */` (`include/d/d_save.h:230`) | GameInfo + 0x86 = **0x803C4C8E** |
| `mReserveFlags` (u32, bit = item - 0x8C) | `dSv_player_c /* 0x090 */ mGetBagItem`, `dSv_player_get_bag_item_c /* 0x0 */` (`d_save.h:246`) | GameInfo + 0x90 = **0x803C4C98** |
| Delivery Bag owned | inventory slot `dInvSlot_DELIVERY_BAG_e` = 18 holds `dItemNo_DELIVERY_BAG_e` 0x30 | 0x803C4C56 |
| Bag slot 0 as a select slot | `dInvSlot_ReserveFirst_e` = 48 (`d_com_inf_game.h:1093`) | save `mSelectItem` / play select items, as for bait |

`main.dol`: `item_func_flower_1` (0x800C468C) calls `onReserve(gameInfo + 0x90, 0)` then `setReserveItem(gameInfo + 0x76, 0x8C)`; `setReserveItem` (0x8005A7E4) stores at `+0x10 + i` into the first 0xFF slot; `onReserve` (0x8005AB24) is `lwz / or / stw` on the word at +0. Pinned by `DeliveryBagMemoryTests.Addresses_AreTheSaveFields`.

## 1. The items and their flags

Every item's `item_func_*` (`src/d/d_item.cpp:1197-1309`) does `dComIfGs_onGetItemReserve(item - 0x8C)` then `dComIfGs_setReserveItem(item)`. So the **obtained** flag is `mReserveFlags` bit (item - 0x8C) for all of them. It is never cleared in the retail game: the only `offGetItemReserve` caller is Zunari's debug menu in the demo build (`d_a_npc_rsh1.cpp:1696`, `VERSION_DEMO`).

What takes an item out of the bag was found by scanning every vanilla REL's relocations for calls to the bag functions (plus `main.dol` and the decomp):

| Caller | Call | What it does |
|---|---|---|
| `daPy_lk_c::procNotUse` (`d_a_player_main.cpp:8558-8562`) | `setReserveItemEmpty(btn)` | Using a trade good in the field asks "throw it away?" (message 0xF0C, only for `isDaizaItem`); yes empties its slot. **No flag.** |
| `d_a_npc_roten` (traders, `d_a_npc_roten.cpp:2441-2449`) | `setReserveItemChange(btn, item)` + `onReserve` | Trade in place: the shown trade good becomes the next one. **No flag** for the item given away. |
| `d_a_dai` (Zunari's stand, `d_a_dai.cpp:198-203`, `311-322`) | `setReserveItemEmpty` / `setReserveItem` | Setting a trade good on the stand empties its slot and stores the item number in an event register (0xD1FF-0xDAFF, `LocalOnly` in `EventFlagCatalog`); taking it back re-adds it with **no flag**. |
| `d_a_npc_rsh1` (Zunari's shop, `:658-676`) | `execItemGet` | Sells trade goods (Town Flower) again and again, until three trade goods are in the bag. |
| `d_a_obj_toripost` (postbox, `:458-463`, `deliverLetter` `:239-247`) | `setReserveItemEmpty` | Posting Maggie's Letter sets event bit 0x1220; posting the Note to Mom sends letter `LETTER_BAITOS_MOM` (register 0xAC03). Father's Letter and Moblin's Letter are refused (message 0xCEA). |
| `d_a_npc_co1` (REL, not decompiled; `.text` 0x22C8) | `setReserveItemEmpty` | Takes the shown item when it is 0x98 (Father's Letter). The path sets **no event bit**. |
| `d_a_npc_kp1` (REL, not decompiled; `.text` 0x1B68) | `setReserveItemEmpty` | Takes the shown item on message 0x1E9D (Moblin's Letter). **No event bit** on that path. |
| `d_a_npc_bs1` (Beedle's shop ship, `:1987-1988`) | `setReserveItemEmpty` | Takes the shown Complimentary ID or Fill-Up Coupon (`ACT_GETTICKET`). `evn_praise_init` / `evn_mantan_init` only refill life / magic / bombs / arrows. **No flag.** |
| `d_a_npc_auction` (REL, not decompiled; `.text` 0x1C48) | `setReserveItemEmpty` | Empties the shown slot on one message path (0x27EE). Not traced further. |

And the game's own "handed over" checks read the bag, not a flag:

- `dNpc_chkLetterPassed` (`src/d/d_npc.cpp:647-653`): Father's Letter passed = `isGetItemReserve(0xC) && checkReserveItem(0x98) == 0`.
- `d_kankyo_dayproc.inc:26`: Moblin's Letter passed = `isGetItemReserve(0xF) && checkReserveItem(0x9B) == 0`.

| # | Item | No. | Obtained bit | Given by | Leaves by | "Handed over" flag | Derivable? |
|---|---|---|---|---|---|---|---|
| 0-11 | Town Flower, Sea Flower, Exotic Flower, Hero's Flag, Big Catch Flag, Big Sale Flag, Pinwheel, Sickle Moon Flag, Skull Tower Idol, Fountain Idol, Postman Statue, Shop Guru Statue | 0x8C-0x97 | 0x0-0xB | Zunari's shop (Town Flower, repeatable), the traders (each trade) | trade, Zunari's stand (and back), thrown away, (auction?) | none; repeatable | **no** |
| 12 | Father's Letter | 0x98 | 0xC | Medli (`d_a_npc_md.cpp:3535`) | handed over (`npc_co1`) | none: the game uses "obtained and not in the bag" | **no** |
| 13 | Note to Mom | 0x99 | 0xD | (`item_func_magic_seed`; giver not traced) | posted | register `LETTER_BAITOS_MOM` 0xAC03 (Shared story, `P.Max`) | only with a register |
| 14 | Maggie's Letter | 0x9A | 0xE | (giver not traced) | posted | event bit 0x1220 | yes |
| 15 | Moblin's Letter | 0x9B | 0xF | `npc_bm1` (offered while bit 0xF is off and event 0x1808 is on, `d_a_npc_bm1.cpp:636`) | handed over (`npc_kp1`) | none: the game uses "obtained and not in the bag" | **no** |
| 16 | Cabana Deed | 0x9C | 0x10 | `npc_ho` (message 0x2743, also sets event bit 0x1C08, `d_a_npc_ho.cpp:315-318`) | never | n/a | yes |
| 17 | Complimentary ID | 0x9D | 0x11 | Beedle's letter (`LETTER_SILVER_MEMBERSHIP`, `d_a_obj_toripost.cpp:49`) | shown at Beedle's shop | none | **no** |
| 18 | Fill-Up Coupon | 0x9E | 0x12 | Beedle's letter (`LETTER_GOLD_MEMBERSHIP`, `:52`) | shown at Beedle's shop | none | **no** |
| - | Salvage items 2 / 3, `XXX_039` | 0xA0-0xA2 | 0x14-0x16 | nothing in the retail game | - | - | not synced (a slot holding one is left alone) |

**Why not a hybrid** (derive what can be derived, store the rest): only Maggie's Letter and the Cabana Deed (and the Note to Mom, through an event register) are covered. Deriving even those needs the obtained bits synced, which no rule does today, so the store would carry them anyway. Two mechanisms for 2-3 items would split one bag menu across two sources. Two rules could then disagree about the same slots (a derived letter written by one path while the other writes a trade), and the result would change with Shared story on or off. The presence store has none of the drift that ruled out a counted small-key store: counts can't go below zero or be duplicated by a race (a delta that takes a missing item is refused whole), one-of-a-kind items are capped at one, gains are capped by the 8 slots, and the obtained bits only OR.

**Why its own rule** and not part of Shared items: Shared items is the room's inventory (`RoomInventory`: owned items, upgrades), which only grows. The delivery bag goes up and down, like the bait and spoils bags, which have their own rules too.

## 2. Races and edge cases

| Case | Handling |
|---|---|
| Two players trade the same item at once | The second delta takes an item the room no longer has: refused whole, their game gets the room's bag back. One trade, one result. |
| Two players hand over / post the same letter | The second -1 is refused. The letter is gone once. |
| Two players are given the same one-of-a-kind item before either hears of the other | The room keeps one (capped). The obtained bit reaches everyone, and NPCs that check it (`npc_bm1`'s offer, `dNpc_chkLetterPassed`) see it. |
| Two players buy the last free slot at once | The later item number is cut. With Shared wallet the rupees are still spent: rare, logged. |
| An item set on Zunari's stand | It leaves the room's bag. The stand is per player (its register is `LocalOnly`): only the player who set it can take it back, which returns it to the room's bag. |
| The room goes past Zunari's "three trade goods" | Zunari and the stand check the local bag. Two players buying at once can go past three; nothing in the game breaks (the bag has 8 slots). |
| A trade or hand-over mid-event | Read and sent at once (reading is safe); the room's bag is written only once the event ends. |
| A change the game makes between our read and our write | The write is compare-and-swap on the slots: raced, nothing written; the change is sent first, then the room's bag applied. |
| Joining with items the room doesn't have | The room's bag is adopted (as with the bait and spoils bags); your obtained bits are added to the room's. |
| A slot holding an unknown item (0x9F-0xA2, a mod) | Left alone, counted as nothing. An item that can't fit is logged (`only ... fits this bag`). |

## 3. In-game checklist (dev-test, Shared delivery bag on)

Nothing below has been run in a game yet.

| # | Do | Expect |
|---|---|---|
| D1 | P1 buys a Town Flower from Zunari | P2's bag shows it once P2 is idle (`[delivery] shared bag ... → applied`) |
| D2 | P1 trades the Town Flower at a trader | Both bags: Sea Flower (or the next good) in the slot the Town Flower had |
| D3 | P1 and P2 trade the same Town Flower at the same moment | Server: one `refused: the room's bag has no Town Flower` line; both bags end with one traded item |
| D4 | P1 sets a trade good on Zunari's stand, then takes it back | It leaves both bags, then comes back to both; P2's stand stays empty |
| D5 | P1 throws a trade good away (use it in the field, answer yes) | Gone from both bags |
| D6 | P1 gets Moblin's Letter from the Rito who gives it (`npc_bm1`), then P2 talks to him | P2 already has it (and its obtained bit), so he doesn't give a second |
| D7 | P2 hands Moblin's Letter over (`npc_kp1`, presumably Maggie) while P1 has it on X | Gone from both; P1's X button clears **(unverified: the HUD redraws the empty button without a menu visit)** |
| D8 | P1 posts Maggie's Letter / the Note to Mom | Gone from both; the letter chain continues (Shared story carries 0x1220 / the letter register) |
| D9 | P1 hands Father's Letter over (`npc_co1`) | Gone from both; Medli treats it as passed in P2's game too (`dNpc_chkLetterPassed`) |
| D10 | P1 uses the Fill-Up Coupon at Beedle's | Gone from both; only P1's meters refill |
| D11 | The owner turns the rule off, then on | `[delivery] shared delivery bag turned off`: bags are per player; back on, the owner's bag seeds and joiners adopt it |
| D12 | Open the pause menu while the other player trades | The bag is rewritten only after the menu closes |
