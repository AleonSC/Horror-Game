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

## Head bob and steaming breath

- **Head bob** a little stronger (`PlayerConfig` > `bobAmount` 0.065, was 0.05), heavier when running (x1.35)
  and softer creeping (x0.7).
- **Breath** (`PlayerBreath`): a soft puff of steam from just below the view with each breath out, drifting
  forward and up, spreading and thinning. Rested: one faint puff every 3.5 s or so. As stamina runs down the
  breaths come quicker (to every 1.1 s), the puffs bigger and thicker, two at a time; winded, three-puff pants.
  It's lit like anything else, so it shows in your flashlight beam and by lamps, and barely in the dark.

## Ground mist that follows the ground

The ground mist was one flat box set at the ground height under the camera, so on a slope it sank into the
hill above you and floated off the ground below you, and the whole layer rose and fell as you climbed. Now
(`GroundMist`) it's a grid of overlapping boxes (5 x 5 at the default reach; 28 m apart, 56 m across), each
sitting on the ground under its own middle. Their soft edges fade linearly from the middle out, so
neighbours add up to exactly one even layer of mist: no seams, and it lies along the hill, rising and falling
with the land rather than with you. Each box's ground height eases toward new readings, so it doesn't jump
over a rock or log. The patches still stay put in the world as you walk.

## Lightning bolts

Most lightning strikes now show a bolt (`LightningBolt`): a jagged streak from the cloud base down toward the
horizon in the direction the flash comes from, with forks splitting off it and forking again, thinner each
time, a glow in the cloud where it starts, flickering with the flash and gone with it; its thunder follows as
before (later the further off). Each strike is a new bolt. It's drawn 340 m off and scaled to look as big as
one at its real distance would (nearer strikes tower over you; far ones are low on the horizon), and the fog
dims it with distance. `WeatherConfig` > `boltChance` (0.75: the rest flicker only in the cloud),
`boltBrightness` (30).

## More grass, root flares and hummocks

- **Hummocks** (`WorldGenConfig` > `hummocks` 0.35 m, `hummockSize` 7 m): low mounds and dips over the whole
  forest floor, with a finer lumpiness on them, so it's never flat. They have their own seed (the rest of the
  plan is unchanged), and the ground's still flattened under buildings as before.
- **Root flares**: every pine (and dead tree) has roots spreading from the foot of its trunk out into the ground,
  each in two crooked, tapering lengths sunk into the soil at the end (6 close up, 4 further off).
- **More grass**: `grass` 16000 (was 10000) about the meadows, and twice the tufts round each tree's foot
  (`tuftsPerTree` 8-16, was 4-10), spread a little further out (to 1.6 m).

## Smoother mist from above, and an endless forest behind the menu

- **Mist ripple**: HDRP stores volumetric fog in depth slices (64 by default, spread over the whole far clip,
  about 9 m each), and a 2 m layer of mist seen from above fell between them, showing as bands. Now the
  volumetrics only reach out to 400 m (beyond, the fog carries on without them), so the slices are finer, and
  the higher the camera is above the ground, the taller and thinner the mist is made (the same amount of it),
  up to 4x at 38 m up. (A custom fog quality packing the slices nearer the camera was tried and taken out: the
  further slices got thick enough to show as layers in the fog and the flashlight beam at walking height.)
- **Endless forest behind the menu** (`Panorama`): the map wraps every 320 m and only its nearest copy was
  drawn. While the menu's film plays, the trees are also drawn at the copies all round out to 480 m, each
  ground chunk shows its copies one map-width away (`PanoramaGround`, no colliders), and the film camera sees
  out to 750 m, so the forest runs on into the fog in every direction.

## Dev tools: Reveal

A **Reveal** tab in the dev window (F9): tick what to mark (All / None buttons), grouped as Items (batteries;
torches and lighters; flare guns and glowsticks; everything else), Places (camps, shacks, the cabin, the car,
the bunker, the watchtower, the landmark tree, lamps (lit or not), the lone door) and Hunters (scours, the blind
one, crawlers, the flier), each with how many there are now. Each one's marked on the screen in its colour, a
diamond with its name and distance, through trees and walls; off the screen or behind you, a diamond at the
screen's edge points the way. The marks stay with the window shut, and update twice a second (things picked
up, hunters coming and going). Under each ticked category, every one found is listed nearest first with a
**Go** button: it puts you a couple of metres short of it on the ground, facing it (out of any seat, and round
it to the first spot with room to stand).

## Logs on slopes, felled trees snapping on contact, a slow flurry of leaves

- **Fallen logs** (the forest's own) now lie along the ground: tilted to the slope between their ends, and
  raised just enough that no part is under the ground along their length (on a hump the ends lift clear). Their
  colliders match.
- **Felled trees** snap into their logs the moment any log touches anything as it goes over (the ground, another
  tree, a rock or a building; not its own stump): each log becomes a physics body carrying on the way it was
  swinging, with a jolt apart, so they tumble and roll from where they hit. Trees that fall whole still thud
  down with a bounce.
- **The crown** breaks up at that moment into a cloud of about 1,100 needle tufts and leaves hanging in the air
  where it was, which drift apart and flutter slowly down over half a minute or so (falling at about 0.4 m/s),
  carried off on the wind, then settle and fade.

## Waking up as in the HDRP build

The intro's waking now matches the old HDRP build's: **9 s** of grogginess (`PresentationConfig` >
`wakeGroggyTime`), which only starts to wear off once the black has lifted past 70%, clearing slowly at first
and then quickly. The head **lolls and sways** (about 2 degrees up and down, 2.5 side to side, 4 of roll, on
slow waves; no tremble now), the **blur** softens everything past arm's length (0.2 to 3 m) at 70% strength,
mouse look starts at half speed, walking at 35%, you speed up and slow down sluggishly (35%), and you can't run
until it's mostly worn off. The extra vignette while groggy is gone. (`PlayerConfig` > Waking up: groggySway 1,
groggyShake 0, groggyAcceleration, groggyLook 0.5, groggySpeed 0.35, groggyBlur 0.9: a little stronger than the HDRP build's 0.7.) Waking after being taken
uses the same.

## Lamps' worn patches

The trodden earth round each lamp read as a stamped-on circle: a near-round disc 2.8 m across with a ring of
grass planted along its edge. Now it's smaller (`lampPatchRadius` 2 m) and of no particular shape (big lobes
and small ones, up to 60% out from round), and instead of the ring there's a loose scatter of clumps thinning
out from its edge, which the forest floor's grass runs into.

## The breakable tree

Only one tree model can fall now: the **breakable pine**, one fixed model (15 m tall, its trunk always 0.3 m
thick), scattered among the ordinary trees. A last world stage (`BreakableStage`) makes 12% of the pines that
are at least as tall as it into one (`WorldGenConfig` > `breakableTrees`), so it never rises into a sightline the
earlier stages kept clear. It's always cut the same way (stump, logs and caps identical every time). Falling
trees (`ForestEvents`) pick only breakable ones, and the blind one barging through the forest knocks over only
those; every other tree stands. A test checks every seed has plenty, all in place of tall enough pines.

## Ground mist, take three: one layer that hugs the ground

The grid of overlapping mist boxes (each on the ground under its middle) still showed layers: where two
overlapping boxes sat at different heights, their halves added up as two faint layers one above the other. Now
(`GroundMist`) the mist is **one box** whose density is a 3D texture made for where you are (64 x 48 x 64 cells
over the mist's reach), with the mist's height above the ground worked into every column of it from the ground
under that column, so it hugs every hill and hummock in one even layer. It's made afresh every 2 s on another
thread, its patches drifted on (`mistDrift`), and each new one blends in over the last (two volumes, their
thickness traded off so together they're always one layer's worth), so the mist drifts smoothly. Its grid is
fixed in the world (whole cells), so it doesn't crawl as you walk. Seen from high above it's still made taller
and thinner (the same amount of mist), so the fog's slices don't band it. Two fog volumes instead of 25.

## Oaks instead of pines, and getting into tents

- **Oaks**: the forest's living trees are oaks now (`ForestMeshes.Oak`): a stout trunk with root flares, forking
  about a third of the way up into four to six crooked, spreading limbs with side branches, under a broad,
  rounded canopy. The canopy is clumps of leaf cards at the branch ends (and one over the middle), each card a
  cut-out spray of lobed oak leaves made in code (`ForestSurfaces.OakLeaves`: some deep green, some yellowing),
  shaded as rounded masses. Far off, fewer and bigger cards. Heights 10.5-16 m, crowns about 5.5 m out. The
  breakable tree and the landmark tree are oaks too (the breakable one 12 m, its trunk 0.38 m). (Inside, the
  living tree is still called `Pine` in the plan.)
- **Tents**: walking or crouching into an open tent's doorway now crawls you in (as E does: the tent's too low and
  steep-sided to walk about in), and the doorway's aim point is at its middle, where you'd look, rather than at
  its foot, so E finds it.
