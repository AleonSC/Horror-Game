# M3: the generated world

Goal: the whole 320 m forest, generated from a seed, wrapping at the edges. The plan's check for this
milestone is 60 fps at 1080p across the whole map, in a build.

## 1. Pull and build the scene

1. GitHub Desktop: **Fetch origin**, then **Pull origin**. Open the project and check the Console for red errors.
2. Top menu: **Game > Build Forest Scene**. It makes `Assets/Scenes/Forest.unity` (and puts it first in the build).
   This scene is the real game scene, so **commit it**.
3. Press **Play**. The world is generated (the Console says how long it took), and you wake in the start clearing.

Every Play makes a new world, because GameSetup has **Randomize Seed Each Run** on. To keep one world, turn
that off and set **Fixed Seed** (`Assets/Config/Resources/GameSetup`). **F9 > Game > Restart run** also makes a
new world.

## 2. What's in it (all from section 3 of the plan)

- **Ground**: rolling waves plus 4 hills (some stretched into ridges; few trees on top, so they're lookouts),
  3 ponds, puddle spots.
- **The start clearing** in the middle, the biggest clearing. You face a set direction, and the giant's
  bearing is within 12° of it: no tree or hill hides it from where you wake. The giant itself comes in M5.
- **10 lamps**, each in its own 12 m clearing with tall grass and open sky above. Crank them as before.
- **Structures** as grey boxes for now: the cabin, 2 shacks, 3 campsites, the watchtower, the car, and the
  giant landmark pine. There's also **the lone door**. Their insides and uses come in M4/M5.
- **14 glades** full of tall grass.
- **Trails** linking the lamps (darker packed earth, petering out in places), a patch of trodden earth under each lamp (the trails run out of it),
  and paths up to the structures.
- **The forest**: about 2,600 trees (pines and dead ones, a few old giants), 1,400 bushes, about 30,000 grass
  clumps, rocks, boulders and logs.
- **Wrapping**: walk off one edge and you come back in on the other, with nothing jumping in view.

## 3. Tools

- **F9 > Game > World**: the seed, how much is drawn and in how many draw calls, and how long each
  generation step took.
- **Map overlay**: in Play, select **World Builder** in the Hierarchy and look in the **Scene** view. It shows
  the clearings (green start, orange lamps, light green glades), the giant's bearing (red), trails, ponds,
  structures, the door and the battery spots (yellow).
- All the generation numbers are in `Assets/Config/World/WorldGenConfig`.

## 4. What to tell me

- Any red errors, and the frame time (F9) while walking around: in the forest, in a glade, near a lamp.
- How it feels to walk: too dense, too sparse, too flat, too hilly?
- Anything that looks broken: floating or sunken trees, holes in the ground, seams between ground tiles,
  trails going the wrong way.

## Not yet (on purpose)

- **Wind**: grass and trees don't sway yet. It needs a custom shader. Everything is already drawn in a way
  that lets wind be added later without rebuilding anything.
- **Structures** are grey boxes, and **batteries** aren't placed yet (their spots are planned). Both come with
  the items and shelters in M4.
- **Your Tree9 pack** isn't used yet. The trees are the built-in shapes. Tree packs come with the asset-pack
  support (section 10.2 of the plan).
