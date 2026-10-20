# Horror Game

Unity scripts for a first-person horror prototype set in a dark forest.

## Scripts

| Script | What it does |
| --- | --- |
| `Assets/Scripts/Player/FirstPersonController.cs` | Walk, run, jump and crouch (CharacterController based), mouse look, head bob. |
| `Assets/Scripts/Player/Flashlight.cs` | Spot light on the camera, toggled with **F**. Runs on a battery (bar in the bottom left) that dims and flickers when low. |
| `Assets/Scripts/World/BatteryPickup.cs` / `BatterySpawner.cs` | Batteries scattered around the map each run. Click one to refill the flashlight by 25%. |
| `Assets/Scripts/World/DarkForestGenerator.cs` | Procedurally builds a 320m x 320m dark forest: rolling ground, granite rocks and big half-buried boulders, pines (bark-textured, root-flared trunks and drooping needle boughs), dead trees with forked twigs, swaying grass, rocks, logs, fog and moonlight. Spawns the player and the other systems too. |
| `Assets/Scripts/World/BlindEntity.cs` / `Noise.cs` | At 3 lamps lit, a larger, eyeless entity appears and roams near you, clicking softly. It's blind (the flashlight does nothing) but hears footsteps (running carries far, crouching barely), bushes, doors, lamp cranking, the flare gun... and rushes to the sound. Moving at all right beside it gives you away. Throw a glowstick to lure it off. Lights keep it out. |
| `Assets/Scripts/World/ShadowEntity.cs` | A pitch-black silhouette that watches you from the dark. It creeps closer when you're not looking, grows and advances if you get near it in the dark, circles lamps you're sheltering in, and poofs if you face it down with the flashlight. A quarter of them have one or two extra pairs of arms. Flash the light on and off at it rapidly and it shrieks and charges at your face, vanishes, and comes back sooner and angrier. |
| `Assets/Scripts/World/Lamp.cs` / `LampSpawner.cs` | 10 unlit lamps scattered around the map (`lampCount` on the Lamp Spawner). Click one repeatedly to light it, making a 13m safe zone the entities can't enter; once lit it stays on (untick `stayLit` to make lamps run down, flicker out and cool down). Every lamp lit summons another entity. All lamp settings are on the Lamp Spawner. |
| `Assets/Scripts/Player/LampInteractor.cs` | Clicking lamps and batteries, plus the HUD (crosshair, lamp bars, lamp counter top right). |
| `Assets/Scripts/Player/IntroText.cs` | Opening narration: lines fade in and out one by one over a black screen that slowly reveals the forest. The lines live in `Assets/Scripts/Resources/IntroText.txt`, one per line. |
| `Assets/Scripts/Player/GameOverScreen.cs` | Fade to black when caught, then resets the run. |
| `Assets/Scripts/World/WeatherSystem.cs` | Wind that sways the trees in rolling gusts, with wind, leaf rustle and tree creak audio. |
| `Assets/Scripts/World/Shack.cs` / `ShackDoor.cs` / `ItemPickup.cs` | Three abandoned shacks placed at random, each with a creaky door you can open and a torch and lighter on the table inside. |
| `Assets/Scripts/Player/PauseMenu.cs` | Esc pauses: resume, restart the run, lighting test panel, mouse sensitivity and volume (remembered), quit. |
| `Assets/Scripts/Player/LightingSettings.cs` | Lighting test panel (pause menu → Lighting): themes, live moon / ambient / fog / flashlight / shadow controls, and "Log Values" to print a setup. |
| `Assets/Scripts/Player/Footsteps.cs` | Very faint footsteps on the dirt (quieter crouching, a little louder running) and a thump on landing. |
| `Assets/Scripts/Player/Hotbar.cs` | Hotbar: 1 flashlight, 2 torch, 3 lighter, 4 flare gun, 5 glowsticks. Lit torches and lighters keep the entities away, but burn out for good. |
| `Assets/Scripts/World/Watchtower.cs` / `WatchtowerButton.cs` / `Ladder.cs` | One very tall watchtower somewhere on the map. Climb the ladder and press the button: floodlights blaze for a minute, every entity's hunt is reset, and the lit area is safe. Then it recharges. |
| `Assets/Scripts/World/FallingTreeEvent.cs` | Every minute or so (first one about 30 seconds in), a nearby tree cracks at the base and topples under physics, splintering into sections (and flying shards) that settle into the lie of the ground. `Trigger()` sets one off any time. |
| `Assets/Scripts/World/ProceduralTextures.cs` | Generates the textures in code: realistic pebbly soil for the ground (darken it with `groundTint` on the generator), bark, needles, granite, worn paths and the sky eye. |
| `Assets/Scripts/World/WatcherEvent.cs` | A colossal entity hangs dead by its neck from a chain far off in the sky from the start. After 5 lamps: a giant red eye opens in the sky (while it's open the view stretches into a dream hallway and you can't make any progress walking or running), the world turns red, and as the dream ends the chain snaps: the giant drops out of the sky and walks toward you, stopping now and then to scan for you, while more giants far behind it, faded in the fog, sprint to catch up. The watchtower floodlights drive it back. After 10 lamps, searchlights blaze from its eyes and follow you, and a crowd of entities comes out of the dark to circle the edge of the light. |
| `Assets/Scripts/Resources/HorrorUnlitNoFog.shader` | Flat colour that ignores fog, for the eye and the giant. |
| `Assets/Scripts/World/FallingLeaves.cs` | Leaves drifting down from the canopy around you, carried by the wind, settling, and skittering along in strong gusts. |
| `Assets/Scripts/World/Campsite.cs` / `Flare.cs` | Three abandoned campsites (tent, smoking burnt-out campfire seen from afar, backpack with a flare gun and 10 glowsticks). Fire a flare into the sky to wipe every entity's hunt progress. |
| `Assets/Scripts/Resources/HorrorSmoke.shader` | Soft, fog-proof smoke puffs for the campfires. |
| `Assets/Scripts/World/BushRustle.cs` | Bushes (small to medium) you can run straight through: they lean away, shake and rustle as you pass. |
| `Assets/Scripts/Audio/MusicManager.cs` | Soundtrack manager: picks a mood (Calm, Intro, Tension, Danger, Watcher, GameOver) from what's happening and crossfades shuffled playlists. Drop music into `Assets/Scripts/Resources/Music/<Mood>/` (see the README there) or assign it in the Inspector. Music volume is in the pause menu. |
| `Assets/Scripts/Audio/EntityDread.cs` | Within ~7m of a visible entity the forest goes silent (drone, wind, noises, even your footsteps) and slow breathing starts right behind your head. |
| `Assets/Scripts/World/VineSystem.cs` | Vines hanging from the branches of a few trees (4%). Rope physics: they swing in the wind, move with the tree, trail on the ground, and get pushed aside by you and the entities. |
| `Assets/Scripts/World/LampPaths.cs` | Faded paths worn into the dirt around each lamp post: a trodden ring round the pole and a few trails wandering off. |
| `Assets/Scripts/World/PortalDoor.cs` | A lone door between two lit lamp posts; click to open or shut it. Through the doorway is a vast, near-black room with rows of pillars and one dim light; round the back it's just forest. Walk through to enter; in the room the door shows the forest. Inside, the forest's sounds fade to an echoing silence with only your footsteps. Entities can't follow you in. (Shader `Resources/HorrorPortal.shader`.) |
| `Assets/Scripts/World/BrokenCar.cs` | A broken-down car parked somewhere at random. Open the driver's door and click the switch inside for headlights: standing in the beams is safe until the battery dies (about 2 minutes of light). |
| `Assets/Scripts/World/Glowstick.cs` | Yellow glowsticks (10 per campsite). Hold with 5, F cracks one and tosses it ahead; it glows on the ground for minutes, marking your way. The entities ignore them. |
| `Assets/Scripts/World/NightSky.cs` | Stars and drifting clouds on a dome around the camera (shader `Resources/HorrorNightSky.shader`). Purely visual: it adds no light. |
| `Assets/Scripts/World/ScreamEvent.cs` | A distant scream from a random direction in the trees, right as the last intro line fades out (50 seconds in on runs without the intro). |
| `Assets/Scripts/World/SafeZones.cs` | Every light the entities fear (lit lamps, torch, lighter) in one place. |
| `Assets/Scripts/World/WorldWrap.cs` | Makes the map loop: walk off one edge and you come back in on the opposite edge, seamlessly. |
| `Assets/Scripts/World/Wildlife.cs` | Beetles crawling on the ground, rats that scatter and squeak, moths around lit lamps, bats swooping overhead, crows flying over high up, dark owls in the trees whose white eyes follow you, and black deer that graze and gallop off when you get close. |
| `Assets/Scripts/Audio/AmbienceAudio.cs` | Background drone, a tension layer that swells near entities, and random noises in the dark. |
| `Assets/Scripts/Audio/ProceduralAudio.cs` | Synthesizes every sound in code, so no audio files are needed. Each script has clip slots to swap in real sounds. |
| `Assets/Scripts/World/MaterialFactory.cs` | Placeholder materials for Built-in and URP. |
| `Assets/Scripts/Input/HorrorInput.cs` | Input wrapper that works with both the new Input System and the legacy Input Manager. |