- **Fuller crowns**: more and bigger leaf clumps (along each limb as well as at its end, and a dome over the
  middle and round its shoulders), more and bigger cards in each (34 close up, 14 mid, 7 far), and a denser
  spray of leaves on each card (130). Its smaller mip levels (seen further off) are made to keep as much leaf
  past the cut-out as the full-size one (`ForestTextures.LeafCut`), so far canopies don't go thin and holey.

## Thinner forest, no landmark tree

The oaks' wide crowns closed the canopy over: now 2100 trees (was 2800), at least 3 m apart (was 2.2 m). The
landmark (the one giant tree, seven times the size of the rest) is gone: `WorldGenConfig` > `landmarkTree`
(off) brings it back.

## Watchtowers: five of them, each in a clearing

- **How many:** five watchtowers now (`WorldGenConfig` > `watchtowers`), at least 55 m apart (`watchtowersApart`).
- **Clearings:** each stands in a small clearing of its own, 12 m across the radius (`watchtowerClearing`).
- **The map:** only one tower has it, on its deck.
- **The drone:** it waits on the deck of whichever tower you reach first, within 18 m of it measured flat. It's no
  longer at the cabin or the car.
- **The model** (`WatchtowerModel`): a proper fire lookout.
  - Splayed timber legs on concrete footings, girts and X-braces with bolt plates.
  - A planked deck on joists.
  - A cab with board walls to waist height and windows above, some panes cracked or gone.
  - A hipped corrugated tin roof with a finial, a lightning rod and an aerial.
  - A floodlight on a bracket at each corner, with conduit to a switch box by the doorway (the button).
  - A fire-finder table, a stool and a hanging lantern.
  - A caged ladder up the west side.
