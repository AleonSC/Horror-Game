# Using your own assets

Every piece of art in the game has a built-in version made in code, and a slot where your own can go
instead. Leave a slot empty and the built-in one is used. Fill it and yours is used from the next Play.

| What | Where the slot is |
| --- | --- |
| Trees, bushes, grass, tall grass, rocks, logs | `Assets/Config/World/WorldArtConfig` |
| Ground, trail and water materials | `WorldArtConfig` |
| Lamp post, cabin, shack, campsite, watchtower, car, landmark tree, the lone door | `WorldArtConfig` |
| The scour | `Assets/Config/Entities/ScourConfig` (see [custom-scour.md](custom-scour.md)) |
| Any sound | `Assets/Config/Audio/AudioConfig` > **Sound Overrides** |
| The flashlight's beam shape | `Assets/Config/Player/FlashlightConfig` > **Cookie** |
| Items, held and lying in the world (flashlight, battery, torch, lighter, flare gun, glowstick, camera, key fob, map, snack) | `Assets/Config/Player/ItemsConfig` > **Art** |

Later systems (the other creatures, items, the player's body, music, the HUD) will get slots the same way.

## Plants, trees, rocks and logs

Drag in any number of **prefabs** per category. Each one becomes a variant, and the generator picks among
them.
- **Many copies are drawn very cheaply** (GPU instancing). The game reads each prefab's meshes, sub-meshes
  and materials, and its **LOD Group** if it has one, so far-off copies use your lower levels.
- **Your trees' real heights are measured** and fed into the world plan, so the giant stays visible from the
  start and the sky above every lamp stays open, whatever size they are. **Tree Size** scales them all.
- **Expected sizes for the other categories:**
  | Category | Make it |
  | --- | --- |
  | Grass | about 1 m tall |
  | Tall grass | about 1 m tall (the generator scales each clump) |
  | Rocks | about 1 m across (the same prefabs are used, much bigger, as boulders) |
  | Logs | 1 m long along Z (stretched to each log's length) |
- **Colliders** in your prefabs are ignored for these. The game adds its own simple ones (trunks, big rocks,
  logs), so thousands of trees stay cheap.
- **Old Asset Store packs are converted automatically.** Packs made for Unity's old built-in renderer
  (Standard or Tree Creator shaders, like your Tree9 pack) show up pink in HDRP. Their materials are swapped
  at Play for HDRP stand-ins using the same textures, and leaves are cut out and lit on both sides. The
  Console says which materials were converted. Your project files aren't changed.

## Places

Drag in a prefab for any of them. It's placed and turned where the plan puts that place, with its pivot on
the ground. Your colliders are kept.

**The lamp** needs a little more, so it can be cranked and lit:
- **Either** give the prefab a **Lamp** component and fill in its Light, Marker, Glass and Crank slots,
- **or** just name its parts: a child with **Light** on it (the warm lamp light), and children whose names
  contain **Marker** (blinks red while dark), **Glass** (glows when lit) and **Crank** (what the crosshair
  aims at).

**Your shack, cabin, campsite and car** can say where things go, with children named:
| Child | What it marks |
| --- | --- |
| `Table` | where the shack's items are laid out (on top of it) |
| `Tent` | the tent's floor, where the snack lies |
| `Backpack`, `GloveBox`, `Trunk` | what you click to search |
| `Deck` | (the watchtower) where the map lies |
| `Inside` | a **Box Collider** covering the inside: being in it is sheltered (the collider is only a marker) |
| `Door` | the door: it swings about its pivot (the hinge), so it needs a collider |

Anything you don't mark uses the built-in layout's spot. For **the lone door**, a child named `Door` swings
(or the whole thing, if there isn't one).

## Items

In `ItemsConfig` > **Art**, a **held** model sits at the hand pointing forward (+Z). A **lying** one sits on
the ground at its pivot (empty: the held model is used). A torch or lighter can have a child named
**Flame**: it's shown only while lit, and the game's light sits there. Colliders in item models are removed.

## Sounds

In `AudioConfig` > **Sound Overrides**, add an entry, pick the sound (Creep, Appear, Pound, Crescendo, Portal,
Shriek, Moan, Heartbeat, Breath, Click, LampOn, Pickup, Ignite, Lighter, FlareShot, FlareBurst, Crack, Shutter,
Chirp, DoorOpen, DoorShut, Clunk, Eat) and drag in one or more clips. With several clips, one is
picked at random each time.

## A note on git

The repo is public, so **Asset Store packs stay out of git** (put them in `Assets/Packs/`). If a config slot
points at something that isn't committed, other machines (and CI) just see an empty slot and use the
built-in version. Nothing breaks, it just looks different there. Your own models and sounds can be
committed normally (for example in `Assets/Art/` or `Assets/Audio/`).
