# Handoff: Horror Game

Notes for picking this project up in a new session. Read this first, then the README for the per-script
overview (the README predates some of the work below).

## The game

A first-person horror game in a procedurally generated, foggy night forest (Unity, everything built in code
at runtime). You light 10 lamps scattered through the forest while being hunted; each lamp lit escalates
things. The owner's vision: **an organic experience of escalating chaos you can't escape, because you're
actively being hunted. No "director" pacing system.** The narrator voice was removed on purpose.

Main threats:
- **Scours** (`ShadowEntity.cs`): black, too-thin silhouettes that creep up when you're not looking. Face
  one down with the flashlight to dismiss it. Some peek from behind trees ("duckers"). No cap on how many.
- **The blind one** (`BlindEntity.cs`): huge and eyeless, hunts by sound (`Noise.cs`), chants, senses the
  drone while it's being flown.
- **The giant** (`WatcherEvent.cs`): hangs in the sky by a chain, drops and walks in after the sky eye
  event, and later sweeps searchlights from its eyes.
- **The flier** (`FlyingEntity.cs`) and **the crawler** (`Crawler.cs`): a head on four neck-arms that leaps
  onto your face. Mash to escape; being latched summons another scour.

Other systems: drone with battery and thermal mode (`Drone.cs`, `DroneCache.cs`), hotbar items
(`Hotbar.cs`, including the dev-only "Stick of Truth" spawner), cabins, watchtower, broken car (sit with Y;
a refuge with the doors shut), campsites, ponds and wildlife, lamp signposts, falling trees, the portal door
to a dark room (`PortalDoor.cs`), and rifts (`RiftEvent.cs`, now spawn-on-demand only).

## Branches and where things stand

| Branch | What it is |
| --- | --- |
| `hdrp` | **The live version.** HDRP plus everything since the switch. Work here. |
| `claude/sharp-fermat-ji4pg6` | The last **URP** version (`76ec9e3`), frozen as a fallback. Don't commit to it unless asked. |

- The owner hasn't decided whether to keep HDRP for good. Merging `hdrp` into the URP branch was offered
  and is on hold. A `urp-final` tag couldn't be pushed (the session's git proxy refuses tags); the owner can
  make it from GitHub's Releases page if wanted.
- The owner's Unity project is a **separate folder** with HDRP installed. If it's checked out on the URP
  branch, it fails to compile with "`Universal` does not exist" errors; the fix is to switch that folder to
  `hdrp` and pull.
- Unity **6.0**, HDRP **17.0.3**, the **Input System** package (`Assets/Scripts/Input/HorrorInput.cs`).
  Unity 6.0 needs `FindObjectsByType<T>(FindObjectsSortMode.None)`; there's no argument-less overload.
- Only `Assets/` is in the repo. Packages and ProjectSettings live on the owner's machine.

## Working with the owner

- They test in Unity and send **Console or Game view screenshots**. Code can't be compiled or run in the
  cloud session, so say plainly that a change is untested, and expect compile errors to come back as
  screenshots.
- Commit to the working branch with a descriptive message (use `git commit -F -` with a heredoc; quotes in
  `-m` broke the shell once), and push. **No pull requests** unless asked.
- When they say "discuss", only discuss, don't change code.
- They like short, plain explanations of what changed and what to look for, and settings they can tweak
  live during Play.

## How the code is built (things to keep doing)

- **Everything is procedural.** Meshes, textures (`ProceduralTextures.cs`), sounds (`ProceduralAudio.cs`)
  and materials are made in code. `DarkForestGenerator` builds the world and adds most systems.
- **Materials**: always through `MaterialFactory` (`Lit`, `LitTextured`, `LitTransparent`, `Unlit`,
  `NoFog`). It makes HDRP/Lit and HDRP/Unlit and calls `HDMaterial.ValidateMaterial`. Use
  `SetDoubleSided` and `SetNormalMapOffset` instead of setting HDRP properties directly.
