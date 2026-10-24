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