- **The ladder jump:** Space jumps you off a ladder, back away from it (or the way you face, if that's away), at
  `PlayerConfig` > `ladderJumpOut` (3.5 m/s). The ladder can't catch you again for 0.6 s.

## Smoke and breath without boxes

The puffs are see-through quads. HDRP was showing their square edges as faint boxes when a light caught them, for
these reasons:
- It keeps a see-through material's full shine across the whole quad ("preserve specular lighting").
- It gives the quads screen-space reflections.
- It writes their depth and motion.
- Ray tracing sees the whole quad.

`RuntimeMaterials.SetWisp` turns those off for the smoke and breath materials. The puffs also no longer take
shadows, or appear in ray tracing or motion vectors.

## Scours: no eyes through trees, gentler dismissal, slowing, clicking, the eye left behind

- **Eyes through trees:** the soft glow round each eye is a disc wider than the eye itself, so with the eye just
  behind a trunk's edge it showed past the bark. Each eye's glow now fades out while that eye is hidden from you
  (`ScourLook.SeeEyesFrom`).
- **Dismissal:**
  - It shakes the view far less (`ScourConfig` > `faceDownShake`, 0.9°, was 2.5°). `cameraShake` is now only for
    a charge.
  - Facing one down now plays a low, hollow swell of breath (`Sound.Crescendo`, remade, with no high whine).
  - Driving it off plays a soft thump and an airy scatter (`Sound.Disperse`).
- **Slowing:** while you hold one in your light, you move at half speed in any direction (`faceDownSlow`, 0.5).
  It never stops you, so with others behind you, choose when to face one down.
- **Clicking:** while one is out, it makes runs of wet throat clicks (`Sound.ScourClick`). They come every ~14 s
  far off, quickening to ~1.4 s close by, and get louder and lower as it grows (`clickFarInterval`,
  `clickNearInterval`, `clickVolume`).
- **The eye left behind:** one you drive off has a 30% chance (`leftEyeChance`) of leaving an eye (`ScourEyeball`).
  - It's a veined ball trailing a strand of nerve. It floats after you about 8 m off at head height
    (`leftEyeDistance`), going slowly round you and steering clear of trunks.
  - It never stops staring at you, with small darting flicks. Shine your light on it and it blinks: fleshy lids
    snap shut over it, again every half second or so while the beam stays on it, squinting in between. Now and
    then it also blinks by itself.
  - After 30 s (`leftEyeTime`) it stops, its iris kindles red over 2 s, and it shoots up into the sky toward the
    giant.
  - It's harmless. At most 3 at once.

## Watchtower top: balcony, a lookout room with a door, a CB radio; quicker climbing

- **The ladder:** the safety cage is gone. The ladder now comes up to a gap in the railing of a balcony that runs
  all the way round (6 m across).
  - Climbing holds you to the ladder, so you can't slide under the deck.
  - At the top (the `LadderTop` marker), keep climbing and it steps you up and across onto the balcony.
  - From the balcony, walk into the gap and W takes you down, whichever way you're facing.
- **Climbing faster:** hold Shift to climb at 5 m/s (`PlayerConfig` > `climbSprintSpeed`; normal is still 2.5).
  It uses stamina like running.
- **The lookout room:** it sits in the middle of the balcony, 3.4 m across.
  - The west wall is boarded up, with a hinged door in it (E opens and shuts it, the same door as the cabins').
    The other walls have windows above the sill.
  - It's a shelter: outside sounds come through muffled while you're in it.
  - Inside: the fire-finder table, the floodlight switch box by the door, the map on the floor, a hanging lantern,
    and a desk with a stool, a logbook and a CB radio left on.
  - The drone waits out on the balcony now (the `Drone` marker).
- **The CB radio** (`CbRadio`, `Sound.RadioCreep`): a 24 s loop.
  - It plays hiss, with the squelch cutting it off into dead air now and then, and a carrier tone drifting under it.
  - A slow, warbling voice comes and goes but never quite makes words, and once there are three flat beeps like a
    number station.
  - The dial glows amber while it's on. Click it to switch it off or on.
  - From out on the balcony it's muffled, more so with the door shut.

## The breakable tree is a small dead tree; the blind one shatters trees

This replaces the breakable pine/oak described above.

- **The breakable tree** is now one small dead tree: 6.5 m tall, always the same shape and trunk.
  - `BreakableStage` puts it in place of 30% of the dead trees that are at least that tall
    (`WorldGenConfig` > `breakableTrees`).
  - It's the only tree that falls: snapping above a stump, breaking into logs, as before.
- **The blind one** now tramples every tree it walks into, except one with an owl in it.
  - The tree bursts at once into splinters of bark and pale wood up its whole height (`ForestTree.Shatter`,
    `TreeSplinters`), plus a spray of leaves from a living tree's crown.
  - The pieces fly away from it, tumble, bounce, skitter, shrink away and are gone in 1–3 s. Nothing is left
    standing and no logs fall.
  - There's a crack, a crash of wood and a rustle. The blind one ignores the noise, as it did a falling tree.

## Doors easier to use; gentler floodlights

- **Doors (and everything else you use):** whatever the crosshair is right on now wins, any part of it. That
  includes the edge of a door, or an open door, which is a trigger. Before, a door counted only if you looked
  near its middle. Aim assist still picks the nearest thing when the crosshair isn't on anything.
- **Watchtower floodlights:** they were under the eaves, a metre from the railing and planks, and blasted them.
  - Each now sits on an iron pipe up from a balcony corner post, out past everything, with its wire slung back to
    the roof.
  - Brightness is 150 (was 450), the cone 80° (was 95°), the fog glow 0.06 (was 0.12) and the light shafts 0.18
    (was 0.35). These are all on the `Watchtower` component.

## Softer wind and leaves

- **Quieter:** the wind is about 40% quieter and the leaves about half as loud. Both are now settings:
  `AudioConfig` > The forest > `windVolume` (0.45) and `leavesVolume` (0.35).
- **Gentler with the gusts:**
  - The volume follows the gusts more gently and slowly, so it no longer surges in and out.
  - The wind's pitch swings much less (0.94–1.05, was 0.85–1.15), and the swing is smoothed.
- **The sounds themselves are softer:**
  - The wind's upper rush is lower and quieter, with less of the hollow moan.
  - The leaves are a low hush (filtered twice, where before they were a bright hiss), and the rustles swell and
    die slowly instead of bursting in.

## Softer bushes; louder, echoing scour clicks

- **Walking through bushes:** it now plays a sound of its own (`Sound.BushRustle`).
  - It's a soft, low brush of leaves that swells in and fades, with nothing sharp in it. Before, it used the same
    crackly rustle as everything else.
  - It's about half as loud as before: 0.28 walking, 0.5 running, 0.12 crouched.
  - How far it carries to the monsters is unchanged.
- **Scour clicks:**
  - They're louder (`clickVolume` 0.9, was 0.65) and carry further (55 m, was 40 m).
  - They now have reverb: a dark echo off the trees, about a second long, baked into the sound (`SoundSynth.Reverb`,
    a small Schroeder reverb anything can use).

## Scours count their creeps in clicks

- **Clicks on landing:** each time a scour creeps at you, it lands with loud, echoing clicks (`Sound.ScourSnap`),
  about 0.4 s apart.
  - It's one click the first time, two the second, three the third, and so on, up to `ScourConfig` > `maxSnaps`
    (8). The volume is `snapVolume`.
  - The count starts again each time it goes away and comes back.
- **No moving while clicking:** a scour never creeps while it's clicking, whether that's its passive clicks or
  the clicks after a creep. It waits until they and the worst of their echo are done. It also never starts its
  passive clicks in the middle of a creep, or over the clicks after one.

## Falling trees sound real; far-off scours call to each other

- **A falling tree** now has three sounds of its own in place of the old crack, creak and thud. Each echoes off
  the forest.
  - As it gives way (`Sound.TreeBreak`): one loud crack, then the trunk tearing, with a run of splintering cracks
    that quicken through the wood's hollow body, and a deep groaning creak as it leans.
  - As it goes over (`Sound.TreeRush`): the rush of its crown through the air, building as the fall speeds up,
    brushing and snapping through the neighbours' branches.
  - As it lands (`Sound.TreeImpact`): a heavy, deep thud and the crash of branches breaking under it. The trunk
    bounces once, then twigs and leaves settle.
- **Scour clicks** now follow the distance.
  - Far off, a scour calls: a run of clicks every ~5 s (`clickFarInterval`) that carries 80 m.
  - Each other scour that's also far off answers 1.6–3.2 s later, with an 80% chance each (`answerChance`).
  - With no other scour out, something answers from 50–75 m off in the dark 45% of the time
    (`phantomAnswerChance`).
  - Middling close it goes quiet, stalking (every ~13 s, `clickMidInterval`). Close in it quickens as before, down
    to every ~1.4 s.
  - How far out counts as calling is `callBelow` (0.4).

## Phantoms: more detailed, and some with sound

There are seven phantoms now (`World/Life/Phantoms.cs`).
1. **A kitchen chair:** splayed legs, stretchers, a seat worn through to the wood, a curved top rail and
   spindles. It sits a little crooked.
2. **A stop sign:** on a galvanised post with holes up it, leaning. A white-edged red octagon with STOP in white
   letters, bolts, a bare metal back and a rust stain.
3. **A gurney:** a tubular frame on scissor legs and castors, side rails and a raised head end. Someone's shape
   lies under the sheet, with a toe tag on a string, and an empty IV bag hangs on a pole at its head.
4. **An old TV:** a wooden cabinet on splayed legs with rabbit ears, knobs, a speaker grille and a flex trailing
   off.
   - Its bulging screen shows moving snow and throws a flickering grey-blue light (`TvStatic`).
   - It hisses with static and mains hum (`Sound.TvStatic`).
5. **A rotary telephone** on a little round side table, replacing the mailbox. It has a dial with finger holes,
   a handset on the cradle, a coiled cord and a flex.
   - It rings (`Sound.PhoneRing`): two bells struck by a hammer, ringing for 2 s, then 4 s silent.
   - The handset rattles on its cradle while it rings.
6. **The CB radio** from the watchtowers, on an old slatted crate, always on.
   - It has a glowing amber dial, a whip aerial and the handset lying on its cord.
   - It plays the watchtower radio's sound (`Sound.RadioCreep`).
7. **A tricycle:** a spoked front wheel with pedals, two back wheels, a step, a saddle, handlebars with white
   grips and streamers, and a bell.

**Fading:** a phantom's sound fades in over 1.5 s as it appears (`PhantomSound`). When it vanishes it's gone at
once, but its sound fades out over 1.8 s. The sound is muffled when you're under cover.

## More phantoms later; pickup names; the drone on the table; blinds; an arcade cabinet

- **Phantoms come more often as the night goes on.** Going from no lamps lit to all ten:
  - the chance on a quick turn rises from 25% to 60% (`ForestLifeConfig` > `phantomChanceLate`);
  - the shortest time between them falls from 40 s to 12 s (`phantomGapLate`);
  - a slower turn is enough: 80°/s instead of 120°/s (`phantomTurnSpeedLate`).
- **Pickups name themselves** on the prompt: "Take Batteries ×2", "Take Flare gun", "Eat the snack".
- **The drone** now waits on the watchtower's map table (the `Drone` marker). The map lies on the floor south of
  the table (the `Deck` marker), out of the arcade cabinet's way.
- **Blinds:** every watchtower window has a roller blind inside (`Blind`): a roller in brackets, cloth, a bottom bar
  and a pull cord with a ring. Click the window and the blind rolls down over the glass; click again and it rolls
  back up.
- **Arcade cabinet:** it stands in the lookout room's north-east corner and plays Snake (`ArcadeCabinet`,
  `SnakeGame`).
  - The cabinet: a black body with green stripes and trim, a glowing marquee, the screen set back under its bezel,
    a stick and two lit buttons, a coin door with red-glowing slots, and a kick plate.
  - Its green screen lights the room.
  - Left alone, it plays itself (attract mode) and flashes "CLICK TO PLAY".
  - Click it to step up and play: arrows or WASD steer, it speeds up as you score, and Space (or a direction)
    starts again after a game over. E steps away.
  - It beeps, and keeps the best score for the session ("HI").

## Softer canopy rustle

The leaves rustling in the trees are now less than half as loud (`AudioConfig` > `leavesVolume` 0.15, was 0.35).
They also swell less and more slowly with the gusts.

## The constant hiss: rain, the radio, and a Sounds tab to find it

- **Rain (the likeliest culprit):** a light drizzle runs from the start (`WeatherConfig` > `rainAtStart` 0.2), and
  its sound was a bright hiss with drops pattering, never stopping and only muffled indoors. Now:
  - the sound is a soft, dark wash with drops tapping on leaves;
  - its volume follows the rain's strength to the power 1.6, so the starting drizzle is about a quarter as loud as
    before and a downpour is still clear (`rainVolume` 0.4 at the heaviest);
  - under a roof it's 60% quieter as well as muffled.
- **The watchtower's CB radio:** its static is less than half as loud. The voice, carrier and beeps stay as they
  were.
- **Dev window > Sounds:** lists everything playing right now, loudest first. Each line shows the sound's name,
  what it's on, whether it loops, its volume, how far off it is (or "everywhere") and its mixer group, with a
  Mute box. It also shows how muffled you are.

## Watchtower room rework: one long desk, a TV playing Snake, a locker; luminescent spray

This replaces the arcade cabinet and the fire-finder table.

- **The desk** runs the whole east wall (3.2 m), with a shelf under it and a back rail. On it:
  - the CB radio;
  - a logbook;
  - a small old TV with rabbit ears and knobs (`SnakeTv`), with a joystick in front of it on a cord;
  - and a stool in front of the TV.
- **Snake on the TV:**
  - Click the TV to sit on the stool and play. Your view is held on the screen (`ILockedView`: the mouse does
    nothing while you play).
  - Arrows or WASD steer, and the joystick's stick leans the way you steer. Space starts again after a game over.
    E gets up.
  - Left alone it plays itself, and its green screen lights the desk.
- **The locker** stands in the north-west corner: steel, with a louvred door, a handle, a name-card slot and a
  shelf.
  - Click it and the door swings open (`Container` now takes a `door`). You take what's inside.
  - Every watchtower's locker has a can of luminescent spray. The drone waits in the locker of whichever tower you
    come to first (`PlaceFittings` puts the drone in a `Locker` when there is one).
  - The map still lies on the floor south of the middle.
- **Luminescent spray** (`ItemKind.Spray`, `SprayPaint`): a spray can with a glowing green band.
  - F shakes it (a rattle). Hold F to spray where you're looking, up to 3.5 m away.
  - It leaves soft, speckled blots of pale green paint that glow faintly in the dark. Each one sticks to whatever
    it landed on (trunk, rock, ground, wall) and moves and wraps with it.
  - It hisses while it sprays. That carries a few metres to the monsters.
  - A can lasts about 25 s of spraying. The hotbar shows a meter, and the held hint shows how much paint is left.
  - At most 2500 blots at once (the oldest go), and they're cleared on a new run.
  - Settings are in `ItemsConfig` > Luminescent spray: reach, rate, blot size, colour, glow, use per blot, most
    blots.

## Cut ends the right size

When a tree falls, each cut end is capped with a disc of fresh wood. Its size and position were guessed from all
the points where the cut crossed the mesh. On the dead tree, the cut also crosses branches, and those points
dragged the disc's middle off the trunk and swelled it into a big orange circle.

- **Following the trunk:** the cuts are now worked out up the trunk from its foot (`MeshSlicer.TrunkAt` with a
  guess and a reach). Each one only looks near where the last one was, carried on the way the trunk leans.
- **Size limit:** a cut can be at most 1.6× the last one's radius plus 12 cm.
- **Colour:** the fresh wood is a darker, duller brown, so it doesn't glow orange in the flashlight.

## Sounds tab that holds still

Dev window > Sounds now has one row per sound (by name), in a fixed order: loops, then one-shots, each
alphabetical. A row stays once it's been heard, and shows "quiet (N s ago)" when it isn't playing. The numbers
(how many are playing, the loudest volume, how far off, the group) update three times a second, not every frame.

Mute works by name. It silences every copy of that sound, new ones too, and keeps doing so with the window shut
(`SoundMutes`). Unmute all clears it.

## Thunder without the static

The thunder was the hiss. Its rumble was noise through a single gentle filter, turned up 8×, so it kept a lot of
high hiss. With the weather's lightning, the blind one's storm and the giant all playing it, that sounded like
static.

`Sound.Thunder` is rebuilt (about 9 s):
- a first crack: a quick burst of cracks rung through a low filter, not bright noise;
- then the roll: rumble filtered four times over, rolling in seven uneven surges, as the sound comes back off the
  hills and clouds one stretch after another;
- a darker growl in the thick of it;
- and a long, dark echo trailing off.

Measured: about 89% of it is below 100 Hz, 10% is 100–1000 Hz (the body you hear on small speakers), 0.35% is
1–4 kHz, and almost nothing is above 4 kHz.

## Ominous birds, the giant's groan, a glitching intro (to try), sharp sky under the eye

- **Owls and crows** now have a long, dark echo off the trees, built into their calls (hoot, barn owl screech,
  caw). They're played lower and slower everywhere (about 0.7–0.85× pitch), so they sound like bigger birds,
  further off than they should be.
- **The giant groans** 1.5–3 s after the intro's last line (`IntroFinished`). It comes from its direction, far
  off (`Sound.GiantGroan`, about 11 s): a vast, slow voice sinking in pitch, its vowel sliding from "aw" to "oo",
  shuddering, over a sub rumble, with a long dark echo. The view trembles a little.
- **Glitching intro:** `PresentationConfig` > `glitchIntro`, now on by default (off: the old intro).
  - Every intro line is shown in the middle of a dark screen.
  - Between lines the dark tears open: black flickering, bars of black, teal, red and white thrown across the
    screen, and a burst of static. For `glitchPeek` seconds (1) you see through your own eyes in the forest,
    groggy and unable to move. Then it tears shut for the next line.
  - After the last line you wake as usual, and the giant groans.
- **Under the eye:** while the giant's eye is open in the sky, the depth-of-field blur on the far distance clears
  over 2 s, so nothing softens it (`LookController.FarSharp`). It comes back once the eye closes.

## The left-behind eye's entrance

The eye a driven-off scour leaves no longer just appears. Over about 2 s (`ScourEyeball.Arrival`):
1. **Gathering:** about 46 dark motes, like the ones the scour came apart into, spiral back in to a point, faster
   as they close and shrinking into it. The eye condenses out of them as a speck, lids shut, with a dim red pulse
   in it, and there's a low, wet gathering sound.
2. **Forming:** it swells to its size with an overshoot and a shudder, and its nerve strand unfurls behind it.
3. **Opening:** the lids snap open with a loud wet click and a flash of red light from the iris. The pupil starts
   as a thin slit and widens as it locks onto you, while the red dies away.

Then it drifts after you as before. Its 30 s of watching start once it's open.

## The player's breathing, gone

The hiss was the player's own breathing (`Sound.Breath`, played in your head). Removed:
- the breathing every 3.6 s while something's near (the threat monitor's "dread"), the one heard most;
- panting when winded.

