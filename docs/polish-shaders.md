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

## The giant, walking

- **It never reaches you:** it only ever seems to come on. As it nears its closest (`GiantConfig` >
  `closestDistance`, 220 m) it slows to a stop while still striding as if coming, and if you run at it, it
  stays just as far off. It no longer takes you.
- **The fall:** it drops out of its hang with its arms flung slowly up and out and its head coming up. Nearing
  the ground, it gathers itself: knees drawn up, body pitching forward, arms coming down to meet the ground.
- **The landing:** it lands in a deep crouch (fingers to the ground), sinks a little further as it takes the
  weight, holds, then slowly rises. It eases into its stride over a few seconds, and in and out of it when it
  stops to scan, instead of snapping between poses.
- **Hands:** they're turned at the wrist so the palms face the thighs and the long fingers hang in a row
  along the body, curling in toward the leg.
- **Feet:** they're on ankle joints and stay flat on the ground as it walks and crouches; they hang toe-down
  while it's on the chain. The tops are rounded now: a domed heel, a high instep sloping to the ball, and
  rounded toes on a flat sole.
- **Steps:** each step is louder, with a deep sub-bass weight and a low boom under it that swell as it nears.
  The shake is stronger (`stepShake` 4.5, and a third of that even when it's far off).

## The chains

- **Links:** every chain is real heavy oval links now, each turned a quarter from the last so they interlock,
  instead of a plain bar. Each chain is made of runs of eight links, as many runs as it needs to reach up into the
  dark (`Entities/Giant/ChainMeshes.cs`).
- **Empty chains:** each one ends in an open neck shackle, hanging. It's a great iron ring hinged at the top,
  with its two halves swung apart and a lock eye on each free end, sized for a neck as big as the giant's.
- **The giant:** it wears a closed collar round its neck, bolted at the front, and its chain hangs from an eye on
  the back. When the chain snaps, the collar stays on, with a few broken links hanging straight down from it as
  it walks.

The chains fade into the sky: they're drawn with `Game/SkyFade`, a silhouette that thins out with height. Each
chain starts fading 30% of the way up its length and is gone by 90% (`GiantConfig` > `chainFadeFrom`,
`chainFadeTo`), so none of them ever visibly ends.

## Wind and ambience

- **Wind sway:** the wind was there, but the sway was far too small to see: a tall tree's top moved about a
  centimetre. Now:
  - A tree's top moves about 10 cm in the breeze and nearly a metre in a strong gust.
  - Bushes and grass bow well over.
  - The breeze sways things back and forth every couple of seconds over a slower lean, out of step from place
    to place.
  - The vines hanging from the trees lean with them.
- **Ambience:**
  - A new layer of the canopy hissing and rustling in the wind, louder in the gusts.
  - The wind itself is louder.
  - Every 15 to 40 seconds, an animal calls far off in the dark: an owl, a barn owl's screech, or a crow.
- **Owl and crow calls:** these are the prototype's (the HDRP build's): the tawny owl's hoot, a pause, then
  the long wavering hoo-hoo-hooo; the barn owl's screech (the owls give one now and then); and the crow's
  rasping caw.

## The forest floor

- **The soil:** the ground is fine, dark soil now, made in code (`GroundTexture`, tiling about every 3 m). It
  has thousands of little lumpy crumbs, the odd clod, little pits, and a scatter of pale grit.
- **Bumps for the lighting:** a normal map is made from the same surface. The crumbs and pits catch the
  flashlight and throw tiny shadows.
- **Grass patches:** thick, ragged patches of grass, thinning at their edges, lie about the forest floor
  (`WorldGenConfig` > Grass patches: 220 of them, 2 to 5.5 m across, 8 clumps a square metre in the middle). 60%
  of them sit on the trails, so the paths run through them with grass either side and none on the trodden earth.
  This applies to new worlds.

## The bunker, the camps' smoke, and what's always there

- **The bunker:** one old concrete bunker is dug into an earth mound somewhere in the forest, its doorway facing
  the trail that runs to it.
  - **The front:** a thick, stained concrete front (rain streaks, moss low down, cracks) with a dark doorway
    under a heavy lintel and a capping slab, and stepped wing walls angling out to hold back the earth.
  - **The mound:** heaped over it in the forest floor's soil, banked against its sides.
  - **Inside:** a corridor runs back to a small, dark room, with a crate (2 batteries, 2 glowsticks, half the
    time a flare gun), a rusted drum and rubble. Inside counts as shelter.
  - **Settings:** `WorldGenConfig` > `bunkers`; your own model goes in `WorldArtConfig` > `bunker`.
