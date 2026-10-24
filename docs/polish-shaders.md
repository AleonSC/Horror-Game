# Polish: the game's own shaders

The plan asked for the game's own shaders (section 4.4) where HDRP's built-in materials look flat. This first
batch is three effects, used all over:

| Shader | What it is | Where you'll see it |
| --- | --- | --- |
| `Game/Glow` | a soft disc of light facing you, brightest in the middle, fading into the fog and where it meets surfaces | round a lit lamp's glass, the red marker on a dark lamp, the scours' eyes, thrown glowsticks, a flare (big and red when it bursts) |
| `Game/Beam` | a soft shaft of light with dust drifting in it | under the watchtower's floodlights, the giant's searchlights (from its eyes down to you), the flier's beam |
| `Game/Flame` | a licking, flickering flame: blue at the root, yellow, orange, a red tip | the torch, the lighter, a flare |

| `Game/NightSky` | the sky, drawn through the fog: twinkling stars, drifting clouds faintly lit at the edges toward the moon, and no moon: just a faint wide glow where the moonlight comes from | overhead |

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

No moon or rays are drawn now; there's only a faint wide glow in the sky where the moonlight comes from. That
spot is still behind the giant's chest (`GiantConfig` > `moonBehind`, `moonAt`), so the moonlight comes from
there. Without the giant (the look test) it's where the look's moon settings put it. Sky settings: the Look
tab, **Night sky**: the glow's colour, brightness and size, stars and how many, cloud cover, brightness and
drift.

The giant is blocky but gaunt and solid black (`solidBlack`): narrow square slabs, thin square limbs, hands
ending in long crooked fingers, big feet (a heel, an arch, a wide ball and five long toes splayed and curling down), a cube of a head under a heavy brow. Once it's fallen, its eyes
open (red) and a search ray from them sweeps the forest in the distance, back and forth as it walks and round
with its head when it stops to scan (`GiantConfig` > Awake). The 10th lamp's searchlights take over from it.

## Lamps

Detailed Victorian street lamps, built from turned (lathed) shapes and shared by every lamp:
- an eight-sided stepped stone plinth;
- a cast-iron bell base, ring on ring of mouldings;
- a slim column with eight raised flutes and three brass bands;
- a flared capital with a ladder bar and brass ball ends;
- four curling iron scroll brackets under the lantern;
- a six-sided lantern, wider at the top, with an iron frame and glazing bars, under a flared cap and a curved
  (ogee) roof, with a crown of brass spikes and a turned brass finial (the red marker sits on its tip);
- the crank on a brass-rimmed housing.

The iron is an old green-black paint. Everything round and under the light casts no shadow, so it doesn't
shade the lamp's own light into a big dark blob on the ground: the lantern, the glass, the capital, the scrolls,
the ladder bar and the brass.

A lit lamp is gentler now. The glass glows at 1.5 (it was 6), and the haze round it is 0.35 across 1.4 m (it was
1.5 across 2.4 m), so it no longer blooms into a white disc. The light is 14 candela (it was 25). `LampConfig` >
Lit has `lightIntensity`, `glassHalo` and `glassHaloSize`.

## Paths

- **One surface:** the trails and the lamp patches are now one surface for each piece of ground, not separate
  overlapping strips. It's built on a fine 25 cm grid from each point's distance to the nearest trail or patch
  edge. Where trails meet, cross, or run into a lamp's patch, they join up cleanly, with no darker overlaps or
  gaps.
- **Edges:** they wander a little and fade over the last 40 cm. The surface follows the ground mesh exactly.
- **The dirt:** paler, sandy packed earth, with faint ruts and grit, a lighter middle where it's walked most,
  and edges darkening as the litter creeps in. A fine bumpy grain of little pebbles lies over it in world space
  and catches the flashlight (HDRP's detail map on the second UV set).
- **Verges:** grass lines both sides of every worn trail, thickest right at the edge, about 3 clumps a metre
  each side, with the odd taller clump. The rims of the lamp patches get short grass only.
  `WorldGenConfig` > Trails > `trailVergeGrass`, `trailVergeTallGrass`. This applies to new worlds.
- **Your own trail material:** if you set one in `WorldArtConfig`, it's used on the same surface: U is the
  distance from the middle line (0.5 on it, 0 at the edge of the fade), V runs over the ground, and UV1 is the
  ground position.

## HUD

- **Hotbar:** a dark band ruled in warm light with a diamond in the middle. Each slot shows a line drawing of
  its item (`UI/ItemIcons.cs`, drawn in code), its key and its count. The slot in hand rises, warms and gets warm
  corner brackets; a lit torch or lighter glows. Its name sits above between diamonds, with what's left in it
  underneath.
- **Lamps:** ten diamonds on a fine rule at the top. Lit ones glow, and the one just lit swells and flares. A
  "3 / 10" count sits beside them.
- **Battery:** a battery shape with ten cells and a flashlight icon, red and blinking when low. Stamina is a thin
  rounded bar above it.
- **Crosshair:** four ticks close in round anything you can use. The prompt band is ruled in warm lines.
- The inventory screen uses the same item drawings, and menu buttons get a red edge and a diamond on hover.

## The white moth

Every 2 to 5 minutes, a white moth may appear fluttering in the dark ahead of you, trailing fine white dust. Get
it in your flashlight beam and it follows the beam, about 4 m out. Lead it into a lit lamp's light (it heads for
one by itself when one is within 7 m) and it circles the glass and vanishes into it. You get 15% more sprint
time for the rest of the run, and your breath back. If you swing the light away faster than it can follow, or
switch the light off, for about a second, it gives up and wanders slowly off in a random direction. Catch it
again before it's gone and it follows again, but if it drifts into a lamp on its own it doesn't count.
`ForestLifeConfig` > White moth.