Panting is now only seen, in your breath's steam. It still makes the noise that monsters close by can hear, as
before. The two breaths as a scour takes you stay.

## Glitching intro: lines in pairs, and you can move in the glimpses

- **Pairs:** the intro's lines come two at a time in the middle of the dark (`Narration.MiddlePair`). The first is
  on the top row, and the second fades in under it 0.7 s later. They hold together, then both fade.
- **The glimpses:** between each pair, the screen glitches to your eyes for `glitchPeek` seconds (now 2.5).
  - You can move and look round, but blurred and swaying: groggy, `glitchPeekGroggy` (0.6) of the way to just
    woken, easing off over the glimpse (`PlayerController.StartGroggy(seconds, amount)`).
  - You're held still again for the next lines.
- After the last pair you wake as usual, and the giant groans.

## The bunker's doorway opens

The earth heaped over the bunker is a grid of heights half a metre apart: full height over the rooms, dropping to
the ground in front of the doorway. That drop came right at the doorway, so a steep sheet of earth (with a
collider) stood across the opening, inside its frame. The mound now leaves a gap over the doorway and the front's
face. The face and its capping slab fill it from above, and the corridor and room behind are open to walk into.

## Roots only on a few trees, and in the ground

- **Most trees** have no visible roots now. On a slope, the roots on the downhill side stood out of the ground.
- **About 1 in 7 trees** (living and dead) show their roots, and only where the ground is nearly flat:
  - the slope under the trunk is at most 0.1 rise per metre;
  - and the ground a root's length (1.6 m) round the trunk is within 12 cm of the trunk's foot.
- **Into the ground:** on those trees each root bends down from the trunk and dives 40 cm into the soil at its end
  (it was 12 cm). The tree also sits 6 cm lower, so no tip is left in the air.
- **Same trees:** the rooted versions are the same trees as the plain ones, with the roots drawn. Leaving roots
  out doesn't change the rest of the tree (`ForestMeshes.AddRoots` takes the same random turns either way). Which
  trees get them is picked from where they stand, so it's the same every time.
- **Your own tree art** isn't touched. The breakable dead tree has no roots.

## Fallen trees don't bounce light; a drone in every locker

- **No bounce:** the flashlight's bounce light already skipped standing trees (by their layer). A felled tree's
  logs and stump are props, though, so they bounced it back. Now they don't, and nor do the logs lying in the
  forest: anything marked `NoLightBounce` (a new Core component, on them and their children) is skipped.
