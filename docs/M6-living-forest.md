# M6: the living forest

M6 comes in parts, pushed one at a time; this guide grows as they arrive. Pull, open `Assets/Scenes/Forest.unity`
and press **Play**.

## Part 1: wind and the forest's sound

- **Wind:** grass, tall grass, bushes and trees all sway. There's a steady breeze, plus gusts you can watch
  roll across the forest in waves. They get a little stronger with each lamp lit.
  - **The giant's shockwave** now whips everything over as it passes.
  - Numbers: `Assets/Config/World/WeatherConfig` > Wind. Change them during Play and you'll see it at once.
  - (The plan says to do this in a shader. I did it in the code that draws the forest instead, because I can't
    test a shader without Unity. It looks the same. If F9 shows it costing much frame time, it can move into a
    shader later.)
- **Bushes:** walk through one and it leans away from you and rustles. The rustle is a **noise**, so the blind
  one can hear it: 14 m running, 6 m walking, barely anything crouching.
- **Ambience**, all made in code:
  - a low drone under everything
  - wind that rises and falls with the gusts where you stand
  - a high, trembling tension layer that swells when something's near
  - every 7-22 s something out in the dark: a twig snapping, a rustle, a far-off moan
  - trees creaking in strong gusts

  All of it goes quiet in shelters and the dark room. Any of it can be swapped for a recording in
  `AudioConfig` > Sound Overrides (WindLoop, Drone, TensionBed, Twig, Creak, Rustle).

**Let me know:** does the sway look natural, or too much or too little? What's the frame time like? Is the
ambience too loud, too busy or too quiet?

## Part 2: weather

- **Rain:** light from the start, heavier at the 5th lamp and a downpour at the 7th, easing in over about 20 s.
  The streaks slant in the wind. **It stops at roofs**: no rain inside the shacks, the cabin or the car, or
  under the watchtower's deck. You hear it outside; the shelters muffle it.
- **Lightning** every 15-45 s: the whole forest flickers bright for a moment, and the thunder comes after it,
  later and quieter the further away it struck (up to about 7 s).
- **Tornadoes**, from the 8th lamp: two vast funnels on the horizon, about 900 m away, twisting and drifting
  slowly round, with a low roar. Like the giant, they stay visible through the fog.
- Numbers: `Assets/Config/World/WeatherConfig` > Rain, Lightning and Tornadoes. To see the late weather quickly,
  F9 > Game > **Light the nearest lamp** a few times.

**Let me know:** is the rain visible enough without being in the way? Is the lightning too bright, or too
often? And the frame time in a downpour (3,000 drops).

## Part 3: things happening in the forest

- **Falling trees:** the first comes 30 s into a run, then one every 45-90 s. A tree in plain view, 12-25 m
  away, cracks, shudders, groans and topples across your view (never onto you). It lands with a thud (a noise
  the blind one hears), lies there a while, and sinks away once you're not looking. To see one now:
  F9 > Game > Places > **Fell a tree in view now**.
- **Phantoms:** turn your head quickly and sometimes (a 1 in 4 chance, at most every 40 s) something that
  doesn't belong is standing just past the edge of your view, on the side you're turning to: a chair, a stop
  sign, a gurney, a TV, a mailbox, a child's tricycle. Look away and back, and it's gone. Your own models can
  replace them: `ForestLifeConfig` > Phantoms.
- **Door knocks:** sometimes (1 in 5), shutting a shack or cabin door is answered by three slow knocks, out in
  the trees.
- **Spider webs:** 70 of them, strung between close trees at head height. They're nearly invisible until your
  light catches them, and if you walk into one it tears across your face.
- **Vines** hang from a few trees (4%), swinging in the wind; walk through one and it's pushed aside.
- Numbers: `Assets/Config/World/ForestLifeConfig`.

**Let me know:** are the phantoms creepy, or too obvious? Do you notice the webs before walking into them?
Too many tree falls, or too few?

## Part 4: wildlife, ponds and leaves

- **Owls:** 45 on branches through the forest, with white eyes. Within 45 m their heads turn to follow you,
  smoothly, with the odd sudden snap. Each calls every 25-70 s. Trees with owls in them never fall.
- **Deer:** a herd of six (one with antlers) grazing 35-60 m off.
  - Within 24 m they lift their heads and watch you.
  - Within 14 m (or nearer 22 m if you're running) one bolts and the whole herd goes with it, hooves drumming
    (a noise the blind one hears).
  - They turn up somewhere else later.
- **Small things:** 40 beetles crawling about near you, and 6 rats that scurry, and flee with a squeak if you
  come within 5 m.
- **Flocks:** every 15-45 s a flock of bats crosses overhead, squeaking. Every 20-60 s crows burst up out of
  the trees and away, cawing.
- **At lit lamps:** moths circle the light, and fireflies drift and blink round the clearing.
- **Ponds:**
  - Wading slows you to about half speed, and your steps splash (louder, so they carry further).
  - Glowing mushrooms grow round the edges.
  - Frogs croak from the nearest pond, and **fall silent whenever something's near**. The silence is the
    warning.
- **Leaves:** dead leaves lie all round you. Strong gusts and your running feet kick them up, and a few are
  always drifting down from the trees.
- Numbers: `Assets/Config/World/ForestLifeConfig` (Owls, Deer, Small things, Flocks, Ponds, Leaves).

**Not yet:**
- The plan's door garden for the fireflies (the dark room's door has no garden yet).
- Leaves bursting off falling trees.
- Splinters when a tree hits the ground.

**Let me know:** the frame time with all of this on (it adds a lot of small moving things), whether any of it
is too much or too busy, and whether you notice the frogs going quiet.
