# Polish: the game's own shaders

The plan asked for the game's own shaders (section 4.4) where HDRP's built-in materials look flat. This first
batch is three effects, used all over:

| Shader | What it is | Where you'll see it |
| --- | --- | --- |
| `Game/Glow` | a soft disc of light facing you, brightest in the middle, fading into the fog and where it meets surfaces | round a lit lamp's glass, the red marker on a dark lamp, the scours' eyes, a battery's gleam, thrown glowsticks, a flare (big and red when it bursts), deer eyeshine |
| `Game/Beam` | a soft shaft of light with dust drifting in it | under the watchtower's floodlights, the giant's searchlights (from its eyes down to you), the flier's beam |
| `Game/Flame` | a licking, flickering flame: blue at the root, yellow, orange, a red tip | the torch, the lighter, a flare |

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
- Deer eyeshine: `ForestLifeConfig` > `deerEyeshine`.

## Deer

They were placed 35-60 m away, past what the fog lets you see. Now the herd turns up within sight (worked out
from the fog), with a clear view to it, usually somewhere ahead of you; after bolting it's back somewhere
else in a minute or two (`deerReturn`). Their eyes shine back pale green when your flashlight catches them.
The model is rebuilt: a deep chest and slim waist, a long neck and narrow head, big ears, jointed legs bent
back at the hock, a white rump and tail, and branching antlers on the buck.

**Let me know:** do the glows and shafts look soft rather than like flat discs and cones? Too bright, too
faint? Do the flames look like fire? And do you see the deer now, and do they look like deer?