- **Drones:** every watchtower's locker now holds a drone. Take one from any locker and the drones in all the
  others are gone (`Container` keeps a list of lockers with a drone). A prefab of your own with no locker still
  gets the one drone at whichever tower you come to first (`DroneSpot`).

## One click per creep; the blind one: detailed, locks your eyes, and a real threat

- **Scours** click once, loud, each time they creep at you (the counting-up is gone; `maxSnaps` removed). They
  still never creep while clicking.
- **The blind one's look** (`BlindBody`), more detailed on the same joints, so it walks, listens, roars and leans
  as before:
  - a spine of bony knobs down its hunched back, ribs showing on each side, shoulder blades jutting, a sunken
    belly, swollen hip bones, and tatters of skin hanging from its back and arms;
  - knobbed vertebrae up its neck;
  - a swollen skull with a ridge down it, a row of glowing pits on each side, and great thin ear membranes on
    bony spines (it hunts by sound);
  - teeth lining the split mouth;
  - knobbed elbows and knees;
  - hands with four long two-jointed fingers and a thumb, all clawed;
  - and long feet up on their toes (it walks the way a dog stands), clawed.
- **Your eyes are dragged onto it** as it breaks out of the ground (`PlayerController.LockGaze`), and held there
  through its emergence and roar (about 10 s, the mouse doing nothing), then let go.
- **A real threat.** Before, roaming it wandered at 1.5 m/s round wherever you'd been. Outrun it once, past its
  hearing, and it searched the wrong spot, then dawdled, and only caught up after 60 s more than 90 m off. Now:
  - **It follows your trail.** It remembers where you've been (a point every 2 s, the last 2 minutes) and walks
    after you, where you were 18 s ago (`trailBehind`), at 2.4 m/s (`trailSpeed`), 12% quicker for each lamp lit
    after it came (`trailSpeedPerLamp`).
  - **It stops dead and listens hard** every 20–40 s (`minHardListen` / `maxHardListen`), with a deep click.
    Anything moving within 30 m (`hardListenReach`), even crouched, and it rushes you.
  - **It catches up sooner:** more than 70 m off (`lostDistance`, was 90) for 30 s (`lostTime`, was 60), and it's
    35 m behind you.

## Hold Esc to skip the intro

While the intro runs (its lines over the dark, the glitches, and, in the old intro, the lines along the bottom
after you wake), "HOLD ESC TO SKIP" shows faintly bottom right, with a bar filling as you hold. After 1.2 s:
- the lines and glitches stop;
- you're awake in the forest after a short 0.8 s fade, a little groggy for 3 s;
- and the giant still groans (`IntroFinished`, sent only once).

Esc doesn't open the pause menu while the intro runs (`MainMenu.IntroRunning`). A tap does nothing.

## Drone: placement ghost, the remote with its LCD, and a beacon

- **Ghost:** holding the drone (not yet out), a see-through drone shows where it'd be set down. That's where
  you're looking, on the ground or something flat within 4 m, or just in front of you. It's pale blue where
  there's room and red where there isn't (a trunk, a wall, too close to you); F there says "No room to set it
  down there." (`Drone.PlacementSpot`, `Hotbar.DroneGhost`.)
- **The remote:** once it's set down, its hotbar and inventory icon becomes a controller (`ItemIcons.For(stack)`),
  and you hold its remote: a dark handset with grips, two thumbsticks, a red and a green LED, a stubby aerial, and
  a small LCD, tilted up toward you (`ItemModels.Remote`). F flies it and F brings your eyes back, as before.
- **The LCD** (`DroneRemoteScreen`): a green-backlit radar.
  - You're the cross in the middle, the way you face is up.
  - The drone is a blinking dot where it is from you, inside a ring for its range. Every 1.8 s it pings: a ring
    spreads out from it, with a soft beep.
  - Its battery bar and four signal bars run along the bottom. A cross over it means no signal.
- **Beacon:** a red light on top of the drone blinks briefly every 1.2 s while it's up (powered and off the
  ground), a glow you can see across the sky, with a little light round it.
- **Fixed:** the luminescent spray can stood on its side (the helper it used lays cylinders along +Z). It's
  upright now.

## The blind one's music, from the HDRP build

The blind one's theme is now the HDRP build's (its `BlindEntityTheme`), ported to `Audio/BlindTheme.cs`. It's the
same synthesis, at this build's 24 kHz with the reverb's delays scaled to match, and plain C# so it's made off the
main thread with the rest of the music. It's scored to its entrance:
- **0–3.5 s:** a sub-bass drone on low D creeping in under a rising, whining cluster as the ground rumbles.
- **3.5 s:** a huge drum hit as it breaks out of the earth.
- **7.6 s:** the roar: a hit and a blaring, dissonant brass chord (D, Eb, A, G#) opening up and dying away.
- **13.1 s:** two stamps.
- **15 s on:** the hunt: a limping drum rhythm like its gait, a driving low cello ostinato, a thin shivering tremolo
  of high minor seconds, and its clicking, all building.

It's made once (80 s) and cut in two for the music manager:
- the intro is the first 67.8 s, the entrance and the building hunt;
- the loop is the five bars of the hunt at its fullest (55.8–67.8 s, cut on bar lines). It carries straight on
  from the intro's end, on the same deck, and loops on itself with its start faded over what follows its end, so
  there's no seam.

Measured: the loop's end and the intro's end match in level, and it starts on the next downbeat.
`MusicTests` now expects 67.8 s and 12 s.

## A storm that builds with every lamp

There's now one storm level (`Weather.Storm`, 0 to 1). Each lamp lit, it climbs toward the next step slowly, over
about a minute (`WeatherConfig` > The storm > `stormBuildTime`, 75 s). It goes from calm at no lamps to its
height at all ten, slow at first and wilder toward the end (`stormCurve` 1.3). It drives:
- **Rain:** from 0.15 at the start to 1 at the storm's height (`rainAtStart` / `rainAtFull`), easing as the storm
  does. It no longer jumps at the 5th and 7th lamps (those settings are gone).
- **Wind:**
  - the steady breeze rises to `stormBreeze` (0.6);
  - the gusts reach `stormGusts` (2.4×) as strong, roll through faster (`stormGustSpeed` 17 m/s) and come closer
    together (`stormGustSpacing` 42 m), so the trees and grass lash about;
  - the wind's sound rises up to 80% louder with it, and the canopy's rustle up to twice as loud. (The old 4% per
    lamp is gone.)
- **Leaves:**
  - lighter gusts lift them off the ground (a push of 0.35 at the height, was 0.9 always) and up to five times as
    often;
  - and the falling leaves are carried further by the stronger wind (`Wind.Storm`).
- **Lightning** comes more often, up to 3× as often at its height (`stormLightning` 0.3).

## Trying out: the blind one teleports in lightning

`BlindConfig` > Teleporting > `teleports` (on). Untick it for the trail-walking blind one from before.

- **It doesn't walk about.** It stands where it came in, still, head cocked, listening, with a low click now and
  then. It still listens hard every 20–40 s. After 7–14 s (`perchMin` / `perchMax`) it goes.
- **Going:** it convulses for half a second (twitching, arms up, head jerking, a low shriek). Then its own red
  lightning strikes it, a thick bright bolt straight down, with a blinding red flash, a crack and thunder at once.
  It's gone, a red glow fading where it stood. The view shakes, harder the nearer it was.
- **Gone** for 1.5–4 s (`goneMin` / `goneMax`): nothing, but its storm cloud drifts overhead to where it's coming.
- **Coming:** another strike, 16–38 m from you (`teleportNear` / `teleportFar`), and 40% of the time where you're
  looking (`teleportInView`). Never in a light or on a building. It's there in the glare, hunched low, rising,
  with a thud you feel and a low moan. Then it stands and listens.
- **Only when it hears you** (a noise in range, moving within 4 m of it, or moving within 30 m while it's
  listening hard) does it run at you on foot, as before. It then searches where it heard you, and goes back to
  teleporting.
- `Storm.StrikeAt` puts a bolt on a chosen spot: brighter, thicker and longer than the storm's own, with its
  thunder at once.

## The locker opens; what's in it is picked up

The watchtower's locker is no longer searched all at once. Click it and its door swings open (click again to shut
it; `LockerDoor`). Inside, lying where you can see them, to take one by one:
- on its floor, **a drone**: one in every tower's locker. Take one and the drones in the other lockers are gone
  (`Pickup.OneOfMany`);