- **Campfire smoke:** grey puffs drift up off each campsite's dead fire, swelling, thinning and carried off by
  the wind. They're lit like anything else, so they show in your flashlight and the embers' glow.
- **Always there:**
  - Every world has its 3 campsites, its 3 shacks (one of them the cabin), its car and its bunker. If
    there's no room by the usual rules, each tries again less fussily (closer to the others, on steeper ground)
    rather than being left out.
  - A test checks this on 20 worlds (and it held on 60).
  - Your saved `WorldGenConfig` asset decides the counts (`campsites`, `shacks`, `bunkers`).
- **The scour hunt sound:** it's no longer music: no notes and no beat. It's a deep rumble that swells and
  sinks, something breathing close by in uneven breaths, dull thumps at odd moments, and the odd long scrape.

## Cranks, bushes, the flare, glowsticks, dropping things, and key hints

- **The crank turns:** each lamp's crank turns with the meter: a quick turn with each click, four whole turns
  from empty to full (`LampConfig` > `crankTurns`), winding back as it drains. A lamp prefab's crank turns about
  its own X axis.
- **Bushes:** they're leafy now: thin stems out of the ground, each ending in a clump of pointed leaves folded
  along their midribs and drooping a little, all facing outward. There are more of them: 3,200, up from 1,400
  (`WorldGenConfig` > `bushes`; new worlds).
- **The flare:** it's far gentler.
  - The burst is 6,000 candela, down from 60,000, and reaches 160 m.
  - It barely lights the fog now (`flareFogGlow` 0.15), so the forest no longer turns solid red.
  - The haze round it in the sky is a fifth of what it was (`flareHaze`).
  - Its colour is a warmer red.
- **New models:**
  - **Flare gun:** a break-open one in blaze orange, with a fat barrel and dark muzzle, a ribbed top, a white
    warning band, the hinge and the brass rim of the cartridge, a black hammer, a trigger in its guard, ribbed
    black grip panels and a lanyard ring.
  - **Glowstick:** a slim yellow tube with ridged black caps and a little loop. It glows yellow without blowing
    out to white: the tube glows gently, the haze round a thrown one is softer, its light is 0.8 (was 1.5) and
    lights the mist less.
- **Dropping things:** in the inventory, right-click a slot or drag it out of the slots to put it on the
  ground in front of you. G does the same for what's in your hand (or, with the inventory open, the item whose
  page is showing).
  - It lies there with what's left in it (fuel, shots) until you take it again.
  - You can't drop the flashlight, or anything burning or out in the world.
- **Key hints:** they're drawn as keycaps (dark keys with a light rim and a deeper bottom edge, a red diamond
  between hints):
  - the crosshair's prompt;
  - the inventory's hints;
  - the drone's hints;
  - a full key list under Options > Controls.

## Muffled under cover

Inside the car, a shack, the cabin, the bunker or a tent, the world outside really is muffled now. Before, the
Sheltered sound state only turned it down.
- **The filter:** outside sounds go through a low-pass filter that closes in over a third of a second as you get
  under cover and opens out again as you leave. They come through dull and low, as through walls.
- **What's muffled:**
  - the forest's wind, rustling and drone;
  - rain and tornadoes;
  - deer;
  - things out there (the Entities group);
  - any sound in the world more than 4 m from you.
- **What isn't:** your own sounds, the music and the menus. The tension under it all is only partly muffled.
- **For your own loops:** put the `Muffled` component on an outside sound's own looping AudioSource.

## The car, the shacks and the cabin, in detail

