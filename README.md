# Horror Game

Unity scripts for a first-person horror prototype set in a dark forest.

## Scripts

| Script | What it does |
| --- | --- |
| `Assets/Scripts/Player/FirstPersonController.cs` | Walk, run, jump and crouch (CharacterController based), mouse look, head bob. |
| `Assets/Scripts/Player/Flashlight.cs` | Spot light on the camera, toggled with **F**. Runs on a battery (bar in the bottom left) that dims and flickers when low. |
| `Assets/Scripts/World/BatteryPickup.cs` / `BatterySpawner.cs` | Batteries scattered around the map each run. Click one to refill the flashlight by 25%. |
| `Assets/Scripts/World/DarkForestGenerator.cs` | Procedurally builds a 320m x 320m dark forest: rolling ground, pines, dead trees, swaying grass, rocks, logs, fog and moonlight. Spawns the player and the other systems too. |
| `Assets/Scripts/World/ShadowEntity.cs` | A pitch-black silhouette that watches you from the dark. It creeps closer when you're not looking, grows and advances if you get near it in the dark, circles lamps you're sheltering in, and poofs if you face it down with the flashlight. |
| `Assets/Scripts/World/Lamp.cs` / `LampSpawner.cs` | Unlit lamps scattered around the map. Click one repeatedly to light it, making a safe zone the entities can't enter. Lamps run out of energy (flickering as they fade), then need to cool down. Every lamp lit summons another entity. |
| `Assets/Scripts/Player/LampInteractor.cs` | Clicking lamps and batteries, plus the HUD (crosshair, lamp bars, lamp counter top right). |
| `Assets/Scripts/Player/IntroText.cs` | Opening narration: lines fade in and out one by one over a black screen that slowly reveals the forest. Edit the lines in the inspector. |
| `Assets/Scripts/Player/GameOverScreen.cs` | Fade to black when caught, then resets the run. |
| `Assets/Scripts/World/WeatherSystem.cs` | Wind that sways the trees in rolling gusts, with wind, leaf rustle and tree creak audio. |
| `Assets/Scripts/World/Shack.cs` / `ShackDoor.cs` / `ItemPickup.cs` | Three abandoned shacks placed at random, each with a creaky door you can open and a torch and lighter on the table inside. |
| `Assets/Scripts/Player/Footsteps.cs` | Very faint footsteps on the dirt (quieter crouching, a little louder running) and a thump on landing. |
| `Assets/Scripts/Player/Hotbar.cs` | Hotbar: 1 flashlight, 2 torch, 3 lighter. Lit torches and lighters keep the entities away, but burn out for good. |
| `Assets/Scripts/World/Watchtower.cs` / `WatchtowerButton.cs` / `Ladder.cs` | One very tall watchtower somewhere on the map. Climb the ladder and press the button: floodlights blaze for a minute, every entity's hunt is reset, and the lit area is safe. Then it recharges. |
| `Assets/Scripts/World/FallingTreeEvent.cs` | Every minute or so (first one about 30 seconds in), a nearby tree cracks at the base and comes crashing down. `Trigger()` sets one off any time. |
| `Assets/Scripts/World/ProceduralTextures.cs` | Generates the forest-floor dirt texture in code. |
| `Assets/Scripts/World/SafeZones.cs` | Every light the entities fear (lit lamps, torch, lighter) in one place. |
| `Assets/Scripts/World/WorldWrap.cs` | Makes the map loop: walk off one edge and you come back in on the opposite edge, seamlessly. |
| `Assets/Scripts/World/Wildlife.cs` | Beetles crawling on the ground, rats that scatter and squeak, moths around lit lamps, bats swooping overhead, dark owls in the trees whose white eyes follow you, and black deer that graze and gallop off when you get close. |
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
- Somewhere there's a tall watchtower. Climb the ladder (W, or look down and W / S to go down) and press
  the button at the top: floodlights drive every one of them away and light up the area for a minute.
- Lamps burn out. They flicker as their energy runs low, go dark, and need time to cool down before
  they can be lit again.

## Controls

| Key | Action |
| --- | --- |
| WASD / Arrows | Move |
| Mouse | Look |
| Left Shift | Run |
| Space | Jump |
| Left Ctrl / C | Crouch (hold, or toggle via `toggleCrouch`) |
| F | Use the item in hand: flashlight on/off, light / put out the torch, flick the lighter |
| 1 / 2 / 3 | Hold the flashlight / torch / lighter |
| Left click | Charge the lamp you're looking at (click repeatedly), use a battery, open a door, pick up an item |
| Esc | Release the cursor (click to recapture) |

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
