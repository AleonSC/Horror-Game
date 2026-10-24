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