- **The car** (`World/Build/CarModel.cs`, driven by `World/Places/Car.cs`): an old four-door sedan.
  - **Outside:** dull dark-red paint, rust round the edges, chrome bumpers that wrap round the corners, a barred
    grille, round headlights in chrome rims with amber indicators under them, framed tail lights, number plates,
    drip rails, side mirrors, door handles and chrome trim. The back-left tyre is flat.
  - **Glass:** tinted windscreen, rear window and door windows.
  - **Inside:** a dashboard with three dials, the steering wheel on its column, a gear stick, the mirror, two
    front seats with headrests, the back bench, the parcel shelf and carpet.
  - **Seats:** Y by any door gets you into that seat, and Y again moves you round all four ("the driver's
    seat", "the passenger seat"...). E gets you out by your seat's door.
  - **Doors and lids:** each of the four doors opens (E). The trunk lid and the glove box's little door click
    open (LMB), and what's in them can only be searched while they're open. The back seat is reached through the
    open rear left door.
  - **Headlights:** click either headlight, or the switch on the dashboard. The prompt shows the battery left.
  - **Key fob:** LMB chirps the car (two chirps, the indicators flashing). RMB sets off its alarm: the horn
    blaring on and off, the lights flashing, heard 150 m off and drawing things to it. RMB again stops it. The
    alarm runs off the headlights' battery, three times as fast (`alarmDrain`).
- **The shacks** (`World/Build/HutModel.cs`): weathered planks with gaps between them, a rusty tin roof, and:
  - **Outside:** corner posts, stone footings, a low tin roof in ribs with barge boards and a ridge cap,
    planked gable ends, a window each side (one boarded up), a plank door with its Z-brace, strap hinges, latch
    and pull, and a step.
  - **Inside:** a plank floor, an iron cot with a stained mattress, pillow and blanket, a planked table and a
    chair, two shelves of jars and cans, a bucket, a hooped barrel and an old calendar.
- **The cabin:** log walls, a shingle roof, a porch, and:
  - **Walls:** logs crossing and sticking out at the corners, with paler cut ends.
  - **Roof:** a steep shingled roof in uneven overlapping rows.
  - **Windows:** four, each with a frame, glazing bars, dirty glass and a sill.
  - **Porch:** across the front, with its deck, steps, posts with brackets, railings with balusters, its own
    shingled roof and a rocking chair.
  - **Inside:** a rug, a bed with a headboard, a table with two chairs (one knocked over), a cast-iron stove
    with its pipe up through the roof, a bookshelf of books, shelves of jars, a ceiling beam with a lantern
    hanging from it, and an old coat by the door.
- **Collision:** what you bump into is still the plain room underneath, now hidden.
- **Your own models:** prefabs set in `WorldArtConfig` are used instead, as before.

## Depth of field, and moths round your flashlight

- **Depth of field:** a new Options > Picture slider sets how soon the distance goes soft (0 turns it off). At
  the default (50%), the forest round you stays sharp out past 200 m and the giant, drawn 360 m off, is fully
  blurred. The waking blur still pulls the sharp distance right in over the top of it.
- **Moths round your flashlight** (`World/Life/FlashlightMoths.cs`):
  - **Gathering:** the longer your flashlight's on, the more moths come in out of the dark to flutter about in
    the beam, up to 28 after two minutes (`ForestLifeConfig` > Moths round your flashlight; the difficulty's
    moth time scales it). They trail after the beam as you swing it.
  - **Light off:** they're lost, milling about where they were.
  - **Light on again:** the ones still near you may find it again. Where you stood, about two thirds come back;
    5 m on, under half; 10 m on (flicked off and on while running), hardly any (`mothComeBack`,
    `mothComeBackRange`). The swarm builds up again from those that came back.
  - **Giving up:** lost ones give up after 4 to 10 seconds and drift off, fading (`mothGiveUp`).

## Loot round backpacks, outlines on everything to take, and smoke to the sky

- **Backpacks:** what was in a campsite's backpack (the flare gun, glowsticks, batteries, sometimes the fob or
  the radio) lies on the ground in a ring round it, each turned its own way, rather than being searched out.
- **Outlines:** everything lying about to be picked up has its shape traced in a faint, gently pulsing white
  light, as batteries are in green (without their glow). That covers a torch on a table, the loot round a
  backpack, a snack in a tent, the map on the watchtower, and things you've dropped. `ItemsConfig` >
  `itemOutline`, `itemOutlineColor` (0 turns it off).
- **Campfire smoke:** it now rises in a thin column off each campfire, climbing faster as it goes, spreading
  and leaning with the wind, up past the treetops to about 80 m before it thins away. It's drawn out to 450 m,
  so you can see the camps' smoke on the skyline.

