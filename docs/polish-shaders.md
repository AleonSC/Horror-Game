# Polish: the game's own shaders

The plan asked for the game's own shaders (section 4.4) where HDRP's built-in materials look flat. This first
batch is three effects, used all over:

| Shader | What it is | Where you'll see it |
| --- | --- | --- |
| `Game/Glow` | a soft disc of light facing you, brightest in the middle, fading into the fog and where it meets surfaces | round a lit lamp's glass, the red marker on a dark lamp, the scours' eyes, thrown glowsticks, a flare (big and red when it bursts) |
| `Game/Beam` | a soft shaft of light with dust drifting in it | under the watchtower's floodlights, the giant's searchlights (from its eyes down to you), the flier's beam |
| `Game/Flame` | a licking, flickering flame: blue at the root, yellow, orange, a red tip | the torch, the lighter, a flare |

| `Game/NightSky` | the sky, drawn through the fog: twinkling stars, drifting clouds lit at the edges near the moon, the moon with faint markings, a halo and rays streaming out | overhead and behind the giant |

| `Game/Rim` | an outline of light: a copy of a model glowing at its edges | batteries lying in the forest (their green) |

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

The giant is blocky but gaunt and solid black (`solidBlack`): narrow square slabs, thin square limbs, hands
ending in long crooked fingers, big flat feet, a cube of a head under a heavy brow. Once it's fallen, its eyes
open (red) and a search ray from them sweeps the forest in the distance, back and forth as it walks and round
with its head when it stops to scan (`GiantConfig` > Awake). The 10th lamp's searchlights take over from it.

## Lamps

Victorian street lamps now: a stepped base, a fluted iron post ringed with brass collars, a crossbar, and a
lantern of glass in an iron frame under a sloping roof and a brass finial (the red marker sits on its tip).
The crank has a housing, an arm and a knob.

## Batteries and lightning bugs

Batteries lying in the forest now have a faint green outline traced round their shape instead of a glow, and
a much weaker light (`ItemsConfig` > `batteryOutline`, `batteryGlowStrength`). Their grab target is bigger
(`batteryGrabRadius`), and aiming at any round target counts all of it, not just its middle, so you can take
one running past.

Lightning bugs drift low and slow through the trees round you, each flashing the batteries' green for half a
second as it swoops up in a "J", every five or six seconds; a few sit in the grass and answer a nearby flash
two seconds later, dimmer. `ForestLifeConfig` > Lightning bugs.

## Deer

The deer are back as they were in the prototype (branch `hdrp`): six of them in small herds roaming within
70 m of you, wandering and grazing, grunting, bleating and snorting now and then, looking up with a snort
when you come within 24 m and galloping off with their white tails up inside 14 m (one spooked sets off the
herd round it, and the one you startled drops a battery). Smooth lofted bodies, knees and hocks, a proper
walk and gallop, dark coats, pale eyes, antlers on the stags. Settings: `ForestLifeConfig` > Deer.

**Let me know:** do the glows and shafts look soft rather than like flat discs and cones? Too bright, too
faint? Do the flames look like fire? And are the deer as you remember them?
