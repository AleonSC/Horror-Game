# Using your own scour model

The scour's behaviour doesn't depend on how it looks. Two looks plug in:

- **Built-in** (default): the procedural figure made in code.
- **Your model**: any prefab you make.

Switch with one checkbox: `Assets/Config/Entities/ScourConfig` > **Use Custom Model**.

## Setting it up

1. Import your model (FBX, for example from Blender, Tripo or Mixamo) and make a **prefab** of it. Put it
   somewhere in git, such as `Assets/Art/Scour/`, not in `Assets/Packs/` (that folder isn't committed).
2. Open the prefab and add the **Scour Model Rig** component to its root (Add Component > search "Scour Model Rig").
3. Fill in what you can. Every slot is optional; empty ones are guessed from the model's size.
   | Slot | What to drag in |
   | --- | --- |
   | Head | the head bone (or head object) |
   | Eye Points | one empty object at each eye. The game puts glowing eyes there. |
   | Eye Renderers | *or* your model's own eye meshes. The game makes them glow red, and blue when it's faced down. |
   | Mouth | an empty object at the mouth, facing out. The red glare shines from it. |
   | Chest | the middle of the body. The flashlight has to be on this to face it down. |
   | Jaw | a jaw bone, if you want the game to open it. Leave it empty if your animations open the mouth. |
4. In **ScourConfig**: drag the prefab into **Custom Model** and tick **Use Custom Model**. Press Play.

The game handles the rest:
- It scales the model to the scour's height (Match Height), puts its feet on the ground, and removes its colliders.
- It adds the eye glow, the red light round the eyes and the mouth glare.
- It does the appearing, shuddering, growing and facing you, exactly as for the built-in one.

If your model faces the wrong way, set **Yaw Offset** (for example 180).

## Animations (optional)

Give the model an **Animator**. If its controller has any of these parameters, the game sets them:

| Parameter | Type | What it means |
| --- | --- | --- |
| `Pose` | Int | the pose it's holding (numbers below). It changes on each creep, while you're not looking. |
| `Side` | Int | -1 or 1: which way a one-sided pose leans (head tilt, reach, peek, turned away) |
| `Mouth` | Float | 0 shut, 1 open, up to 1.6 gaping |
| `Speed` | Float | metres per second it's moving (gliding at you in the dark, charging) |
| `Shudder` | Float | how hard it's shaking (it builds while being faced down) |
| `Appear` | Trigger | it has just appeared |

| Pose | Number | When |
| --- | --- | --- |
| Neutral | 0 | standing, arms hanging |
| HeadTilt | 1 | after a creep |
| Reach | 2 | after a creep; and as it closes in to take you |
| Crouch | 3 | after a creep |
| TurnedAway | 4 | after a creep: back to you, head turned round |
| ArmsUp | 5 | after a creep |
| HangingHead | 6 | after a creep |
| Peek | 7 | leaning out from behind a tree |
| Lunge | 8 | charging you, and grabbing you |

A simple controller with one state per pose, chosen by `Pose`, is enough.
