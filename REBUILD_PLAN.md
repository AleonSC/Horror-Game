# Rebuild Plan: "Scary Game" (first-person forest horror)

This document tells a fresh Claude Code session how to rebuild this game from nothing, on a sound technical
footing. It was written from the existing prototype (branch `hdrp` of this repo, about 42,000 lines of C#),
which works but grew without a plan. Everything the game does is specified here, with its numbers. The
architecture is new: it fixes the problems the prototype ran into (listed in section 13).

Read all of it before writing code. Build in the milestone order of section 12, and don't move on until a
milestone's checks pass.

---

## 0. Working with the owner

- The owner tests in the Unity Editor on Windows and sends Console or Game view screenshots. A cloud
  session can't run Unity unless CI is set up (section 2.3), so say plainly when a change is untested.
- Short, plain explanations: what changed, what to look for, and where to tune it. No jargon dumps.
- When they say "discuss", only discuss: no code changes.
- They like settings they can tweak live in Play mode, and a way to copy tuned values back to Claude.
- Commit with a descriptive message to the branch you're told to use; no pull requests unless asked.
- The creative goal, in their words: **an organic experience of escalating chaos you can't escape,
  because you're actively being hunted. No "director" pacing system.** Escalation comes from the lamps
  the player lights (section 6.4) and the time they spend out there, never from an invisible AI
  balancing tension.

---

## 1. The game in one page

You wake in a clearing in a foggy, endless night forest. A colossal dead figure hangs by its neck from a
chain in the sky ahead of you. Ten unlit lamp posts are scattered through the woods. Lighting them (by
cranking each one with repeated clicks) makes safe pools of light, unlocks a door that stands alone in the
forest, and is the only way out. But every lamp lit makes things worse: more hunters come, new and bigger
ones arrive at set lamp counts, the weather turns, and the hanging giant wakes.

**Main threat, the scours:** too-thin black figures with burning red eyes that creep closer whenever you
look away and take you if they reach you. Facing one down with your flashlight dismisses it. There are
more of them every minute and with every lamp.

**Other hunters:** the blind one (a huge eyeless thing that hunts by sound), crawlers (heads on four
spider-arms that leap onto your face), the flier (a vast bird whose searchlight eye calls every scour to
you), and the giant (which drops from the sky after the 5th lamp and walks toward you, shaking the ground).

**Tools:** a flashlight on a battery, batteries, torches, lighters, flare guns, glowsticks, an instant
camera, a key fob for an abandoned car, a hand-drawn map, a drone, and snacks that build stamina. Places
to shelter: shacks, a cabin, tents, the car, lit lamps, a watchtower with floodlights.

**Pillars:**
1. **You can't fight, only see.** Light and sight are the only weapons. The darkness is the threat.
2. **The forest is alive.** Wind, wildlife, sounds and weather all react to you and to what's hunting you.
3. **Escalation you cause.** Each lamp lit is progress and punishment at once.
4. **Everything is readable.** Every threat has a tell (eyes in the dark, a sound, a silence) and a counter.

---

## 2. Technical foundation (do this first, before any gameplay)

### 2.1 Engine and pipeline: decided once

- **Unity 6** (the project is on **6000.6.6f1**, see `ProjectSettings/ProjectVersion.txt`), **HDRP** from the first commit (the prototype switched from URP to HDRP halfway
  through, which rewrote every shader and material and caused weeks of visual bugs). DirectX 12 on
  Windows. Ray tracing is an optional extra, never assumed.
- **Input System package** only (no legacy input fallback).
- **Linear colour space**, fixed exposure, no auto exposure (the game lives in near-black; auto exposure
  ruins it).
- Packages to include from day one: HDRP, Input System, Cinemachine (main menu film), Burst +
  Collections + Mathematics (generation and instancing), Test Framework. Optional: Addressables (if asset
  packs grow).

### 2.2 The whole project in git

Commit `Assets/`, `Packages/manifest.json`, `Packages/packages-lock.json` and **all of `ProjectSettings/`**
(the HDRP asset, quality levels, tags and layers, input actions, physics layers, graphics settings). The
prototype committed only `Assets/`, so every machine had different packages and settings and errors kept
coming from that.

Use a Unity `.gitignore` (Library, Temp, Logs, UserSettings, Builds) and Git LFS for binaries (textures,
audio, models, `.unitypackage`).

### 2.3 CI from day one

A GitHub Actions workflow using GameCI (`game-ci/unity-test-runner` and `game-ci/unity-builder`) with the
owner's Unity licence as repository secrets:

- On every push: compile, run edit-mode and play-mode tests, and make a Windows build artifact.
- A headless "smoke play" test: load the main scene, generate the world with a fixed seed, simulate 60
  seconds, assert no errors were logged and frame time stayed under budget.

This is what lets a cloud Claude session verify its own work instead of waiting on screenshots.

### 2.4 Project layout and assemblies

```
Assets/
  Game/
    Core/            (Game.Core.asmdef)       services, events, run state, save, config base types
    World/           (Game.World.asmdef)      generation pipeline, terrain, vegetation, structures
    Rendering/       (Game.Rendering.asmdef)  atmosphere, shaders, materials, instanced renderers
    Audio/           (Game.Audio.asmdef)      mixer control, procedural synthesis, music
    Player/          (Game.Player.asmdef)     controller, flashlight, hotbar, interaction
    Entities/        (Game.Entities.asmdef)   AI framework and each hunter
    Events/          (Game.Events.asmdef)     escalation, set pieces, ambient events
    UI/              (Game.UI.asmdef)         menus, HUD, narration, inventory
    DevTools/        (Game.DevTools.asmdef)   dev window, recorder, model editor (excluded from release builds)
    Editor/          (Game.Editor.asmdef)     import tools, build setup, config inspectors
    Tests/           (EditMode + PlayMode asmdefs)
  Config/            ScriptableObject assets (all tuning numbers live here)
  Art/               imported packs, textures, models (LFS)
  Audio/             recorded clips if any, the AudioMixer asset
  Scenes/            Boot.unity, Forest.unity, TestScenes/
  Shaders/           all custom shaders (HLSL, HDRP)
```

Assembly definitions keep compile times down and stop systems from reaching into each other.

### 2.5 Architecture rules

1. **Data lives in ScriptableObject configs**, not in MonoBehaviour field defaults and not in PlayerPrefs.
   One config asset per system (`ScourConfig`, `LampConfig`, `WorldGenConfig`, `LookConfig`...). They're
   versioned in git, editable in Play mode (changes to assets persist), and diffable. Player preferences
   (volume, sensitivity, graphics choices) are the only thing in PlayerPrefs or a settings file.
   (The prototype had three overlapping hacks for this: a "Tuning" auto-save, PlayerPrefs lighting, and
   copy-paste "Look" settings. Don't repeat that.)
2. **No static mutable state.** Use a small service locator or a `GameRun` object that owns the run's
   state and is created fresh each run. If something must be static, it's reset by
   `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` in its own class. (The prototype needed a
   central `StaticReset` listing 60 classes.)
3. **An event bus for game events** (`LampLit`, `PlayerCaught`, `EntitySpawned`, `NoiseEmitted`,
   `EnteredShelter`, `RunReset`...). Systems react to events instead of polling each other. The session
   recorder, music, narration and escalation all subscribe.
4. **Small components.** No class over ~500 lines. An entity is made of parts (perception, movement,
   state machine, presentation, audio) rather than one 2,200-line class.
5. **Scenes and prefabs for anything hand-authored** (player, entities, props, UI). Procedural generation
   only for the world layout and for things that must vary per run. The prototype built everything,
   including the player and every prop, from code at runtime, which made art iteration slow.
6. **Deterministic, seeded generation** (section 3), so a bad layout can be reproduced exactly.
7. **Engine features first:** AudioMixer snapshots for sound states, HDRP Volumes for looks, Cinemachine
   for cameras, the Animation system (or Playables) for creatures, LOD Groups for meshes.

### 2.6 Performance budget (set before content)

Target: 60 fps at 1080p on a mid-range GPU (RTX 3060 class), 16.6 ms.

| Area | Budget |
| --- | --- |
| Draw calls (batches) | < 3,000 |
| Shadowed lights on screen | moon + flashlight + at most 4 others (pick the nearest; HDRP's shadow atlas has limits) |
| Grass and small foliage | GPU instanced (`Graphics.RenderMeshInstanced` / `RenderMeshIndirect`), wind in the vertex shader, no GameObject per clump |
| Trees | LOD Group: LOD0 mesh, LOD1 simplified, LOD2 impostor/billboard; wind in the shader |
| Simulated creatures | only within ~70 m of the player; pooled |
| Audio voices | < 48 real voices; pooled one-shot sources |
| Physics | layers set so foliage, effects and pickups don't collide with each other |

Profile in a build every milestone. The prototype had 10,000+ grass GameObjects swayed by script.

### 2.7 Layers

| # | Layer | Use |
| --- | --- | --- |
| 0 | Default | world |
| 8 | Ground | terrain (raycasts for placement) |
| 9 | Trees | trunks (capsule colliders) |
| 10 | Props | structures, rocks, logs |
| 11 | Pickups | interactables (trigger colliders) |
| 12 | Entities | hunters (no physical collision with the player) |
| 13 | Effects | no collision, excluded from ray tracing |
| 29 | PlayerBody | the player's own model: hidden from the main camera, seen by the drone and portals |
| 30 | PortalFaces | doorway render surfaces |
| 31 | Thermal | the drone's thermal view |

---

## 3. World generation (a staged, seeded pipeline)

### 3.1 Shape of the pipeline

Generation runs as stages over plain data (`WorldPlan`, structs and arrays), then a final stage turns
the plan into GameObjects and instanced batches. Each stage is a class with `Run(WorldPlan plan, Rng rng)`,
can be unit tested on its own, and can be drawn as gizmos in the editor.

```
1  Terrain        heightfield: rolling waves + hills
2  Layout         the important places first: start clearing, giant bearing, lamps, lamp clearings,
                  structures, the door, glades, ponds
3  Sightlines     guarantee views that matter (the giant from the start; sky above every lamp)
4  Paths          trails between lamps, paths round lamps and up to structures
5  Vegetation     trees, bushes, grass, tall grass, flowers (density maps respecting everything above)
6  Props          rocks, boulders, logs, puddles, webs, vines, signposts
7  Pickups        batteries, items in structures and camps
8  Build          GameObjects for structures/props, instanced batches for vegetation
```

The prototype scattered trees first and patched problems after (cutting trees out round lamps, moving
the giant into view). Placing the important things first, then growing the forest round them, makes every
run read the way it's designed.

### 3.2 The map

- **320 m x 320 m**, **wrapping**: walk off one edge and you come back in on the other, seamlessly. Terrain
  is built from periodic functions so it tiles. Every registered static object is drawn at whichever
  copy of its position is nearest the player; when the player crosses an edge, they and everything near
  them shift by one map width (only things half a map away visibly jump, deep in the fog). All distance
  maths uses the wrapped difference.
- **Seed**: an int on `WorldGenConfig`; "randomize each run" toggle. Log the seed in the session recorder.

### 3.3 Terrain

- Height = sum of 8 broad sine waves (wavelength >= 40 m, amplitude 0.35 x bumpiness 1.5 each, random
  phase/direction using whole cycles across the map so it tiles) + 12 small ones (wavelength >= 10 m,
  amplitude 0.06 x 1.5).
- **Hills:** 4, height 7-13 m, radius 30-50 m, half stretched 1.4-2.2x into ridges along a random axis.
  Profile: flat top for the inner 15% then smoothstep down; outline wobbles +-15% with angle (Perlin).
  Keep them at least `startClearing + 1.3 x radius` from the start and `0.8 x (r1 + r2)` from each other.
  Hilltops get few trees (density x (1 - 0.75 x top^2)) so they're lookouts over the canopy.
- **Ponds:** 3, radius 7-13 m, pressed in as smooth bowls deep enough that the middle is at least 0.9 m
  under water; the water level is the lowest point of the rim minus 0.1 m. Away from the start and each
  other. **Puddles:** 40 on flat ground.
- Ground mesh: 1 m vertex spacing, chunked (e.g. 32 x 32 m chunks) for culling; a proper ground material
  (texture + normal, ideally scanned dirt/leaf litter) with detail decals for paths.

### 3.4 Layout rules (stage 2), in this order

1. **Start clearing** at the map centre: radius **28 m**, ragged edge (radius x 0.9-1.15 by angle), trees
   thinning over the next 40%. It must be the **largest clearing on the map**.
2. **Player facing**: a random yaw. **The giant's bearing = that yaw +-12 degrees**, so it's in view from
   the first frame.
3. **Lamps: 10.** At least **55 m** from the start, at least **25 m** apart, not within 7 m of a structure.
   Each gets a **clearing of radius 12 m** (ragged +-30%) with **tall grass** (kept 4 m back from the post)
   and the sky open above it.
4. **Structures:** 3 shacks (one built as the big cabin with rooms), at least 60 m apart; 3 campsites,
   at least 50 m apart; 1 watchtower; 1 broken car; the landmark tree (giant pine, 7x scale). Each with a
   keep-out box nothing grows into.
5. **The lone door**: at least **65 m** from the start, on the flattest ground found (600 attempts), more
   than 25 m from structures, well inside the map (75 m from the edge, because its dark room is built far
   below it).
6. **Glades: 14**, radius 7-16 m, ragged edges, at least `28 + r + 6` m from the start, `r1 + r2 + 10` m
   apart, not on hilltops. No trees in the middle; dense **tall grass** (0.55 clumps per m^2, 1-1.5 m
   tall), thinning to the edge.

### 3.5 Sightlines (stage 3)

- From the start position along the giant's bearing, at elevations 10-38 degrees: no tree may block it
  within the start clearing; thin the clearing's far edge on that bearing if needed.
- Above every lamp: no canopy within a 55-degree cone up from the post.
- Validate with a test: generate 50 seeds and assert the giant is visible from the start in all of them.

### 3.6 Vegetation (stage 5)

- **Trees: 2,800**, minimum spacing 2.2 m (grid-hashed Poisson-ish placement), patchy density from Perlin
  noise (scale 0.05), 30% dead/bare trees, 3% "old giants" (1.7-2.5x size with room cleared round them),
  every tree at 0.85x size, **12% of planned spots left empty** (natural gaps). Trees from a pack (section
  10.2) or procedural pines (tapered flared trunk with root lobes, drooping needle boughs as real
  geometry) and dead trees (forked bare branches).
- Each tree: trunk capsule collider on the root, a "sway" pivot for wind (or shader wind), LOD Group.
- **Bushes: 1,400**, 0.45-1.4 m, three kinds (mound, upright, leaning), leaves as real folded geometry in
  two shades; no colliders: they lean away and rustle as you push through (and make a Noise).
- **Grass: 10,000 clumps** gathered into meadows by noise (0.03), plus 4-10 tufts round each tree's foot.
  Instanced, wind in the shader.
- **Vines:** on 4% of trees, Verlet ropes swinging in the wind, pushed by the player and entities; only
  simulated near the camera.
- **Spider webs** between close trees at head height, nearly invisible until lit; walking into one tears
  it across your face.

### 3.7 Props and pickups (stages 6-7)

- Rocks 480 (0.3-1.6 m, sunk into the ground), boulders 40 (2.5-6 m, half buried), fallen logs 140.
- **Batteries: 30** (x difficulty), at least 8 m from the start, 12 m apart, 1.2 m from trees; each
  refills the flashlight by 10%; a pulsing green light helps find them.
- Shack table: torch + lighter, 50% a battery, 20% the car's key fob, 30% a radio. Wall torch to light.
- Campsite: tent (with lantern and a **snack**), burnt-out campfire (relightable), backpack with a flare
  gun, 10 glowsticks, 2 batteries, 20% key fob, 30% radio, smoke column visible from far off.
- Car: 2 batteries in the glove box, radio on the back seat, camera in the trunk.
- Drone: at whichever of cabin store room / watchtower deck / car back seat the player reaches first.
- Lamp extras: worn trodden ring and trails (HDRP decals), garden (tall grass, fireflies when lit),
  a signpost whose arrow boards point to the nearest dark lamps (a notch per 30 m), and old trails
  linking all the lamps (minimum spanning tree plus 2 extra loops), wandering round trunks and petering
  out in stretches.

---

## 4. Rendering and the look

### 4.1 Target look

Reference: a foggy night forest photo the owner supplied. A pale blue-grey fog brighter than the
shadows (trees stand dark against it, fading with distance), thick low ground mist, muted colour, soft
contrast, a little vignette and grain, red eyes and red glow the only saturated colour. Build a **look
test scene** in milestone 1 and match it before content.

### 4.2 HDRP setup (one global Volume profile, `Config/Look/ForestNight.asset`)

- **Exposure:** Fixed, EV100 **-1.9** (tuned so light intensities read as intended).
- **Tonemapping:** None (ACES crushes the dark end); offer Neutral as an option.
- **Fog:** colour from the look config; mean free path ~0.85 / density (density 0.05 -> about 17 m);
  base height high (400 m) so it's uniform through the forest; **volumetric fog on**, albedo 0.55,
  depth extent at least the camera's far clip (else a hard seam where volumetric meets plain fog);
  anisotropy configurable.
- **Sky:** a gradient sky for ambient only; the visible sky is a custom star/cloud dome (stencil-masked so
  far shapes can hide it).
- **Shadows:** max distance configurable (~150-500 m); contact shadows optional; one shadowed directional
  light only (the moon; lightning flashes turn the moon's shadows off briefly).
- **Lights:** candela for punctual lights, lux for the moon; point lights' volumetric dimmer low (0.3) or
  the mist round every lamp blazes.
- **Post:** colour adjustments, vignette, film grain, optional motion blur and depth of field (used when
  waking groggy). Render-texture cameras (portal views, drone screen, polaroid) get post-processing
  turned off on their own frame settings (otherwise it's applied twice and they look washed out).
- **Classic look** toggle (no volumetrics/AO/reflections) and **ray tracing** toggle (shadows, AO,
  reflections; GI optional) in the options, saved; game effects (beams, glows, mist, sky, smoke, portals)
  excluded from the ray tracing acceleration structure.

### 4.3 Owner's chosen default lighting (keep these)

| Setting | Value |
| --- | --- |
| Moon colour / brightness / height | (0.45, 0.55, 0.80) / 0.25 lux-equivalent / 35 deg, soft shadows |
| Ambient | (0.06, 0.07, 0.09) x 1.0 |
| Fog colour / density | (0.02, 0.025, 0.035) / 0.05 (the "Reference" theme instead: (0.16, 0.17, 0.20) / 0.06) |
| Stars / clouds | 1.0 / 0.5 |
| Picture | post exposure **+1.63**, contrast **35**, saturation **40**, vignette 0, grain 0 |
| Flashlight | intensity **20.4** (x boost 2), range **24 m** (light reaches 2.5x further, fading), beam **31 deg**, crisp edge (feather 0), shadows on |

Also provide alternative themes: Darker, Pitch Black, Blood Moon, Foggy, Sickly Green, Dusk, Bright
(testing), Reference.

### 4.4 Custom shaders (HDRP, HLSL, `ForwardOnly` pass, a shared include)

Write a shared `Horror.hlsl` (camera-relative transforms, screen UV, scene depth). Shaders:

| Shader | Use |
| --- | --- |
| Beam | soft camera-facing light shafts (searchlights, floodlights, scour glare) |
| Glow | additive soft glows (eyes, bulbs, embers) |
| Haze | far shapes in the fog (the giant, its chains, the landmark tree): solid, **fog-aware** (blends to fog by distance itself, adds back the flashlight's and nearest lamps' glow in the mist in front, writes stencil so the sky skips them) |
| MistVolume | the ground mist: a box round the player raymarched with 3D noise hugging the ground, reading a small height/density map and the depth buffer; lit by the flashlight cone and nearby lamps |
| NightSky | star and cloud dome, stencil-tested |
| SkyEye | the giant red eye in the sky (opening, slit pupil, saccades) |
| Smoke, Flame, Trail | campfire smoke, torch/lighter fire, flare trails |
| Portal, Rift | doorway render textures clipped to a plane, ragged burning-edged holes |
| Throat | the scour's mouth (vertex colours, alpha = glow) |
| UnlitNoFog | flat colours that must read through fog (eyes far off) |

**Draw order lesson:** HDRP fogs everything in its opaque pass. Anything meant to show *through* the fog
(sky, far silhouettes, eyes far off) must be drawn after it, in the transparent queue, with its own fog
handling. Plan render queues explicitly: sky 2950 (Transparent-50), fog-aware far shapes 2910+, solid
fog-ignoring things 2990.

### 4.5 Materials

- Prefer authored materials (HDRP/Lit) in `Art/`. For runtime-made materials, one `MaterialFactory` that
  creates HDRP/Lit variants and always calls `HDMaterial.ValidateMaterial`.
- Keep a set of reference materials in `Resources/ShaderRefs` covering every shader and keyword
  combination created at runtime (lit, lit + normal, double-sided, cut-out, transparent, unlit, decal),
  so builds don't strip them.
- Imported packs with built-in pipeline materials (Standard, Tree Creator `Hidden/Nature/...`): convert in
  the editor where possible; at runtime, detect any shader with no HDRP pass (no `GBuffer`, `Forward` or
  `ForwardOnly` LightMode) and swap in an HDRP/Lit stand-in (texture + tint; leaves cut out, double-sided
  with normal mode None so undersides aren't black, brightness multiplier).

### 4.6 The flashlight (a hero feature)

- Spot light from the right hand (offset 0.25, -0.2, 0.2 from the camera), trailing the view (sway speed
  5.5, max 14 degrees behind, hand lag 14).
- Cookie: hot centre, softer spill, faint reflector rings, a darker ring round the hot spot, lens noise
  (256 px, generated).
- **Spill:** a second, wide (85 degree), faint (8%), shadowless spot on the same transform; lights the fog
  at 30% of the beam's rate.
- **Bounce:** where the beam lands within 12 m, a soft point light (range 7 m) placed 0.5 m off the
  surface and partway back toward you, intensity = beam x 0.025 x distance fade, tinted by the surface
  colour. No volumetric glow.
- **Fade:** light range = 2.5 x Range so it dies away gradually.
- **Up close:** pointed at something within 4 m it dims to 35% (smoothed).
- Volumetric dimmer 0.22 (beams in the mist without whiting it out).
- Battery: 150 s full life (x difficulty), drains while on; below 20% it dims to 45% and flickers more and
  more; empty = won't turn on. Moths gather after about a minute on and slowly drain it.

---

## 5. Audio

### 5.1 Mixer (an AudioMixer asset, not per-script volume hacks)

Groups: `Master > {Music, UI, Effects > {Player, World > {Outside, Inside}, Entities, Stingers}}` (Effects
gathers everything but music and UI so one volume slider covers it).

Snapshots, blended by a single `AudioStateController`:

| Snapshot | When | Effect |
| --- | --- | --- |
| Outdoors | default | all on |
| Sheltered | inside a shack, cabin, tent (head under the canvas), or seated in the car | Outside -80 dB over 0.6 s (wind, rain, thunder, ambience, giant), Entities low-passed to ~380 Hz and -12 dB, Player/Inside untouched |
| DarkRoom | through the lone door | everything outside silent, Player with a big reverb |
| Dread | a visible entity within ~7 m | World ducks to ~10%, slow breathing behind your head |
| Hush | something close enough to take you | everything but a heartbeat ducks almost out |
| Paused | pause menu | all paused |

The prototype wired "room quiet" and "shelter quiet" multipliers into a dozen scripts by hand. With
snapshots, a new sound only needs the right mixer group.

### 5.2 Sound sources

- **Procedural synthesis** is allowed and good (the prototype synthesizes every sound in code, on a
  background thread at startup, so it ships without audio files). Keep that system (`ProceduralAudio`
  with one generator per clip, cached), but every playing site takes an optional `AudioClip` override so
  recorded sounds can replace them.
- Pool one-shot sources. 3D sounds use custom rolloff curves (quiet at range; the forest should feel
  big).
- Sounds that matter for gameplay emit a **Noise** event (position + radius) that hearing AI uses
  (section 6.6).

### 5.3 Music

- `MusicManager` with moods: Calm, Intro, Tension (entity within range), Danger (one on you), Watcher (eye
  open / giant walking), GameOver. Playlists per mood from `Resources/Music/<Mood>/` or config; crossfade
  fast toward worse, slow toward calm; fall back down the chain if a mood has no tracks.
- **Boss themes**, procedurally synthesized and scored to each entrance (they can stay procedural):
  - Blind one: sub-bass drone, a drum hit at 3.5 s as it breaks out of the ground, a roar with a
    dissonant brass chord (D, Eb, A, G#) at 7.6 s, two stamps at 13.1 s, then a limping hunt rhythm.
  - Giant: a huge landing hit, a low brass cluster (D, Eb, A over D), chain shriek, a swell to the
    shockwave at 2.5 s, then a 66 bpm march of drums and pulsing strings.
  - Flier: a cold high choir, a reversed swell into a hit as it passes overhead at 6.85 s, a brass stab
    as it climbs, a bell as its eye opens at 12 s, then a 6/8 string ostinato in E phrygian.

### 5.4 Ambience

A low drone bed, a tension layer that swells near entities, random distant noises (twig snaps, moans,
rustles), wind with gusts, leaf rustle, tree creaks in strong gusts, owl calls, deer, frogs at ponds (they
fall silent when something's near: the silence is the warning).

---

## 6. Gameplay systems

### 6.1 Player

| Value | Default |
| --- | --- |
| Walk / run / crouch speed | 3 / 6 / 1.5 m/s |
| Acceleration / air control | 12 / 0.3 |
| Jump height / gravity | 1.1 m / -20 |
| Standing / crouch height | 1.8 / 1.0 m (eye 0.15 below the top) |
| Sprint time (full stamina) | 9 s (x (1 + snack bonus)) |
| Recover time | 7 s standing (walking 1.67x slower), +5% per lamp lit |
| Winded | run it out and you can't run until stamina is back to 35%; you pant (audible to the blind one close by) |
| Head bob | 1.8 Hz, 0.05 m |
| FOV | 60 (dev option 40-100) |

Also: ladders (climb by look direction), wading slows you in ponds, breath fog after long sprints, faint
footsteps (louder running, near silent crouching, splashes in water), a groggy wake-up (blur, slow) after
being caught, and a "dream stretch" effect (FOV pulls into a long hallway and movement gets nowhere)
used by the giant's eye.

### 6.2 Interaction

- Crosshair; aim-assisted targeting (nearest interactable to the line of sight within reach, with extra
  leeway for small items); things inside a shack can only be taken from inside it.
- **Left click**: crank lamps, pick up, switches, eat. **E**: doors only (so frantic clicking never swings
  a door). **F**: use the held item.
- Prompt style: spaced small capitals between diamonds on a soft dark band, with a mouse/E key glyph.
- Pickups get a faint halo when you're within a couple of metres.

### 6.3 Hotbar and items

Eight slots; start with only the flashlight. Number keys or wheel to hold; F uses; Tab opens the
inventory (drag to rearrange, a page per item).

| Item | Behaviour |
| --- | --- |
| Flashlight | section 4.6 |
| Batteries | stack in one slot; F inserts one (+10%) |
| Torch | lighting costs 10% lighter fuel; burns 120 s; 7 m safe circle; gone when burnt out |
| Lighter | 60 s of fuel; 3.5 m safe circle |
| Flare gun | one flare: fired into the sky it bursts at the top of its arc, floods the forest with red light and resets every entity's hunt; fired low it's wasted |
| Glowsticks | 10 per bundle; crack and toss 3 m; glow for minutes; only a light (entities ignore them) |
| Camera | 3 shots; flash drives every entity in frame (40 m, line of sight) away for 30 s; ejects a developing polaroid of the shot |
| Key fob | the car chirps and flashes wherever it is (things nearby hear it) |
| Map | hand-drawn sketch of the lamp trails, lamps inked in when lit, no "you are here" (from the watchtower) |
| Drone | set it down, fly it from the remote (WASD, mouse, Space/Ctrl, Shift): view through its camera with a lamp and a thermal mode; you stand blind and deaf; limited signal range; motors are a Noise (the blind one comes); battery; drops if it dies |
| Snack (eaten on the spot) | in each tent: +25% sprint time for the run, stamina refilled; the stamina bar grows to show it |

### 6.4 Lamps and escalation (the spine of the game)

**A lamp:** click repeatedly to fill its meter (+6% per click x difficulty; drains at 8%/s after 1.5 s
idle). Full = lit: a 13 m safe zone, warm light, fireflies, its trodden trail. An unlit lamp blinks a red
marker on top (seen through fog, fading 10-25 m) and buzzes faintly (heard to 40 m). Lit lamps stay lit
(optional energy/cooldown mode exists: flicker below 35%, 30 s cooldown with blue sparks).

**Escalation by lamps lit (TimesLit):**

| Lamps | What happens |
| --- | --- |
| each | one more scour joins; narration for some counts; the giant's chain jolts |
| 2 | crawlers start (4 round the player); narration "Have you seen the Scours? They really don't like light. Use it." |
| 3 | the blind one arrives (its storm and theme); narration about "the Auscult" |
| 5 | the sky eye opens and the giant falls and walks; rain heavier; frantic narration ("NO IT IS TOO LATE!!!" ... "FIND THE DOOR!!!") |
| 7 | the flier arrives; rain heavier again; narration "I do not know of this entity..." |
| 8 | tornadoes on the horizon |
| 10 | the lone door unlocks (a heavy clunk heard everywhere); the giant's eyes become searchlights that track you and a crowd of 32 scours circles the light's edge |

**Also over time:** one extra scour every minute for the first ten minutes (`ScourClock`, x difficulty).
A falling tree near the player every 45-90 s (first at 30 s). Lightning every 15-45 s. A scream from the
giant's direction as the intro ends.

### 6.5 Safe zones and shelters

- **Safe zone** sources: lit lamps (13 m), held lit torch (7 m) / lighter (3.5 m), car headlight beams
  (shaped, 16 m long), watchtower floodlights (45 m while on), refuges (shacks, cabin, tents: 5 m), and
  the dev "invincible" spot. Inside one you can't be taken; scours won't enter; they gather and circle
  just outside, more the longer you stay.
- **Shelter** (audio state, section 5.1): shack/cabin interior, tent canvas, car seat.
- **Watchtower:** very tall lattice tower; climb the ladder; the button lights floodlights for 60 s (then
  a 120 s recharge): every entity's hunt resets, the area is safe, the giant is driven back to the start.
- **Car:** sit in it (seat), headlights from a switch inside (120 s of battery), four doors, glove box,
  trunk, blood everywhere.
- **Lone door:** locked until 10 lamps; through it a vast black pillared room built far below the world,
  with portal views both ways; the forest's sound fades to an echoing silence; entities can't follow.

### 6.6 Entity framework

One reusable AI framework:

- **Perception:** sight (is the player looking at me: view angle; is the flashlight on me: cone test
  with occlusion), hearing (subscribe to Noise events: position + radius, scaled by difficulty), and a
  "dread" radius.
- **Navigation:** the forest has no navmesh-friendly geometry (thousands of trunks), so use steering with
  local avoidance and "settle" (step round trunks and rocks), plus custom rules (never enter safe zones;
  the blind one barges through trees and knocks them down).
- **State machine** per entity, with states exposed for the session recorder.
- **Presentation** separate from logic: model (prefab or the model-editor JSON), animation, eyes,
  audio.

### 6.7 The scours (main threat)

A 2.4 m, too-thin black stick figure: hunched spine with knobbly vertebrae, curling ribs, collarbones,
arms hanging past the knees with backward-bending elbows, long curling fingers, a small too-tall head on
a long forward-jutting neck, red glowing eyes, and legs that hang loose from hips and knees with shins
that thin and melt into the fog above the ground (they swing and trail as it moves). 25% have one or
two extra pairs of thinner arms out of the ribs. Body: flat unlit black (optionally a dark lit body).
Mouth: a 3D throat with lips and fangs that drops open and glares red light (beam and pool on the
ground) as it nears.

Behaviour (numbers are defaults):

- **Appears** 22-32 m away within 70 degrees of where you're facing, materializing (1.2 s); half are
  "duckers" that peek from behind trunks and snap back when looked at.
- **Creeps** while you're not looking (within 45 degrees of your view counts as looking): every 3-6 s
  (faster with aggression) it steps 3 m closer. With your flashlight off, looking doesn't hold it: it
  creeps even while watched. A creep that would bring it within 3 m takes you.
- **Approached in the dark** (within 14 m, flashlight not on it): it grows (to 1.8x) and advances at 0.75 m/s
  with a pounding cue. Reaching you, it takes you.
- **Faced down**: walk at it with the beam on it (within 14 m): its eyes turn blue, it vibrates, you slow
  to a stop 2.5 m away; it builds to a crescendo (2.5 s, 15% faster per lamp, min 0.8 s, x difficulty)
  and is dismissed: drawn back through a ragged red-edged black portal that opens behind it. Costs 10%
  battery. It comes back 7-12 s later somewhere new.
- **Provoked**: flash the light on/off at it 4 times within 2.2 s and it shrieks and charges your face at
  22 m/s, vanishes, and returns sooner and angrier (aggression up to 4).
- **Safe zones**: can't enter; circles at the edge (+1.5 m) while unwatched. A lamp lit right next to it
  drives it off.
- **Taking you**: it closes in (graspTime 1.4 s), a portal tears open behind it (1 s), it drags you back
  through (2.8 s), the screen fades (4 s). Game over (section 6.12).
- Eyes: very bright on appearing, dim to a glint; eyeshine flares in the flashlight beam (warm); burn
  hot as a hunt nears its end.
- Count: 1 at the start (after 60 s of calm), +1 per lamp, +1 per minute for 10 minutes. No cap.

### 6.8 The blind one ("The Auscult", 3rd lamp)

7.4 m tall, hunched, long-armed, a smooth eyeless head with a split mouth. Enters 45 m away under a
storm (a low churning black cloud with red lightning forking to the ground), breaking out of the earth
with its theme. Roams within 40 m of you at 1.5 m/s, pausing to cock its head and click. Hears Noise
events: running carries far, walking 5 m, close 4 m, crouching barely, x difficulty. On a noise it rushes
(5.2 m/s) to the spot and searches (5 s, then 45 s within 10 m). Within 2.6 m it takes you (it leans its
face down to yours; a hard cut). Lights stop it. It shoulders through trees and knocks them down.
Rocks, boulders and buildings it steps round. If you get 90 m away for 60 s it catches up to 35 m. It
senses the drone in flight from 220 m. A radio playing pulls it from anywhere; it smashes the radio.

### 6.9 Crawlers (from the 2nd lamp)

A head with four long thin arms from its neck stump, crawling like a spider, lying low in the mist. Four
kept 14-45 m round the player (moved if 70 m away). Eyes shine in the beam from several metres; it clicks
close up. Come within 1.8 m walking (2.5 m running) and it leaps (0.35 s) onto your face: blind, can't
move, it shrieks (a Noise 30 m: the blind one hears), mash Space 8 times (or it lets go after 6 s). Being
latched summons another scour. Crouch past it and it doesn't notice. Avoids lights.

### 6.10 The flier (7th lamp)

A vast ragged black bird (~40 m wingspan) with one searchlight eye. Entrance: a cry, a long dive straight
over you low enough to shake the ground and tear leaves up, a climb, then the eye lights. It patrols at
48 m altitude, circling stretches of forest (34 m circles, 100 m patrol) sweeping its beam (9 degree
half-angle). For the first 15 s its beam avoids you. If the beam passes within 9 m it snaps onto you and
holds until you break line of sight (trees, roofs, safe light) for 4 s. Standing still in it is safe;
moving in it for 5 s total makes it screech, the light turns red, and every scour comes at once. It never
takes you. The watchtower floodlights drive it away.

### 6.11 The giant and the eye (5th lamp; present from the start)

- **Hanging** from the start: 660 m tall, 2,100 m away along the giant's bearing, neck at 38 degrees above
  the horizon and feet at 10, turning slowly on its chain (45 s), head lolled; 4 empty chains elsewhere
  round the horizon. Drawn with the fog-aware Haze shader, far away but always a clear silhouette; kept
  at least 45 m apparent height. The first time you have a clear view (after the intro) your view is
  gently drawn up to it once. Each lamp lit makes the chain jolt.
- **5th lamp:** a giant red eye opens high in the sky above it (68 degrees elevation; open 4 s, stares
  11 s, closes 3 s); the view is drawn up to it; after 3 s the dream hallway takes hold (2.5 s ramp: FOV
  widens to 100, running gets nowhere); the world turns red (fog (0.11, 0.015, 0.015)). As it closes, the
  chain snaps like thunder; the giant falls (4 s), lands with a boom (its theme starts), a shockwave
  rolls out (2.5 s later, 220 m/s) whipping trees and grass over and blowing leaves, and it walks toward
  you at 7.5 m/s (12 s strides), stopping now and then (3-6 s) to scan, each step a shockwave and a camera
  shake growing as it nears. Within 135 m it takes you. The floodlights drive it back to the horizon.
- **10th lamp:** searchlights from its eyes track you (radius 9 m, 1.05 tracking), and a crowd of 32
  scours circles the edge of the light.

### 6.12 Being caught, restarting

- **Caught:** fade to black, a message, then you wake (groggy) by the last lamp you lit (or the start).
  Progress is kept (lamps stay lit, inventory kept); the scours melt away and come back after a while;
  the blind one and flier leave for a bit; the giant returns to the horizon; battery topped up to a
  minimum.
- **Restart (pause menu):** everything back to the start: lamps out, items and pickups respawned,
  snacks back, stamina bonus gone, entities removed, the giant re-hung ahead of where you start.

### 6.13 Ambient life and events

- **Wildlife** (always round the player, recycled): 40 beetles, 6 rats (flee at 5 m), moths at lit lamps,
  bat flocks every 15-45 s, crow flocks every 20-60 s, 45 owls on branches (white eyes, heads track you
  within 45 m, call every 25-70 s), 6 deer (look up at 24 m, gallop off at 14 m, a spooked deer sets off
  the herd), frogs and glowing mushrooms at ponds, fireflies at lit lamps and the door garden.
- **Falling trees:** a tree in plain view 12-25 m away cracks, shudders (0.7 s) and topples under physics
  pivoting on its stump; splinters into sections and shards on hitting the ground (never in mid-air on a
  neighbour); leaves burst off; logs settle, lie 25 s, then sink away when unseen; the stump stays. Trees
  with owls are left. Simpler alternative if physics proves fragile: a pre-authored fall animation per
  tree type.
- **Weather:** wind gusts rolling across the forest (trees and grass sway in shader); light rain that
  grows at 5 and 7 lamps (instanced drops that stop at roofs); lightning with thunder delayed by distance;
  tornadoes on the horizon from 8 lamps (kept hundreds of metres away).
- **Peripheral phantoms:** as you turn your head, an out-of-place object (a chair, a stop sign, a gurney,
  a TV, a mailbox, a tricycle) is placed just past the edge of your view on the side you're turning to;
  look back and it's gone.
- **Door knocks:** sometimes after you shut a door, three slow knocks answer from the trees.
- **Rifts** (dev/on demand): a ragged glowing oval showing another part of the forest; step through.
- **Leaves:** instanced dead leaves underfoot that gusts and fast movers kick up; leaves falling from
  canopies.

### 6.14 Difficulty

Main menu: Easy / Normal / Hard / Custom (every setting a slider). Each scales one existing thing:

| Setting | Easy | Normal | Hard |
| --- | --- | --- | --- |
| Flashlight battery life | 1.5 | 1 | 0.75 |
| Batteries in the forest | 1.4 | 1 | 0.7 |
| Time between extra scours | 1.5 | 1 | 0.7 |
| How many extra scours | 0.6 | 1 | 1.4 |
| Time to face a scour down | 0.75 | 1 | 1.3 |
| How far the blind one hears | 0.7 | 1 | 1.3 |
| Time for it to close in | 1.4 | 1 | 0.7 |
| How many crawlers | 0.5 | 1 | 1.5 |
| Crawlers from lamp | 3 | 2 | 1 |
| Time for moths to gather | 1.5 | 1 | 0.67 |
| Lamp cranking speed | 1.35 | 1 | 0.8 |

---

## 7. UI and presentation

- **Boot:** a black screen while procedural audio and the world generate on background threads (show a
  subtle progress line).
- **Main menu:** once per session. Title and START / DIFFICULTY / QUIT over cinematic letterbox bars;
  behind it a slow Cinemachine film of the generated forest (high over the treetops, craning down onto a
  camp or a lamp, a grazing deer, an owl, creeping low through the mist), cutting through black. No
  monsters in shot, nothing looking at the giant.
- **Intro narration** over black fading to the forest. Lines fade in, linger, fade out; capitals;
  `*word*` red, `**word**` red and lingers, `***word***` lingers long. The lines:
  ```
  *WAKE UP.* THERE IS LITTLE TIME.
  YOU *MUST* MOVE.
  **HE** IS AWARE OF YOUR INTRUSION.
  FIND THE *DOOR.*
  KEEP THE FOREST *BRIGHT.*
  THEY DON'T LIKE BEING *SEEN.*
  BUT BE CAREFUL...
  ...THEY LIKE **YOU** EVEN LESS.
  ```
  Lamp narration (shown on lighting that many lamps; "-word" marks red emphasis, 5th lamp frantic: red,
  bold, shaking):
  - 2: `Have you seen the "-Scours?"` / `They "Really" don't like light.` / `"Use it."`
  - 3: `This is "-The Auscult."` / `"This" one... is "-Blind."` / `A rare Scour "-mutation."` / `Don't let him "-hear you."`
  - 5: `"NO IT IS TOO LATE!!!"` / `"YOU MUST HURRY!!!"` / `"FOR -HE- HAS SEEN YOU!!!"` / `"LIGHT THE REMAINING!!!!"` / `"FIND THE DOOR!!!"`
  - 7: `"..."` / `"I do not know of this entity."` / `"I have never witnessed it."` / `"I must seek safety, please understand."` / `"Good luck."`
- **HUD:** crosshair; lamp meter while cranking; a row of 10 squares (lit as lamps are); battery bar
  bottom left with %, a thin stamina bar above it (reddening as you tire, longer with snacks); hotbar
  slots shown briefly on change; drone warnings.
- **Pause:** Esc; time and sound stop; letterbox bars; Return / Restart / Options / Quit. Options: mouse
  sensitivity, master / effects / music volume, motion blur on/off and strength, Classic look, Ray
  tracing (or why it's unavailable). Laid out on a virtual 1600 x 900 canvas, scaled.
- **Inventory:** Tab; dims the world (which keeps running); grid of slots and a description page.
- Use **UI Toolkit** for menus and HUD (the prototype used IMGUI everywhere).

---

## 8. Developer tools (built alongside, from milestone 1)

- **Dev window (F9)**, styled (dark rounded panel, amber accents, pill switches), tabs:
  - Game: live summary (lamps, scours, blind ones, safe?), **Invincible** (the player becomes a moving
    safe zone: nothing takes you, scours circle you), FOV, set lamps lit (nearest lit for real, so all
    escalation fires), Stick of Truth toggle, spawn overlay, model editor, session recorder, restart.
  - Lighting: themes and the main look sliders.
  - Look: every look-related config value as sliders in folding sections with search and per-value reset.
    With configs as assets (section 2.5), edits in Play persist, so "copy settings to paste to Claude" is
    only needed for builds.
- **Stick of Truth:** a dev hotbar item: right click picks any item or creature, left click spawns it
  where you point; double-tap Space to fly.
- **Spawn overlay (F1):** labels through walls for structures, lamps, items, creatures, with distance and
  bearing; a list down the side.
- **Session recorder (F11):** logs every meaningful event (each scour numbered and its state changes,
  other entities' states, what the blind one heard and what made the noise, lamps and split times, safe
  zones, seats, drone, dark room, being caught, winded, storms, falls, lightning, snacks) plus totals
  (time running, walking at full stamina, recovering, standing, crouched, safe, flying the drone,
  distance). Copy / Save (to `SessionLogs/`) / Clear; auto-saved when Play stops. Built on the event bus.
- **Model editor (F10):** a turntable far below the world; characters as simple shapes on joints (body,
  head, arms, hips) with eyes and notes; captured from how the game builds them; saved as JSON to
  `Resources/EnemyModels/<Name>.json`; a saved Scour or Player model with "use in game" replaces the
  built one.
- **Seed and layout gizmos:** draw the WorldPlan stages in the Scene view.

---

## 9. Characters and animation

- Entities as prefabs with proper rigs where possible (the owner may supply models; Tripo or similar
  can generate them, rigged with Mixamo or AccuRIG). Procedural construction is fine for the scour (it's
  the identity of the game) but build it once into a prefab/mesh at edit time, not per spawn at runtime.
- **Player body:** visible only to non-main cameras (drone, portals). From a `PlayerCharacter` config:
  model, height, yaw offset, offset, shadows, and clips (idle, walk, run, crouch idle, crouch walk, jump,
  winded, drone control, seated) blended by state with a PlayableGraph or Animator; walk and run play at
  the player's pace; a remote in the right hand (humanoid) while flying the drone. Fallback: a simple
  hooded figure from shapes.

---

## 10. Content pipeline

### 10.1 What's procedural and what's authored

| Procedural (per run) | Authored (prefabs, assets) |
| --- | --- |
| terrain, layout, vegetation placement, trails, puddles, pickups placement | structures (shack, cabin, tent, tower, car, door), props, entities, player, UI |
| sounds (synthesized at startup) with clip overrides | music if supplied |
| sky, mist, fog, weather | materials, textures (or code-generated fallbacks) |

### 10.2 Asset packs

- **Trees:** a `TreePack` config: prefabs, share of living trees (rest procedural pines), keep procedural
  dead trees, size range, trunk collider radius, keep or strip prefab colliders (strip by default; packs
  often wrap the crown), leaf/bark brightness. Each pack tree wrapped as Tree root (trunk capsule) > Sway
  pivot > prefab, so everything that works with trees works with it. Editor menu: Open Settings, Check
  (non-HDRP materials, size, LODs, colliders), Allow Felling (tick Read/Write on the models). Felling
  cuts per submesh, only LOD0, finds bark by material name (bark/trunk/wood/stem) or by the material
  hugging the trunk axis.
- The owner's current pack: a Unity Tree Creator pack (`Tree9_2..5`, "Optimized Bark/Leaf Material"),
  built-in pipeline shaders: needs the runtime HDRP stand-in (section 4.5).
- Recommend the owner use "M" (one material per colour) variants of low-poly packs, not "T" (one atlas
  material), so bark and leaves can be told apart and recoloured.

### 10.3 Build setup

An editor step that runs on script reload and before every build: maintain shader reference materials,
keep fog variants, ensure scenes are in the build list. Test a standalone Windows build every milestone.

---

## 11. Persistence

- **Settings** (volume, sensitivity, motion blur, difficulty and custom values, classic look, ray tracing,
  dev tab): a JSON settings file in `Application.persistentDataPath`.
- **Configs** are assets (not saved at runtime in builds).
- **No mid-run save** (runs are one sitting); optionally record best progress per seed.
- Model editor JSON and session logs: in the project in the editor, persistent data path in builds.

---

## 12. Build order (milestones with checks)

Each milestone ends with: CI green, the smoke test passing, a build tested, and the owner's sign-off.

**M0. Foundation (no gameplay).** Full repo with Packages and ProjectSettings; HDRP configured; layers;
asmdefs; CI with compile, tests, build; event bus; service/run-state skeleton; config base types; the
AudioMixer with groups and snapshots; dev window shell; session recorder on the event bus.
*Check:* empty scene builds and runs in CI; snapshot switching unit-tested.

**M1. Look test scene.** A small handmade patch of forest (a few trees from the pack, ground, a
clearing, one lamp post) with the full HDRP look: fog, volumetrics, ground mist shader, night sky dome,
moon, the flashlight (beam, spill, bounce, fade), exposure and grading from section 4.3. Match the
reference picture with the owner.
*Check:* owner signs off the look; frame time < 8 ms in the test scene.

**M2. Player and the scary minute.** Player controller, stamina, flashlight battery, interaction, one
scour with full behaviour (appear, creep, dark advance, face down with portal retreat, provoke and charge,
take with portal drag), one lamp, game over and respawn, the Dread and Hush audio states.
*Check:* in grey-box, the owner finds one minute with one scour genuinely tense. Iterate here until
true; everything else depends on it.

**M3. World generation.** The full staged pipeline of section 3 with tests (determinism per seed;
layout constraints; giant visible from the start on 50 seeds); instanced grass and foliage with shader
wind; tree LODs; wrapping world; structures as prefabs placed by the plan.
*Check:* 60 fps at 1080p across the whole map in a build.

**M4. The loop.** 10 lamps with escalation table (6.4), scour count rules, safe zones, shelters with
the Sheltered audio state, batteries, hotbar and items (torch, lighter, flare gun, glowsticks, camera,
key fob, snacks), pause and options, difficulty.

**M5. The hunters.** Blind one (with Noise hearing, storm, tree-knocking), crawlers, flier, giant and the
eye (hanging, eye, dream, fall, walk, searchlights, crowd), watchtower, car, lone door and dark room,
boss themes, music moods.

**M6. The living forest.** Wildlife, weather, lightning, tornadoes, falling trees, phantoms, knocks,
webs, vines, ponds and frogs, leaves.

**M7. Presentation.** Main menu film, intro and lamp narration, HUD polish (UI Toolkit), inventory, map,
drone, player body with animations, polaroids.

**M8. Tools and polish.** Model editor, spawn overlay, Stick of Truth, Look tab, tree pack tools,
player character import; performance pass; build size; first-run experience.

---

## 13. Lessons from the prototype (don't repeat these)

1. **Pipeline switch mid-project** (URP to HDRP): every shader, material helper and lighting value had to
   be redone, causing a black sky, a black cut-out giant, see-through shapes, fog seams, pink materials,
   too-bright and too-dark exposure. Decide HDRP on day one (2.1).
2. **Only `Assets/` in git**: the owner's packages and settings drifted (missing Input System, HDRP asset
   without ray tracing, wrong branch compiling URP code). Commit everything (2.2).
3. **No way to compile in the cloud**: every change round-tripped through screenshots. CI from day one (2.3).
4. **Everything built in code at runtime**, including every prop and creature from primitives: slow
   iteration, huge classes (generator 2,600 lines, scour 2,200, hotbar 1,450). Prefabs for authored
   things, small components (2.5).
5. **Per-script volume multipliers** for the dark room and shelters. Use mixer snapshots (5.1).
6. **Three settings-persistence hacks.** Use ScriptableObject configs (2.5).
7. **Global static state** needing a central reset list. Avoid statics (2.5).
8. **Thousands of GameObjects for grass, swayed by script.** Instance it with shader wind (2.6).
9. **Unbounded shadowed lights** (every scour's mouth glare) against HDRP's shadow atlas; the moon and
   lightning both wanting directional shadows. Budget shadowed lights (2.6).
10. **Patching layout after the fact** (cutting trees round lamps, moving the giant into view, the door
    spawning by the player). Plan the layout first, then grow the forest (3.1).
11. **Fog draw order:** anything meant to read through fog must be drawn after HDRP's fogged opaque pass
    with its own fog handling (4.4).
12. **Render-texture cameras** double-applying post-processing (4.2).
13. **Physics tree felling** was fragile (trees caught upright on neighbours, shattering in mid-air,
    logs hanging). Keep it simple: only break on the ground, free a stuck tree from static obstacles only,
    clear logs away after a while; or use authored fall animations.
14. **Imported packs in built-in materials** show pink in HDRP; Tree Creator shaders can't be converted by
    Unity's tool. Detect and replace at import or runtime (4.5).
15. **Unity 6 API notes:** `FindObjectsByType<T>(FindObjectsSortMode.None)` (no parameterless overload);
    the Terrain module may not be installed (don't reference `TerrainCollider` directly);
    `UnityEngine.Experimental.Rendering.RayTracingMode` must be fully qualified next to HDRP's namespace;
    `out var` inside `&&` short-circuits gives CS0165.

---

## 14. Controls

| Key | Action |
| --- | --- |
| WASD | move (drone: fly) |
| Mouse | look |
| Left Shift | run (drone: faster) |
| Space | jump (drone: climb; crawler: struggle) |
| Left Ctrl / C | crouch (drone: sink) |
| Left click | crank lamps, pick up, switches, eat |
| E | doors (shacks, cabin, car doors and glove box, tent flaps, the lone door) |
| F | use the held item |
| 1-8, wheel | hotbar |
| Tab | inventory |
| Y | sit in / leave the car |
| Esc | pause |
| F1 / F9 / F10 / F11 | spawn overlay / dev tools / model editor / session recorder (dev builds) |

---

## 15. Where to find the prototype's code for reference

The prototype on branch `hdrp` of `aleonsc/horror-game` has working implementations of every system above.
Use it as a reference for behaviour and feel, not structure. Useful files: `World/ShadowEntity.cs` (scour
behaviour), `World/BlindEntity.cs`, `World/WatcherEvent.cs` (giant), `World/FlyingEntity.cs`,
`World/Crawler.cs`, `World/DarkForestGenerator.cs` (terrain, trees, layout), `World/HdrpAtmosphere.cs`
(HDRP mapping), `Player/Flashlight.cs`, `Audio/ProceduralAudio.cs` (all sound synthesis), the boss themes in
`Audio/`, `Resources/*.shader` and `HorrorHDRP.hlsl`, and `HANDOFF.md` for recent history.