## Batteries and lightning bugs

Batteries lying in the forest now have a faint green outline traced round their shape instead of a glow, and
a much weaker light (`ItemsConfig` > `batteryOutline`, `batteryGlowStrength`). Their grab target is bigger
(`batteryGrabRadius`), and aiming at any round target counts all of it, not just its middle, so you can take
one running past.

Lightning bugs drift low and slow through the trees round you, each flashing the batteries' green (a little brighter now: `lightningBugGlow` 4) as it swoops up in a "J":
a quick rise to full (about a tenth of a second), then a long fade to nothing (about a second and a half),
then a wait of 10 to 18 seconds before the next (`lightningBugEvery`, `lightningBugRise`, `lightningBugFade`); a few sit in the grass and answer a nearby flash
two seconds later, dimmer. `ForestLifeConfig` > Lightning bugs.

## Owls and vines

The owls are small and round now (built the deer's smooth way): a plump body with a speckled cream front and
folded wings, a big round head with a pale heart-shaped face, ear tufts, a little beak, toes gripping, and the
white eyes as before. Each sits out on its own branch: a bough curving up out of the trunk to a slender tip,
two twigs with leaves at their ends, a crust of pale lichen; it faces out along it.

Vines hang from about a tenth of the trees, up to three strands from one, down to between knee and head
height: tapering stems with pairs of leaves. They swing in the wind and gusts, bend rather than fold, are
shoved aside all the way up your body as you walk through, swing on after you, and rustle. `ForestLifeConfig`
> Vines (how many, how low, how windy; on Restart for how many).

## Deer

The deer are back as they were in the prototype (branch `hdrp`): six of them in small herds roaming within
70 m of you, wandering and grazing, grunting, bleating and snorting now and then, looking up with a snort
when you come within 24 m and galloping off with their white tails up inside 14 m (one spooked sets off the
herd round it, and the one you startled drops a battery). Smooth lofted bodies, knees and hocks, a proper
walk and gallop, dark coats, pale eyes, antlers on the stags. Settings: `ForestLifeConfig` > Deer.

**Let me know:** do the glows and shafts look soft rather than like flat discs and cones? Too bright, too
faint? Do the flames look like fire? And are the deer as you remember them?

## Trees and crows

Trees (and boulders) are now drawn out to 200 m, up from 115 m, with their simpler versions starting at 40 m
and 95 m (`ForestPopulator` > `TreeRange`, `TreeLods`). This costs more to draw; turn it back down there if the
frame rate drops.

Crows are black birds now, with a head, a thick beak, a tail, legs and wings in two parts. Every 20 to 55
seconds a crow flies over high up, either alone or in a straggling flock of 4 to 12, flapping and gliding and
cawing. Three flocks are always on the ground somewhere 40 to 80 m from you, put down out of view in the open.
They peck, hop and look round. Walk within 14 m, run within 24 m, or make any noise that reaches them, and they
all burst up one after another with a flutter and caws. They climb away from you, level out and fly off, and a
new flock lands somewhere else later. The old crows that burst up out of the trees now use the same model.
`ForestLifeConfig` > Crows.


## Campsites

The tent was upside down: its two sides leaned outward instead of meeting at the ridge. The campsite is rebuilt as
a camp someone left in a hurry:
- **The tent:** an A-frame of faded olive canvas that sags a little between its poles and bellies in down the
  sides. The back is closed and the front door flaps are tied back toward the fire. It has end poles, guy ropes
  out to stakes, a ground sheet, and a sleeping bag along one side (loot still lies down the middle).
- **The fire:** a dead fire in a ring of uneven stones, with ash, charred logs fallen in, and a few embers still
  glowing faintly.
- **Around it:** two logs to sit on and a stump, a pot on the stones and a knocked-over mug, an old camping
  lantern by the tent door, firewood stacked by the tent, and the backpack slumped where it was dropped (lid,
  pocket, straps, a bedroll on top).
- **Variation:** each campsite varies a little with its position.
- **Your own campsite prefab:** if you set one in `WorldArtConfig`, it's used instead, as before.
