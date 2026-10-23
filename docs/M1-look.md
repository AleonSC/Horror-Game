# M1: the look test scene

Goal: a small patch of night forest that looks like the reference photo (pale fog, glowing ground mist, dark
trees fading out, a warm flashlight, red eyes the only strong colour). When it's right, you sign it off and
we move on to the player and the first scour (M2).

## 1. Pull and build the scene

1. GitHub Desktop: **Fetch origin**, then **Pull origin**. Open the project.
2. Check the Console has no red errors.
3. In Unity's top menu: **Game > Build Look Test Scene**. It takes a few seconds, then opens
   `Assets/Scenes/Generated/LookTest/LookTest.unity`.
   - The scene is generated, so git ignores it. Rebuild it any time with the same menu item.
   - The Scene view looks bright and plain. The night look is only switched on in Play mode.
4. Press **Play**.

## 2. Moving round

| Key | Does |
| --- | --- |
| Click | capture the mouse (Esc frees it) |
| WASD / mouse | walk / look (Shift: faster) |
| G | fly on/off (Space up, Ctrl down) |
| F | flashlight on/off |
| F9 | dev window (F11: session log) |

You start on a path. About 15 m ahead stands a stand-in scour with red eyes. Further on there's a clearing
with a lit lamp post.

## 3. Tuning

Open **F9**:

- **Game** tab: the frame time is at the top. The plan's budget for this scene is **under 8 ms**.
- **Lighting** tab: theme buttons (**Reference** is the default) and the main sliders: brightness, fog,
  mist, moon, contrast, saturation, flashlight.
- **Look** tab: every value, in folding sections, with a search box. **↺** puts a value back to its default.

Changes show immediately and are kept after you stop Play.

**To keep your changes:** press **Save** at the bottom of the Lighting or Look tab. Then in GitHub Desktop,
commit and push the changed files in `Assets/Config`. Or press **Copy for Claude** and paste the text into
the chat.

## 4. What to look at

Compare with the reference photo:
- Is the fog pale and brighter than the shadows? Do the trees stand dark against it and fade with distance?
- Is there a thick, glowing mist low over the ground, broken into soft patches?
- Is the sky a dull grey behind the trees?
- Does the flashlight show as a beam in the mist? Does it light the path in a warm pool, and dim when pointed
  at your feet?
- Are the scour's eyes the only strong colour, with a faint red haze round its head?
- Is the lamp's glow in the mist soft, not blazing?

Screenshots of the Game view help a lot: one with the flashlight on, one with it off.

## What's a stand-in (on purpose)

- **Trees, grass and rocks** are simple generated shapes, good enough to judge fog and light. Real trees
  (your Tree9 pack, or better procedural ones) come with world generation in M3.
- **The scour** is a rough figure, there to check the red eyes and the haze round its head. The real one
  comes in M2.
- **The night sky** is a flat grey for now (the reference barely shows stars). The star and cloud dome comes
  later.
- **Walking** has no collisions. The real player controller comes in M2.
