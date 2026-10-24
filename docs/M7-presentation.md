# M7: presentation

M7 comes in parts, pushed one at a time; this guide grows as they arrive. Pull, open `Assets/Scenes/Forest.unity`
and press **Play**.

## Part 1: the new HUD and pause menu (UI Toolkit)

The plan asks for the menus and HUD in Unity's UI Toolkit, not the quick IMGUI drawing they used until now. They
look the same as before for now, just crisper, and they scale with the screen:
- **HUD:** crosshair and prompt, the lamp row, battery and stamina, the hotbar, hints, and the fade when you're taken.
- **Pause menu** (Esc): Return, Restart, Options and Quit, with real sliders and toggles in Options, and a scrolling
  page.

**One thing to commit:** Unity makes a `.meta` file for the new theme, `Assets/Resources/UI/GameTheme.tss.meta`,
the first time it opens the project. Commit it with your next push.

**Let me know:**
- Does everything show up, and in the right place? (Screenshots help.)
- Do the buttons and sliders respond to the mouse? If they don't, the Console will probably say something about
  the event system.

## Part 2: boot, main menu and narration

Press **Play** in the forest scene and you now get:
1. **Boot:** a black screen with a faint line filling while the world and the music are made.
2. **Main menu:** the title and **START / DIFFICULTY / QUIT** between black letterbox bars, over a slow film of
   the forest: treetops, a crane shot, the deer herd, an owl, low mist. Shots cut through black. Nothing hunts you
   here and the clock doesn't run.
3. **START:** fade to black and "Wake up" over black; then you wake in the forest, groggy (slow, swaying, slow to
   look round, vision blurred and clearing over 5 seconds), and can move while the rest of the intro lines play.
4. **Lamp narration:** lines low on the screen when you light the 2nd, 3rd, 5th (frantic: red and shaking) and
   7th lamps.

The menu shows once per session; Restart goes straight back into the forest.

**Settings** (`Assets/Config/UI/PresentationConfig.asset`): the title (empty uses the product name), whether the
menu shows at all, seconds per film shot, the intro lines, how long each holds, how many play over black before you wake, how long you're groggy, and the
lamp lines. How slow and blurred groggy is: `PlayerConfig` (Waking up). Markup: in the
intro `*word*` is red, `**word**` red and lingers, `***word***` lingers long; in lamp lines `"-word"` is red,
other `"quoted"` words are italic, and `-WORD-` is one red word.

**Let me know:**
- Does the boot screen give way to the menu, and does the film look good? Any shot that points at nothing, sits
  inside a tree or looks at the giant up close?
- Do START and the intro flow into the game smoothly, with the mouse captured afterwards?
- Do the lamp lines appear, and are they readable?

## Part 3: inventory, map and polaroids

- **Inventory (Tab):** the world dims but keeps going, and you can still walk. Your eight slots in a grid on
  the left: **drag** one onto another to swap them (the number keys follow the new order); **click** one for
  its page on the right (what it is, what F does, what's left in it). Your photos are underneath; click one
  to look at it. Tab or Esc closes it.
- **The map** lies on the watchtower's deck. Hold it and press **F** to hold it up: a pencil sketch of the
  trails, lamps as rings, ponds, and little symbols for the buildings. Lamps you've lit are inked in. It
  doesn't show where you are. You can walk and look round with it up; F again, or switching item, puts it
  down. Its inventory page shows it too. (Dev window, Game tab: **Give the map**.)
- **Polaroids:** each photo with the camera comes out at the bottom right, blank, and develops over about 8
  seconds (`ItemsConfig` > Camera: photo size and develop time). They're kept in the inventory for the run.

**Let me know:**
- Do drag and click both work in the inventory? Does Esc close it without opening the pause menu?
- Does the map look like a sketch, and is it the right way round compared to the forest (north up)?
- Do the polaroids show the actual shot, lit by the flash? Too dark, too bright, or washed out?
- The first time the map is shown there may be a short hitch while it's drawn.

## Part 4: the drone, your body, the radio

- **The drone** is at the cabin's table, on the watchtower's deck or on the car's back seat: whichever you get
  to first (the others stay empty). Hold it and press **F**: it's set down and lifts off, and you see through
  its camera. **WASD** fly, **mouse** look, **Space / Ctrl** up and down, **Shift** faster, **click** its lamp,
  **T** (or right click) thermal: grey-blue, no fog, and living things glow. **F** brings your eyes back and
  it settles to the ground; click it to pick it up, or F with it in hand to fly it again. While you fly you
  stand where you are, blind and deaf: the scours treat you as not looking. Its signal only reaches about
  140 m (static, then it won't go further), its battery lasts about 2.5 minutes in the air and it drops when
  flat, and its motors can be heard by the blind one from 220 m. Settings: `ItemsConfig` > Drone.
- **Your body:** a hooded figure the drone's camera and the portals see (never your own eyes): it walks and runs
  at your pace, crouches, pants when winded, sits in the car, and holds the remote while you fly. Your own
  model and clips can go in `PlayerCharacterConfig`.
- **The radio:** on the car's back seat, and sometimes on a shack's table or in a backpack. F sets it down
  playing an old song. The blind one hears it from anywhere, comes for it, and smashes it: a lure. Click it to
  pick it up again. Settings: `ItemsConfig` > Radio.

Dev window, Game tab: **Give the drone**, **Give a radio**.

**Let me know:**
- Does flying feel right (speed, turning), and does F switch back and forth cleanly?
- Does thermal look like thermal, and do the scours, crawlers, the blind one, deer and you glow in it?
- Can you see your body from the drone, and does it move sensibly?
- Does the blind one come for the radio and smash it?
