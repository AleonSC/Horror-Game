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
3. **START:** fade to black, the intro lines one at a time (red words, some lingering), then the forest fades in
   around you and the run begins.
4. **Lamp narration:** lines low on the screen when you light the 2nd, 3rd, 5th (frantic: red and shaking) and
   7th lamps.

The menu shows once per session; Restart goes straight back into the forest.

**Settings** (`Assets/Config/UI/PresentationConfig.asset`): the title (empty uses the product name), whether the
menu shows at all, seconds per film shot, the intro lines, how long each holds, and the lamp lines. Markup: in the
intro `*word*` is red, `**word**` red and lingers, `***word***` lingers long; in lamp lines `"-word"` is red,
other `"quoted"` words are italic, and `-WORD-` is one red word.

**Let me know:**
- Does the boot screen give way to the menu, and does the film look good? Any shot that points at nothing, sits
  inside a tree or looks at the giant up close?
- Do START and the intro flow into the game smoothly, with the mouse captured afterwards?
- Do the lamp lines appear, and are they readable?