- on its shelf, **a can of luminescent spray** and **three batteries**.

The locker's sides, back, top and bottom, and its door, each have their own collider, so with the door open you
reach the things inside.

## Drone remote raised into view

The held remote sat below the bottom of the screen. It now has its own spot, `Items > Hand > remoteOffset` (default 0.16 right, -0.15 up, 0.42 forward from the eye), which is higher and nearer the middle than the other held items. It updates live, so you can nudge it while playing.

## Drone hovers, light static, longer range

- When you press F to leave the drone, it now hovers where you left it, bobbing slightly. Its motors keep running, so the blind one can still hear it, and its red beacon keeps blinking. It uses battery at `droneHoverDrain` (0.35) of the flying rate, and it falls if the battery dies. To pick it up you need to land it, or be able to reach it. If you've walked out of signal range, F says "No signal" until you get closer.
- The drone's picture always has a little static (`droneStatic`, 0.12) plus short random bursts. The static still gets heavier as the signal fades.
- `droneRange` is now 260 m, up from 140, and the slider goes to 600.

## Strike scorch and softer lightning bloom

- The blind one used to leave a round red ball of glow where it struck. Now it leaves glowing burn lines on the ground (StrikeScorch.cs). Six to eight lines branch and fork outward 2–3.6 m from the spot, following the terrain. They glow red-hot and flicker, with a dim red light just above the ground, and they cool and disappear over 4 s.
- Lightning bloom reduced:
  - The blind one's red bolt: `boltGlow` is now 2.5 (was 8). A strike on the blind one is only 1.36× brighter, where it used to be 2.2×. The bolt is never wider than 0.45 m.
  - Weather lightning: `boltBrightness` is now 12 (was 30).

## Giant: higher, bigger, walking, ground shake, shockwave; a new eye

- **Hanging:**
  - The giant is 740 m tall (was 660).
  - It hangs higher: its feet are 15° above the horizon (was 10°) and its neck 45° (was 38°), which puts it about 870 m away.
- **Walk:**
  - Rewritten so it reads from far away. The whole body sinks as each foot lands and rises over the planted leg, rolling toward it. The head nods on each footfall, the long arms swing against the legs with the elbows bending, and the knees bend through the swing and straighten as the foot lands.
  - A stride is now 8 s (was 12).
  - The thud now lands when a foot actually comes down. Before, it fell midway through the swing.
  - It still never reaches you.
- **Ground shake:**
  - Each step makes the ground heave under you through a new `PlayerController.AddRumble(amount, seconds)`. The view drops, then a deep tremor dies away over `stepRumbleTime`.
  - Strength is `stepRumble`, a third as strong when the giant is far off.
  - The random jitter from `stepShake` is lowered to 2.5.
- **Landing and shockwave:**
  - A cloud of dust bursts up around its feet as it lands, then settles over about 20 s (`landingDust`).
  - The shockwave now drives a visible wall of dust out across the forest (new `Game/ShockDust` shader on a `DustRing`; `shockwaveDust`, `shockwaveHeight`, `dustColor`).
  - When the wave reaches you, the ground heaves (`shockwaveRumble`), the view is knocked about, and there is a roar of thunder, boom and rushing wind. You're dazed for `shockwaveDaze` seconds, and the trees still whip over as before.
- **The eye:**
  - Rebuilt to look like the reference painting, using a new shader `Game/SkyEye` on one quad.
  - The eye: a dull mauve-brown iris with fine radial fibres, a paler ring around a large pupil, a dark outer rim, a glint low in the pupil, and the white barely showing.
  - The lids close from above and below.
  - Around the eye, the clouds are drawn into slowly turning rings, lit warm from below near the eye and dark blue-grey further out. A pale violet lid fold arcs above it.
  - The clouds gather before the eye opens and drift apart after it closes. The pupil starts wide, narrows onto you, then makes small darting looks.
  - `eyeSize` (15°) is the width of the opening. `eyeGlow` (now 1) is the brightness. `eyeSwirl` sets how fast the clouds turn.
  - If the shader can't be used, the old red eye is drawn instead.

## Scours kept out of trees; realistic crows

