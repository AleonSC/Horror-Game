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
- **Dev tools (F9)**: lamps lit, field of view, Stick of Truth, spawn overlay, restart the run; Lighting
  tab for the picture, moon, fog and flashlight.
- Saved tuning: **Horror > Open Saved Tuning**; right-click a component > **Clear Tuning** to reset it.
