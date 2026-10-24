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
