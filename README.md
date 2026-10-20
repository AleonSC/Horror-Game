# Horror Game

Unity scripts for a first-person horror prototype set in a dark forest.

## Scripts

| Script | What it does |
| --- | --- |
| `Assets/Scripts/Player/FirstPersonController.cs` | Walk, run, jump and crouch (CharacterController based), mouse look, head bob. |
| `Assets/Scripts/Player/Flashlight.cs` | Spot light on the camera, toggled with **F**. Slight sway, optional click sound. |
| `Assets/Scripts/World/DarkForestGenerator.cs` | Procedurally builds a dark wooded area: bumpy ground, pines, dead trees, rocks, logs, fog, moonlight and boundary walls. Can spawn the player too. |
| `Assets/Scripts/World/ShadowEntity.cs` | A pitch-black silhouette that watches you from the dark. When you're not looking it creeps a few metres closer; if it gets close enough, it takes you. Run at it to make it crescendo, poof and rematerialize far away. |
| `Assets/Scripts/World/Lamp.cs` / `LampSpawner.cs` | Unlit lamps scattered around the map. Click one repeatedly to fill its meter and light it, making a safe zone the entities can't enter. Every lamp lit summons another entity. |
| `Assets/Scripts/Player/LampInteractor.cs` | Clicking lamps, plus the HUD (crosshair, lamp meter, lamps lit). |
| `Assets/Scripts/Player/GameOverScreen.cs` | Fade to black when caught, then resets the run. |
| `Assets/Scripts/Audio/AmbienceAudio.cs` | Background drone, wind, a tension layer that swells near entities, and random noises in the dark. |
| `Assets/Scripts/Audio/ProceduralAudio.cs` | Synthesizes every sound in code, so no audio files are needed. Each script has clip slots to swap in real sounds. |
| `Assets/Scripts/World/MaterialFactory.cs` | Placeholder materials for Built-in and URP. |
| `Assets/Scripts/Input/HorrorInput.cs` | Input wrapper that works with both the new Input System and the legacy Input Manager. |

## Quick start

1. Create a new 3D project (Built-in or URP), Unity 2022.3 or newer.
2. Copy the `Assets/Scripts` folder into your project.
3. Open any scene that has no player in it (the default SampleScene is fine) and press Play.
   The forest, player, entity, lamps and audio are all created automatically.
   To tweak settings, add **DarkForestGenerator** to a GameObject yourself; auto-creation can be
   switched off with `DarkForestGenerator.autoCreateInEmptyScenes`.

Tip: right-click the DarkForestGenerator component header and choose **Generate** to build the forest in edit mode, so you can look around and tweak it. Change `seed` for a different layout.

## How to play

- Something watches you from the dark. Each time you look away, it creeps a little closer.
  Let it get too close and it takes you.
- Running straight at it is the only way to drive it back: you'll slow to a halt, it will shriek and vanish,
  then reappear far away.
- Find the lamps and click them repeatedly to light them. A lit lamp's light is safe ground,
  but every lamp you light summons another one of them.

## Controls

| Key | Action |
| --- | --- |
| WASD / Arrows | Move |
| Mouse | Look |
| Left Shift | Run |
| Space | Jump |
| Left Ctrl / C | Crouch (hold, or toggle via `toggleCrouch`) |
| F | Flashlight on/off |
| Left click | Charge the lamp you're looking at (click repeatedly) |
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
