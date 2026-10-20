# Horror Game

Unity scripts for a first-person horror prototype set in a dark forest.

## Scripts

| Script | What it does |
| --- | --- |
| `Assets/Scripts/Player/FirstPersonController.cs` | Walk, run, jump and crouch (CharacterController based), mouse look, head bob. |
| `Assets/Scripts/Player/Flashlight.cs` | Spot light on the camera, toggled with **F**. Slight sway, optional click sound. |
| `Assets/Scripts/World/DarkForestGenerator.cs` | Procedurally builds a dark wooded area: bumpy ground, pines, dead trees, rocks, logs, fog, moonlight and boundary walls. Can spawn the player too. |
| `Assets/Scripts/World/ShadowEntity.cs` | A pitch-black silhouette that materializes in the distance and watches you. Approach it and it vibrates, its eyes widen and you slow to a halt, then it builds to a crescendo and poofs, rematerializing elsewhere with blazing eyes. Spawned by the generator. |
| `Assets/Scripts/Input/HorrorInput.cs` | Input wrapper that works with both the new Input System and the legacy Input Manager. |

## Quick start

1. Create a new 3D project (Built-in or URP), Unity 2022.3 or newer.
2. Copy the `Assets/Scripts` folder into your project.
3. Open any scene that has no player in it (the default SampleScene is fine) and press Play.
   The forest and a player with a flashlight are created automatically.
   To tweak settings, add **DarkForestGenerator** to a GameObject yourself; auto-creation can be
   switched off with `DarkForestGenerator.autoCreateInEmptyScenes`.

Tip: right-click the DarkForestGenerator component header and choose **Generate** to build the forest in edit mode, so you can look around and tweak it. Change `seed` for a different layout.

## Controls

| Key | Action |
| --- | --- |
| WASD / Arrows | Move |
| Mouse | Look |
| Left Shift | Run |
| Space | Jump |
| Left Ctrl / C | Crouch (hold, or toggle via `toggleCrouch`) |
| F | Flashlight on/off |
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