- **Custom shaders** live in `Assets/Scripts/Resources/` (so they're always in builds), all HDRP HLSL
  using the shared `HorrorHDRP.hlsl` (camera-relative transforms, screen UV, scene depth). They draw in
  the `ForwardOnly` pass and **ignore exposure** (their colours go straight to the screen):
  `Beam`, `Glow`, `Haze`, `MistVolume`, `NightSky`, `SkyEye`, `Smoke`, `Flame`, `Trail`, `Portal`, `Rift`,
  `Throat`, `UnlitNoFog`.
- **Fog-free draw order.** HDRP fogs everything in its opaque stage, background included. Anything meant
  to show through the fog is drawn after it: `MaterialFactory.FogFreeQueue`. Far-off shapes (giant, its
  chains, the landmark tree) use `MaterialFactory.MakeFogAware`: solid, marked in stencil bit 64 so the
  star dome skips them, and they add back the glow of the flashlight and the nearest four lamps in the
  mist in front of them (`_HorrorSpot*` and `_HorrorPoint*` globals from `HdrpAtmosphere`). Without that
  they cut black shapes out of lit fog.
- **`HdrpAtmosphere.cs`** maps the game's own settings onto HDRP every frame. `RenderSettings` fog, ambient
  and camera background become HDRP fog (volumetric), a flat gradient sky and a colour background. It also
  sets fixed exposure (-1.9 EV), no tonemapping, light units (candela, lux for the moon), lamp fog glow,
  and ray tracing (shadows, AO, reflections; GI optional) with the game's effects kept out of the ray
  traced scene. It owns the **Classic look** and **ray tracing** switches (PlayerPrefs; in the pause menu
  options and the dev tools Lighting tab).
- **Tuning**: every game component calls `Tuning.Apply(this)` first thing in `Awake`. Any value changed in
  the Inspector during Play is auto-saved per component *type* (`Editor/TuningAutoSave.cs`) into
  `Resources/HorrorTuning.asset`. Give new components the same `Awake` line.
- **Static state**: any class with statics needs a `ResetStatics()` listed in `StaticReset.cs`.
- **Audio clips**: `public static AudioClip X => x != null ? x : (x = Take(nameof(x)) ?? MakeX());`. Field
  names must not collide (a duplicate `Splash` once broke the build; the new one became `Plop`).
- **The world wraps** (`WorldWrap.cs`): use `WorldWrap.NearestImage` / `Fold` for positions across the
  edge.
- **C# gotcha**: `out var` assigned inside an `&&` short-circuit gives CS0165; declare the variable first.

## Recent work on `hdrp` (newest first)

- **Player character model** (`Player/PlayerCharacter.cs`, `Editor/PlayerCharacterSetup.cs`): the owner's
  model and clips go in `Resources/PlayerCharacter` (Horror > Player Character > Open Settings). PlayerBody
  uses it instead of the built figure, sized to `height`, animated with a PlayableGraph mixer (no Animator
  Controller): idle, walk, run, crouch idle/walk, jump, winded, drone control, seated, with fallbacks for
  empty slots; walk/run play at the player's pace; a remote goes in a humanoid rig's right hand while
  flying the drone. Still only seen by other cameras (layer 29).
- **Tree packs** (`World/TreePack.cs`, `Editor/TreePackSetup.cs`): the owner's tree pack goes in the
  `Resources/TreePack` asset (Horror > Tree Pack > Open Settings; drag the prefabs into Prefabs). The
  generator grows them set up like its own trees (`BuildPackTree`: a "Tree" with a trunk CapsuleCollider,
  a "Sway" pivot holding the prefab), so wind, felling, scour peeking and building keep-outs all work. The
  prefab's own colliders are removed by default. TreeFall cuts packs per material (submesh), only LOD 0,
  finds the bark by material name (bark/trunk/wood/stem) or else by which material hugs the trunk, and
  needs Read/Write meshes (Horror > Tree Pack > Allow Felling); without them a pack tree never falls.
  Horror > Tree Pack > Check lists non-HDRP materials and other problems.
- **Reference theme** (Lighting tab): aims at the owner's reference picture (pale blue-grey fog brighter
  than the shadows, thick patchy mist, muted graded picture, scours with a dark lit body and eyes that
  throw a red pool on the ground). It sets the Lighting values plus Look settings (`LightingSettings.Reference`,
  its `look` text). Scours gained `litBody`, `bodyColor`, `bodySmoothness`, `eyeGlowRange`, `eyeGlowShadows`;
  HdrpAtmosphere's `noTonemapping` became `tonemapMode` (0 none, 1 neutral, 2 ACES). Look settings for
  scours are kept and given to new scours as they spawn (`LookSettings.ApplyTo`).
- **Dev tools rebuilt** (F9; `Player/DevTools.cs`, styled by `Player/DevSkin.cs`): tabs Game, Lighting, Look.
  Game has **Invincible**: the player becomes a moving safe zone (`SafeZones` source), so nothing can take
  them and scours circle round them like a lamp (the giant's catch is skipped too). **Look**
  (`Player/LookSettings.cs`) is a list of fields on the game's components (HdrpAtmosphere, Flashlight,
  GroundMist, Lamp, NightSky) shown as sliders, saved in PlayerPrefs, with **Copy settings** (plain text,
  `Type.field = value` lines, changed ones marked `*`, plus the Lighting tab's JSON) for the owner to paste
  into chat. To make pasted values the defaults, set those fields' initialisers (and the LightingSettings
  theme). Add a setting with one `Add(...)` line in LookSettings. HdrpAtmosphere gained fog anisotropy,
  shadow distance, moon/flashlight shadow resolution and strength, contact shadows, screen-space AO and a
  bloom override.
- **Scours back to the original design**: the black stick figure (bones and knobs, ribs, backward elbows,
  loose legs whose shins melt into the fog, `LegDrag`). The sculpted `ScourBody` meshes below are unused by
  default (still there for saved models that use them).