## Quick start

1. Create a new 3D project (Built-in or URP), Unity 2022.3 or newer.
2. Copy the `Assets/Scripts` folder into your project.
3. Open any scene that has no player in it (the default SampleScene is fine) and press Play.
   The forest, player, entity, lamps, batteries, wildlife, wind and audio are all created automatically.
   To tweak settings, add **DarkForestGenerator** to a GameObject yourself; auto-creation can be
   switched off with `DarkForestGenerator.autoCreateInEmptyScenes`.

Tip: right-click the DarkForestGenerator component header and choose **Generate** to build the forest in edit mode, so you can look around and tweak it. Change `seed` for a different layout.

## How to play

- Something watches you from the dark. Each time you look away, it creeps a little closer.
  Let it get too close and it takes you.
- Walk at it with your flashlight on it and you'll slow to a halt; it shrieks and vanishes, then
  reappears far away. That's the only way to drive it back.
- Get near it *without* the flashlight on it and it grows, and comes for you. A pounding heartbeat
  means it's coming. If it reaches you, you die.
- Find the lamps and click them repeatedly to light them. In the light they can't hurt you, but when
  you aren't watching they gather and circle just outside it. Every lamp you light summons another one.
- Your flashlight drains while it's on. Look for batteries (a faint pulsing green light) and click them
  to top it up by 25%.
