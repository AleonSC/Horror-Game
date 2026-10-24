# Polish: the game's own shaders

The plan asked for the game's own shaders (section 4.4) where HDRP's built-in materials look flat. This first
batch is three effects, used all over:

| Shader | What it is | Where you'll see it |
| --- | --- | --- |
| `Game/Glow` | a soft disc of light facing you, brightest in the middle, fading into the fog and where it meets surfaces | round a lit lamp's glass, the red marker on a dark lamp, the scours' eyes, a battery's gleam, thrown glowsticks, a flare (big and red when it bursts) |
| `Game/Beam` | a soft shaft of light with dust drifting in it | under the watchtower's floodlights, the giant's searchlights (from its eyes down to you), the flier's beam |
| `Game/Flame` | a licking, flickering flame: blue at the root, yellow, orange, a red tip | the torch, the lighter, a flare |

| `Game/NightSky` | the sky, drawn through the fog: twinkling stars, drifting clouds lit at the edges near the moon, the moon with faint markings, a halo and rays streaming out | overhead and behind the giant |

They live in `Assets/Game/Rendering/Resources/Shaders` with a shared include, `Horror.hlsl`.

**If one has an error in your Unity:** it's left out and the old look stays (no pink squares), with a warning
in the Console naming it; the shader's own error will be in the Console too. Send me a screenshot of that.

**One thing to commit:** Unity makes a `.meta` for `Horror.hlsl` the first time it opens the project.

**How strong they are** (live, in the config assets):
- Lamps: `LampConfig` > `glassHalo`, `glassHaloSize`, `markerHalo`.
- Scour eyes: `ScourConfig` > `eyeHalo`.
- Floodlights: the Watchtower's `beamGlow`. Searchlights: `GiantConfig` > `searchlightShaft`. Flier:
  `FlierConfig` > `beamShaft`.
- Torch and lighter flames: `ItemsConfig` > `flameGlow`.

## The sky, the moon and the giant

The moon hangs behind the giant's chest (`GiantConfig` > `moonBehind`, `moonAt`), so it stands black against
it with the rays streaming out round it, and the moonlight now comes from there (the moon's shadows have
moved). Without the giant (the look test) it's where the look's moon settings put it. Sky settings: the Look
tab, **Night sky**: the moon's colour, size, glow and rays, stars and how many, cloud cover, brightness and
drift.

The giant is rebuilt blocky and bulky: a slab of a chest and heavy shoulders, thick square limbs, big hands
and feet, a cube of a head under a heavy brow.

## Lamps

Victorian street lamps now: a stepped base, a fluted iron post ringed with brass collars, a crossbar, and a
lantern of glass in an iron frame under a sloping roof and a brass finial (the red marker sits on its tip).
The crank has a housing, an arm and a knob.

## Deer

The deer are back as they were in the prototype (branch `hdrp`): six of them in small herds roaming within
70 m of you, wandering and grazing, grunting, bleating and snorting now and then, looking up with a snort
when you come within 24 m and galloping off with their white tails up inside 14 m (one spooked sets off the
herd round it, and the one you startled drops a battery). Smooth lofted bodies, knees and hocks, a proper
walk and gallop, dark coats, pale eyes, antlers on the stags. Settings: `ForestLifeConfig` > Deer.

**Let me know:** do the glows and shafts look soft rather than like flat discs and cones? Too bright, too
faint? Do the flames look like fire? And are the deer as you remember them?