- **Scours rebuilt, detailed** (`World/ScourBody.cs`): sculpted lofted meshes (starved torso with ribs,
  vertebrae, shoulder blades; tendon-corded neck and limbs; long skull with brow and sockets; jointed clawed
  hands; long feet), dark wet HDRP Lit skin. They stand and walk normally (`LegWalk`); the dragging-leg
  ragdoll is gone. The meshes are registered in `EnemyMeshes` so the model editor can show and save them
  (parts with shape "Mesh"). A previously saved Scour model with "Use in game" overrides this look.
- **Flashlight**: `boost` (x2 on top of the lighting panel's intensity), a real beam-shaped cookie (hot
  centre, spill, reflector rings), less fog glow.
- **Session recorder** (F11; `Player/SessionRecorder.cs`): logs every change that matters (each scour by
  number and its state changes, the blind one/crawlers/fliers/giant, what the blind one hears and the
  script that made the noise, lamps and split times, safe zones, the drone, being caught, breath, storms,
  falling trees, lightning) plus totals (running, walking at full stamina, recovering, standing, crouched,
  safe, drone...). The owner copies it ("Copy everything") and pastes it for analysis; it's also saved to
  `SessionLogs/` next to `Assets` when Play stops. Add lines anywhere with `SessionRecorder.Log`.
- **Model editor** (F10, or Dev Tools > Model editor; `Player/ModelEditor.cs`, `World/EnemyModel.cs`): the
  owner designs enemies from simple shapes on joints (body, head, arms, hips), with eyes, mouth, legs and
  notes, and saves them as JSON in `Assets/Scripts/Resources/EnemyModels/<Enemy>.json` (or copies the JSON
  to paste into chat). A saved **Scour** model with `useInGame` is what new scours are built from
  (`ShadowEntity.BuildFromModel`; its legs, mouth, eye glow and twitching are kept). The other enemies'
  models are designs to be built for real: read their JSON and notes and recreate them in code.
- A scour that catches you drags you backwards through a red-edged black portal (`ScourPortal.cs`), then
  the game-over fade. The wings and the flight to the giant are gone. (`WatcherEvent.ReachFor` is now
  unused.)
- Lamp glow in HDRP fog toned down (`lampFogGlow`). Far shapes add lamp glow, not just the flashlight's.
- Classic look and ray tracing switches.
- The giant is solid again (it had become see-through).
- The flashlight dims when pointed at things close by, and lights the fog less (`Flashlight` "Up close").
- Inspector auto-save tuning.
- Lightning turns the moon's shadows off while it flashes (HDRP allows one shadowed directional light).
- Scour legs: long, with feet, simulated so they drag limp along the ground (`LegDrag` in `ShadowEntity`).
- Scour mouth: a real 3D throat, lips and fangs (`Horror/Throat`).
- A dismissed scour is dragged back through a red-edged portal instead of bursting apart.
- Rifts are no longer a random event (Stick of Truth only).
- HDRP conversion, steps 1 to 4: shaders, materials, fog, sky, exposure, ray tracing.

Before HDRP (also on the URP branch): new beam shader (camera-facing soft shafts), trees and props kept out
of building interiors, enemy lights cast shadows through plank gaps, rift event.

## Open items

1. **Second-camera views** (portal door, rift, drone screen, Polaroid) likely get post-processing applied
   twice, so they look washed out. Turn post off on those cameras. They're also expensive in HDRP.
2. **Light brightness** was converted roughly. Retune lamps, fires, torches, scour glare, searchlight and
   lightning from the owner's screenshots.
3. **HDRP/Unlit materials react to exposure** and may be about 3x too bright (for example the scour burst
   embers). Consider moving them to the game's own `Horror/Unlit No Fog`.
4. **Drone thermal mode** hasn't been seen running under HDRP.
5. **Fog stacking**: the ground mist plus HDRP fog may be thicker than in URP. Both are tunable (see below).
6. **Shadow limits**: every scour's mouth glare casts shadows and scours are uncapped. Cap shadows to the
   nearest few, or raise HDRP's shadow atlas size.
7. **No standalone build** has been tried on HDRP. Ray tracing needs DirectX 12.
8. Decide on merging `hdrp` into the URP branch.

## Where to tune things during Play

- **HDRP Atmosphere**: exposure, tonemapping, fog thickness (Fog Distance Scale; higher = thinner),
  volumetric albedo and distance, lamp fog glow, beam over shapes, ray tracing switches.
- **Ground Mist**: density, height, patchiness, clearing round you and lamps, flashlight and lamp
  strength.
- **Player > Flashlight**: brightness, range, near dim, fog glow.
- **Dev tools (F9)**: Game (invincible, field of view, lamps lit, Stick of Truth, overlay, editors, restart),
  Lighting (themes, picture, moon, fog, flashlight), Look (fine detail, copy and paste).
- Saved tuning: **Horror > Open Saved Tuning**; right-click a component > **Clear Tuning** to reset it.