- **Scours and trunks:**
  - Before, scours were only checked against trees when they first appeared. Creeping, advancing, circling, charging and the walk-up when they grab you could all put one inside a trunk.
  - Now every move goes through `OutOfTrunks`, which pushes the body (a capsule about the scour's size) just clear of any trunk or prop it overlaps.
  - A creep with no room near its landing spot looks within about 2 m (`FindRoom`). If there's still no room, it skips that creep and tries next time.
  - Hiding spots are worked out from the trunk's collider, not the tree object's position (the two can differ). Only standing trunks are used (tall and narrow), never logs or stumps, and a trunk is only used when there's room behind it.
- **Crows:**
  - Rebuilt. A call is now a series of 2–4 harsh caws, a little under half a second apart, the last one longer and lower.
  - Each caw is a rough, strained voice: sharp pulses wavering in timing and strength, every other pulse weaker for the rattle, and breath through it. Its pitch jumps up at the start then sags (about 480 Hz). It's shaped by broad throat resonances around 1.3, 2.4 and 3.6 kHz, with a little forest echo.
  - Playback pitch raised to 0.85–0.97 (was about 0.7), so they sound like crows rather than something slowed down.

## Blur until the drop, bigger eye, the chain snapping

- **Background blur:**
  - It now stays on while the eye stares.
  - It lifts in about 0.4 s the moment the chain snaps, so the fall and landing are sharp.
  - Once the giant is up and walking, it comes back over about 4 s.
- **Eye size:** `eyeSize` is now 28° (was 15). The slider stops at 34°, because past that the swirl's corners reach the camera's far clip and get cut off.
- **Chain snap:**
  - There's a new sound, `ChainSnap`: a huge crack, a thump, the deep clanging ring of the parted links, a rush of air as the chain whips back, and loose links clattering, then echoing off the hills.
  - The camera jolts and the ground rumbles.
  - The broken chain whips back up, lashing side to side as the motion dies away, and is hauled up into the dark over about 3 s. Its fade into the sky goes with it.
  - A bright flash shows where it parted.
  - Nine loose links are flung out and tumble down after the giant, spinning.
  - The broken stub on the giant's collar shows from the moment it snaps.

## Watchtower floodlight lever; real light from the lamps

- **The lever:**
  - The red button is gone. There's now a knife-switch lever on the switch box by the door (steel arm, black grip) that you click to flip. Down is on, up is off, and it swings over.
  - The floodlights run on a charge: 60 s of light (`onTime`), which drains while on and refills while off (`rechargeTime` 120 s from empty).
  - They need 25% charge to switch on (`minCharge`). When the charge runs out they flicker for the last 4 s, die, and the lever kicks back up.
  - A small lamp on the box glows green when there's enough charge and pulses amber while it refills.
  - Switching on still drives off the giant and the flier and makes the tower safe. To stop the lever being spammed, hunts reset at most once every 20 s.
- **Light:**
  - Each lamp face now has a bright glare you can see from the forest.
  - Brightness is 700 cd (was 150) with an 80 m reach.
  - The fog they light (`fogGlow` 0.035) and the soft beam shafts (`beamGlow` 0.06) are turned down, so the lit ground stays clear when you look in from outside the beam rather than being hidden behind haze.

## Bunker doorway, actually open

- **Why the last fix didn't work:** the mound's heights were right, but its points sit 0.5 m apart. The last row at full height was at z 3.1 and the first ground-level row in front was at z 3.6, and the bunker's front face is at z 3.5. So the slope between those two rows ran from 3 m high down to about 0.5 m inside the door frame: a dirt ramp blocking the doorway. That slope also had a collider.
- **The fix:** the mound now leaves out every square that reaches onto the front face or in front of it (`OverFront`). The front wall and its capping slab close what's left above.
- **Checked offline:** I sampled the mound surface inside the doorway and corridor (|x| < 0.7, z > 1.5, below door height). 217 points were inside before the fix, and 0 are now.

## Session timeline: judging the pacing

It's hard to judge whether horror works when you know every scare. The session recorder now also keeps each run as a timeline and saves it as an HTML page to open in a browser.

- **What it records:** twice a second it notes:
  - what each threat is doing: away, out/watching, hunting you, or taking you. The giant walking far off counts as background, not pressure.
  - the threat level (calm / dread / hush);
  - how you're moving: still, walking, running, crouching, being taken, or flying the drone;
  - the flashlight, and whether you're in the open, in a light, or under cover.

  It also marks lamps lit, being caught, things appearing, hunt resets, floodlights, boss themes, and scares. Scares come from a new `ScareHappened` event that phantoms and falling trees now publish.
- **The page** (no scripts, works offline):
  - Summary cards:
    - **Hunted:** the share of the run with something coming for you.
    - **Under pressure:** the share with something out, or the dread/hush on.
    - **Overlap:** the share with two or more threats out at once.
    - **Quiet:** the share with nothing going on, plus the longest quiet stretch and how many lasted 30 s or more.
    - **Between beats:** the typical and longest gap between scares or arrivals.
    - Counts of scares and catches, and how much you stood still.
  - A pressure curve in 10 s bins, then one lane per threat and player state. Lamps are drawn as numbered lines down through everything, and you can hover anything to see what and when.
  - A lamp-by-lamp table.
- **Where to find it:**
  - It's saved to `SessionLogs/Timeline_<date>.html` when Play stops, and for each run when a new run starts.
  - The dev window's Session tab (F11) shows this run's pacing so far and has an **Open timeline** button that saves it and opens it in your browser.
  - It's only in the editor and development builds, like the rest of the dev tools.
- **How to read it:** look for contrast. Long flat stretches of pressure, a lot of overlap, or short gaps between beats usually mean the fear is going numb. Long stretches of standing still can mean fear or being lost; watch someone play to tell which.
- **Code:** `DevTools/SessionTimeline.cs` (the model and the numbers, plain C#), `DevTools/TimelineReport.cs` (the page), `DevTools/SessionRecorder.cs` (sampling and saving). Tests are in `Tests/EditMode/SessionTimelineTests.cs`.

## The silence: snap, fall, blast, ringing

- **The sequence:**
  1. **Fade to silence:** starting `silenceLead` (4 s) before the chain snaps, all sound (music and every effect) slowly fades to nothing.
  2. **Heard alone:** in that silence you hear only the chain snapping, the giant hitting the ground (boom, a heavy step, a low thunder) and then the shockwave arriving.
  3. **The shockwave hits:**
     - A high ringing starts in your ears: two tones around 3.7 kHz, a few hertz apart so they beat, with a faint octave above and a little hiss (new sound `EarRing`).
     - The world fades back in over `hearingBack` (9 s), starting dull and muffled and slowly clearing, while the ringing thins away (`ringVolume` 0.35).
- **How it works:**
  - The mixer only exposes Master, Music and Effects volumes. A new `Hearing` service fades the Music and Effects faders on top of the player's own volume settings (`AudioStateController.WorldVolume`). The moments that must cut through play on the UI group, the only one outside those two.
  - Deafness (`SoundPlayer.Deafened`) runs through the same low-pass filters used for muffling under cover, on every pooled sound and every looping source that uses `Muffled`. UI sounds aren't affected.
  - Safety: hearing is restored at once on a new run, and it comes back on its own if it's ever left silent for 25 s.

## Eye: old saved values replaced, bigger, dimmer

- **Why it still looked small and blown out:** once Unity has saved a config asset, the values in the asset win over the code's defaults. A GiantConfig saved before the new eye kept eye size 9° and brightness 8, which drew a small blob bright enough to bloom white.
- **Upgrade on load:** `GiantConfig` now records which defaults its values were saved under (`savedUnder`, hidden). When an older asset loads, it replaces the values changed since then (height, feet and neck angles, eye size, eye brightness, stride time, step shake) once, then marks the asset to be saved. Values you set after that are left alone.
- **Bigger:** `eyeSize` defaults to 38° and can go up to 60°. When the swirl around it would reach past the camera's far clip, the eye is drawn nearer (`EyeDistance`): 360 m up to about 30°, 328 m at 38°, 223 m at 60°. It's untouched by fog, so the distance doesn't change how it looks.
- **Dimmer:** `eyeGlow` now defaults to 0.6.
- **If it still shows as a plain glowing ball:** that's the fallback eye, used only when the `Game/SkyEye` shader can't load. The Console will show a warning saying the shader is missing or has errors.

## Giant always blurred; a bigger, scarier flier and its cry

- **Blur:** the far blur now stays on the giant the whole time, including while it falls and lands. Nothing sharpens it any more (`FarSharp` is held at 0).
- **The flier's body**, rebuilt for its outline (it's mostly seen against the sky):
  - A starved, bat-winged thing with a lean body, a ridged spine, and a long neck curving down to a skull with a long, open, toothed beak and two horns swept back.
  - Wings of torn membrane stretched between four long finger bones that end in claws past the membrane, with a hooked claw at each wrist. The leading edge arches forward, and the trailing edge is scalloped, nicked and ripped.
  - Long legs trailing under it with hooked talons, and a whip of a tail ending in rags.
- **Animation:** the fingers lag the arm and fold on the upstroke, the tail whips, the legs swing, and the head hunts about.
- **Size:** `wingspan` is now 66 m (was 40). `FlierConfig` upgrades older saved values once, the same way `GiantConfig` does.
- **Arrival cry**, a new sound `FlierCry`:
  - a rasping breath drawn in;
  - a scream from several throats at once, detuned, cracking and rattling, with a sub-octave under it, sliding formants, and driven hard;
  - a slow guttural clicking as it dies away, with a big forest echo.

  The old shriek still plays quietly under it, slowed down.

## The flier: the HDRP build's bird

- The built-in flier is now the bird from the `hdrp` branch (`FlyingEntity.Build`), ported part for part:
  - a heavy body and a long crooked neck;
  - a narrow head with a long hooked beak and one great eye under the brow;
  - wings made of an arm and a hand, with six secondaries hanging off the arm and seven ragged primaries splayed like fingers at the tip;
  - a fanned tail of seven feathers, and legs with three-clawed talons trailing at 55°.
- Its parts were in metres for a bird about 48 m across. Here they're scaled to one unit across (`K = 1/47.8`), and the flier scales that to `wingspan` (66 m). Its random feather lengths come from a fixed seed, so it's the same bird every time.
- Wing animation is the HDRP build's: slow, heavy beats, the hand lagging the arm, wings swept back while diving, and the body heaving with each beat.
- The eye glows (`FlierBody.SetEyeGlow`), burning in the beam's colour while its searchlight is on and a dull red ember otherwise.
- The bat-winged body from the previous change is gone.

## The lone door: grander, a ring of ten stones, and it answers the lamps

- **The door** (built-in) is now:
  - a flush stone threshold slab;
  - fluted wooden posts on stone plinths with stone capitals;
  - a heavy lintel and stone cornice, with an arch of nine wedge stones over it, a keystone standing proud, and a dark tympanum under it;
  - the door itself with four raised panels each side, two long iron straps with studs, a brass ring knocker on its boss, and a brass keyhole plate.

  The threshold is flush so the doorway, its window into the dark room, and the room's own doorway all still start at the ground. The keyhole is named `Keyhole` so the light can shine through it.
- **The ring** (`World/Places/DoorShrine.cs`): ten rough standing stones, 6 m out and none straight in front of or behind the door. Each is a jittered, tapering slab, leaning a little and sunk into the ground, with moss on its back. A different rune is cut into the face it turns toward the door. Stones lit = lamps lit; each lit stone's rune glows a cold pale blue (`glow`, `runeGlow`), with a small light on the ground (`stoneLight`).
- **When a lamp is lit:**
  - The next stone wakes with a new `StoneWake` sound (a grinding rumble deep in the rock, then a cold, glassy, detuned chord and a breath of air). Its rune flares, then settles.
  - The door is struck from the other side: once, twice from the 7th lamp, three times at the 10th. Each blow plays a new `DoorThud` sound (a deep thump, boom, and the door rattling in its frame) and the door shakes in its frame while shut.
  - More light seeps out along its foot and through the keyhole each time, on both sides (`doorLight`), pulsing with each blow.
- **At ten:** every stone flares, and the light pours out round the door as it unlocks.
- **Your own door art:** the ring and the light at its foot work round a custom `door` from WorldArtConfig too. The rattle needs a child named `Door`, and the keyhole glow needs one named `Keyhole`.
- **Clearing:** the door's clearing is 9 m now (was 5) so the stones stand clear of the trees. World-gen tests pass, and no seed is short of structures.

## The door opens with the lamps; the giant's chain back

- **The door, lamp by lamp:**
  - It creaks a little further open with each lamp lit: a crack after the first, about a hand's width after a few, a third of its swing by the ninth (`DoorShrine.ajarAtNine` 0.33).
  - It stays locked and solid at every step, so there's no way in. Through the gap you get a glimpse of the dark room: its window now shows while the door is ajar, not only when it's open.
  - At the tenth lamp, after the stones flare, it swings fully open by itself, slowly.
- **`Door` changes:**
  - `ajar` (and `SetAjar`, which creaks there over `creakTime` with a creak sound) is where the door rests while it isn't opened. It's solid at rest there.
  - `Rattle(seconds, degrees)` and `SwingOpen()` are now part of the door, so the rattle from each blow and the slow opening can't fight over its rotation.
  - Opening and closing it yourself still take `swingTime`.
- **The giant's chain:**
  - The chain only draws up to the camera's far clip (600 m).
  - After the giant was hung higher, his neck was drawn 509 m away, which left the chain 58 m before the clip. It fades into the sky from 30% of its length, so it was gone.
  - The giant is now drawn at 290 m (was 360), scaled down to match so he looks exactly the same size. That leaves the chain 180 m, a little more than before he was raised.

## The blind one's voice: no more 8-bit death sound

- **What was wrong:** the blind one screamed with the shared `Shriek`, a clean sine sweeping from 1400 down to 700 Hz with vibrato. Played at a third of its pitch, that's the classic falling "pew-wowww" of an 8-bit death sound. It growled with `Moan`, a near-pure harmonic hum.
- **The new sounds:** both come from one beast-voice generator (`SoundSynth.Throat`) with no clean note anywhere in it:
  - irregular low pulses that jitter, with every other pulse weaker for the rattle, and that sometimes catch and drop out;
  - torn breath through them, the broad formants of a big mouth, and a sub-bass weight under it;
  - driven hard, and shuddering.
- **`BlindRoar`** (2.4 s): its scream (a flash in its face, arriving, shattering trees, going), with a strained, noisy scream riding high over the first half and a big echo.
- **`BlindGrowl`** (1.7 s): lower, closer and wetter, a slow clicking rattle with no scream. It plays when it hears you and rushes, when it comes in, and when it takes you.
- **Check:** the roar's spectral flatness is 0.185, against the old shriek's 0.052, so it's breath and rasp rather than a tone.
- `Shriek` and `Moan` are untouched for anything else that uses them.

## Scarier crawlers

- **Body:**
  - A pale, damp, too-human head lying low: a long skull, sunken dark sockets with the eyes deep in them (they still shine in your beam), and a mouth gaping far too wide with teeth top and bottom.
  - A hanging jaw, and 18 long black strands of hair falling from the crown over the face to the ground.
  - Four long arms whose elbows jut up high over the head like a spider's, coming down to palms with four long splayed fingers on the ground.
  - The skin is pale (it was near-black), so you actually see it in your flashlight.
- **Behaviour:**
  - Its face turns to follow you within 9 m.
  - Its head twitches sideways and back now and then, much more often while it's in your beam.
  - In your light it goes still and only its head moves. Out of it, it scuttles in quick bursts and stops, rather than crawling steadily.
- **Sounds:**
  - `CrawlerScreech`: a ragged, cracking, almost human throat with breath tearing through it. It's used for the leap, on your face, and when it's thrown off, replacing the slowed sine-sweep `Shriek`.
  - `CrawlerSkitter`: long nails ticking over the ground in an uneven patter, with dry leaves, as it moves.
  - `CrawlerBreath`: a wet, rasping breath in, a catch, and a long gurgling breath out, when you're within 5 m.

## The blind one's theme: back on top when it hunts

- **Why it was hard to hear:**
  - The theme itself is loud: about twice the level of the mood tracks.
  - The music manager plays the theme of whichever creature *started* most recently. The blind one's starts when it arrives (lamp 3), the giant's replaces it when the giant lands (after lamp 5), and the flier's replaces that (lamp 7).
  - So unless you lingered between lamps 3 and 5, you heard little of it, and never again once the giant was down, even with the blind one right on you.
  - (The mixer's Hush snapshot takes the Music group to -40 dB, but Music's volume is an exposed parameter set from your settings, so snapshots don't touch it. Hush also only happens within grabbing range.)
- **The fix:** a new event, `BossThemeFocus`. The blind one publishes it when it starts rushing at you and when it strikes in after a teleport.
  - The music manager then puts its theme back on top, going straight into its hunting loop rather than the slow intro, and fading over 1.2 s.
  - Nothing happens if its theme hasn't started yet.
- **To check what's playing:** the dev window's Game tab (F11) has a **Music** row showing the mood and the track, e.g. "Danger (Blind theme)".

## The blind one is now the Tall Listener

From the 3D mock-up (Blind One Studies), built into the game (`Entities/Blind/BlindBody.cs`):

- **Body:**
  - A starved, hunched giant: hips, a gaunt belly, a spine curving forward with knobs down its back, ribs showing.
  - A smooth, eyeless head, with skin over where the sockets would be, rows of faintly glowing pits it hears through, nose slits, a lipless toothed mouth and jaw, and two vast scalloped ear membranes ribbed with bone.
  - A 16-vertebra neck, folded down over bare shoulders.
  - Its arms hang from a yoke of bone just under its head, so they go wherever the head goes. Long fingers end in claws.
  - It's built in the mock-up's metres (6.1 m stood) under a model scaled to one unit tall, so `height` still sets its size.
- **Legs:**
  - Two-bone legs whose bones lengthen.
  - Each foot is planted in the world where it lands, on the terrain. One at a time lifts, draws up, reaches far ahead and sets down, and the knees bend to reach the feet.
  - Standing still, a stray foot steps back under its hip. The body dips as a leg reaches out.
  - While it's still down in the ground, breaking out, its feet stay level with its body so its legs don't poke up through the surface.
- **Neck:**
  - `stretch` lifts the head `neckReach` (16 m) above its shoulders, up over the trees, with a slow S of a sway. Up there the head looks round, stopping dead.
  - Listening, it cocks its head and snaps toward sounds, ears swivelling.
  - Roaring throws the head up and spreads the arms.
  - Taking you, the neck curls right over and down so its face comes to yours (the camera follows `HeadWorld`).
- **Striding** (`BlindSettings.strides`, on), in place of the lightning teleport:
  - When it moves on, its legs grow to `strideGrowth` (3.6) times their length over `growTime` (2.5 s).
  - It walks to its next spot at `strideSpeed` (5 m/s) in huge, slow steps, then its legs shrink back down.
  - It still hears you all the way, and if it rushes you mid-stride its legs shrink as it comes.
  - Turning `strides` off brings the lightning back.
- **Footsteps:** every foot that comes down (`BlindBody.FootDown`) is a heavy step at that foot.
  - It's louder, deeper and heard further the bigger it's grown (60 m to 200 m).
  - The camera shakes, and grown steps also heave the ground (`AddRumble`).
  - A grown foot crushes the trees it lands on.
  - Your own model still uses the timed steps.
- **When the neck goes up:**
  - while it stands waiting, from 1.5 s in until 3 s before it moves on;
  - all through a hard listen, which now lasts 3× `listenTime` so the head has time to rise and look round;
  - a little during an ordinary listen.