- Search the abandoned shacks for torches and lighters. Lighting a torch takes a little of the lighter's
  fuel; a lit torch keeps them out of a wide circle around you, a lighter a small one. Both burn out for good.
- Look for thin columns of smoke: they lead to campsites, each with a flare gun. Fire it into the sky
  (press 4, aim up, F) and every one of them has to start its hunt over. Fired low, it's wasted.
- Somewhere there's a tall watchtower. Climb the ladder (W, or look down and W / S to go down) and press
  the button at the top: floodlights drive every one of them away and light up the area for a minute.
- Once lit, a lamp stays on. But every lamp you light summons another entity.

## Controls

| Key | Action |
| --- | --- |
| WASD / Arrows | Move |
| Mouse | Look |
| Left Shift | Run |
| Space | Jump |
| Left Ctrl / C | Crouch (hold, or toggle via `toggleCrouch`) |
| F | Use the item in hand: flashlight on/off, light / put out the torch, flick the lighter |
| 1 / 2 / 3 / 4 / 5 | Hold the flashlight / torch / lighter / flare gun / glowsticks |
| Mouse wheel | Switch to the next / previous item you have |
| F1 | Debug: show where everything spawned (labels through walls, plus a list) |
| Left click | Charge the lamp you're looking at (click repeatedly), use a battery, open a door, pick up an item |
| Esc | Pause menu (resume, restart, mouse sensitivity, volume, quit) |

## Using your own player

1. Empty GameObject "Player" with its pivot at the feet.
2. Add **FirstPersonController** (adds a CharacterController).
3. Add a child Camera, assign it to `cameraRoot`.
4. Add **Flashlight** to the camera.

The forest generator won't spawn a second player if one already exists.

## Notes

- The generator makes placeholder art from primitives. Drop real tree/rock prefabs into `treePrefabs` / `rockPrefabs`, or assign materials in the Material Overrides section.
- Placeholder materials are created with `Shader.Find` (Standard or URP Lit). If they show up pink in a build, assign materials in the inspector instead.
- In URP, make sure "Additional Lights" and additional light shadows are enabled in the URP asset so the flashlight lights and casts shadows.