## Scours burned off by light all round them

A scour caught in a light that fills the ground round it comes apart, as it does when you face it down: a last
shudder, then it bursts into dark motes that scatter on the wind, with its dissolving sound. That covers a lamp,
your torch or lighter flame, the car's headlights, the watchtower floodlights and the space round a shelter.
Before, it simply vanished. It happens whether it was stalking, being faced down, charging or just appearing,
and costs your flashlight nothing.


## Fuller pines, ray tracing, bigger dev text, flashlight presets, a giant that stays far off

- **Pines:** more tiers, each arched (rising a little before it droops) with a smaller skirt tucked under it,
  so there's no daylight through the middle. Up close (within 40 m) every bough carries drooping sprays of
  needles that rag the edges; further off (to 95 m) one spray per bough.
- **Ray tracing:** Options > Picture > Ray tracing (off by default). On, it turns on ray-traced ambient
  occlusion and reflections and the moon's ray-traced shadows. It only works on a ray tracing card under
  DirectX 12 with the High Fidelity quality level (the only HDRP asset with ray tracing switched on); otherwise
  the option says so and does nothing.
- **Dev window:** bigger text, buttons, number boxes and a wider window by default.
- **Flashlight presets:** dev window > Lighting > Flashlight: Standard, Old torch, Tactical LED, Floodlight,
  Spotlight, Dim and grim, Moonlit. Each changes only the look (colour, brightness, reach, width, edge, fog,
  spill, bounce), not the battery or the hand. Fine-tune in the Look tab; Save keeps it.
- **The giant** never comes nearer than 90% of the distance it landed at (`GiantConfig` > `keepAway`, on top of
  `closestDistance`): it barely gains, striding forever on the horizon.

## Bark, felled trees that break up, and the owner's lighting as the default

- **Bark texture:** made in code (`BarkTexture`): thick plates, taller than wide, split by deep dark
  fissures, cracked across, with fibres running up them and patches of grey-green lichen, plus a matching
  normal map so the fissures catch your light. Dead trees and logs get the same grain weathered grey.
- **Falling trees:** a felled tree now snaps off above a **stump** that stays (with a torn, splintered top of
  pale fresh wood). The trunk above is cut into 3-6 **logs** (its own mesh, sliced, the cut ends capped)
  that topple together. As it hits the ground the **crown bursts** into a flurry of needles that flutter
  down, settle and fade. A few seconds later the **logs come apart** and roll (real physics bodies). After
  25 s they sink away when you aren't looking. Tree-pack art whose meshes can't be read still falls whole.
- **Default lighting:** the owner's settings are now the defaults, both in the code (the Reference theme)
  and in `LookConfig.asset` / `FlashlightConfig.asset`: brighter flashlight (73), thinner fog (0.0044), no
  moonlight, and more contrast and saturation. The flashlight presets are scaled to match. Other themes
  start from these values too.

## More realistic forest floor, trails and bark

All three are now built the same way (`ForestSurfaces`): a height field painted by laying real-sized pieces on
top of each other, each resting on what's already there. Colour, bumps (normal map) and HDRP's mask map
(crevice shading and shine) all come from it, so they agree. Made on background threads while the world plan
is worked out, once per session (`ForestTextures`); about a second.

- **Forest floor** (repeats every 4 m, 1024 px): dark damp soil with a faint sheen, clumpy moss in patches, an
  old layer of flattened dark needles, stones half sunk in, fallen leaves (some lobed, some rotted through,
  edges curled), crooked twigs with side twigs, and a deep litter of pine needles in pairs, from fresh rust to
  rotting grey-brown. A detail map of big light/dark, bumpy and damp patches lies over it on its own 23 m
  repeat, so the 4 m repeat doesn't show.
- **Trails**: sandy packed earth, paler and smoother where it's trodden down the middle, with the odd boot
  print, gravel and a few bigger stones worked up out of it, and pine litter creeping in from the edges as it
  fades. It now has its own bumps and mask too, with the fine grain still laid over it in world space.
- **Bark**: plates of overlapping scales lifted at their lower edges, the odd scale fallen away to show the rust
  layer under the grey, deep fissures with rust-brown walls and a dark floor, weathering and lichen. **Dead
  wood**: silver-grey wood with long cracks up the grain and streaks, with patches of bark still clinging on.
