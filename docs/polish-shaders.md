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
- **Glitching intro:** `PresentationConfig` > `glitchIntro`, off by default.
  - Every intro line is shown in the middle of a dark screen.
  - Between lines the dark tears open: black flickering, bars of black, teal, red and white thrown across the
    screen, and a burst of static. For `glitchPeek` seconds (1) you see through your own eyes in the forest,
    groggy and unable to move. Then it tears shut for the next line.
  - After the last line you wake as usual, and the giant groans.
- **Under the eye:** while the giant's eye is open in the sky, the depth-of-field blur on the far distance clears
  over 2 s, so nothing softens it (`LookController.FarSharp`). It comes back once the eye closes.