- **Your own**: `WorldArtConfig` now has **Bark** and **Dead Wood** materials alongside Ground and Trail.

## SmoothStep fix, and scours peeking round trunks properly

- **SmoothStep:** Unity's `Mathf.SmoothStep(a, b, t)` blends between a and b; several places used it as a
  threshold, so they barely varied. They now use `MathUtil.SmoothThreshold` (Game.Core). What changes: grass now
  grows in real meadows and is sparse between them (about 30% more grass overall, much less evenly spread); tree
  patches and glade/edge thinning follow their noise properly; the ground mist is properly patchy; the
  flashlight's reflector rings start where they should; and the placeholder rock texture's blotches and moss.
- **Peeking scours:** before, a scour behind a trunk slid sideways and leaned its whole body out, and its
  hanging arms swung out past the far side of the trunk. Now it stands right behind the trunk, narrow, arms
  in close (`PeekHidden`), and to peek it only cranes its tilted head and near shoulder round the edge, the
  long fingers of that hand curled round the bark at head height, while the far arm (and the extra arms) are
  held straight down inside the trunk's outline (`Peek`). It blends between the two by how far out it is,
  creeping out slowly and snapping back in when you look. It picks a side at random, and on thicker or thinner
  trunks it stands to one side to match. (The pose was worked out against the rig's bones offline; model looks
  get `Peek` / `PeekHidden` as animator poses.)

## Campfires you light, and tents you crawl into

- **Campfire** (`Campfire`): the fire is out and laid ready, in a fancy pit: a ring of big rocks with a lower
  ring of smaller sooted stones inside it, a bed of old ash and charcoal, kindling and a twist of birch bark
  under a teepee of split logs, and an iron tripod over it with a kettle hung on a chain. Click it with a
  lighter on you (it uses a little fuel) and it catches over a few seconds: flames licking up through the logs,
  a flickering warm light with soft shadows, a crackling sound (new `Sound.FireLoop`), smoke rising to the
  skyline, and a **safe circle of 8 m for 10 minutes**. Over its last minute it dies down and the circle shrinks
  with it; the logs char; the embers glow a while and fade. It lights once. Settings: `LampConfig` > Campfires.
  Smoke from the last fire still rises off the ashes into the sky, so camps can be found from afar.
- **Tent**: the door flaps open and shut with **E** (from inside or out); shut, they block the doorway. With
  them open, **E** on the doorway crawls you in to sit on the blanket, low, facing the door. Inside, E on the
  flap opens or shuts it, and E anywhere else crawls you out (if the flap's open). Outside sounds come through
  muffled while you're inside (the tent's a shelter, and its refuge circle still keeps you safe).
- A little **lantern** hangs from the ridge: click it on and off (a warm, small light that stays in the tent
  but for the doorway). A **wool blanket** (red and green check, its top turned back) and **two pillows** lie
  on the ground sheet, in place of the old sleeping bag.

## Brightness option, and a lighter menu

With the moon off, the forest is lit only by the soft ambient light (a dim sky), so the dark soil read as
black. **Options > Picture > Brightness** (0-1, default 0.5) lifts that ambient light: 1x at 0 (the look
exactly as tuned), 2.25x at 0.5, 6x at 1. It lights the ground, trees and things, not the fog, so the night
stays murky while what's round you shows. Behind the **main menu** the ambient light is tripled on top, so its
views of the forest read (`LookController.MenuLift`).

## Wind, hut lanterns and batteries

- **Wind** (`Sound.WindLoop`, now 16 s): a deep, airy rush in two bands that swells and falls away in uneven
  gusts (the higher band opening up as it gusts), a low rumble under it, and at each gust's height a faint,
  hollow moan sliding up and down. The gusts are sums of waves that repeat in the loop, so it loops clean.
- **Lanterns** (`Lantern`, made by `LanternModel`): a small hurricane lantern (tin base and cap, glass globe in
  a wire guard, bail handle). Click it on and off: a warm, small light with soft shadows. One stands on each
  shack's table; one hangs on a chain from the cabin's beam; and the tent's is the same lantern, hung by a cord.
- **Batteries**: every shack's table now has 2 (and a 50% chance of a third); the cabin's table has 3.
  `WorldGenConfig` > `batteriesInShack`, `batteriesInCabin`.
