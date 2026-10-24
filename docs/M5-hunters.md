# M5: the hunters

M5 comes in parts. Each part is pushed when it's done, so this guide grows as they arrive.

Pull (GitHub Desktop: **Fetch origin**, then **Pull origin**), open `Assets/Scenes/Forest.unity` and press
**Play**.

## Part 1: footsteps and music

- **Footsteps** make a soft sound, and a **noise** that things hunting by sound can hear (the blind one, in part
  3): running carries 22 m, walking 5 m, crouching about 1 m. While you're winded, your panting carries 6 m.
  Numbers: `Assets/Config/Player/PlayerConfig` > Footsteps.
- **Music** follows what's happening. The moods:
  | Mood | When |
  | --- | --- |
  | Calm | nothing about (no music) |
  | Tension | something in sight within 30 m |
  | Danger | one coming for you |
  | Watcher | the eye is open or the giant walks |
  | GameOver | while you're taken away |

  The music fades in fast when things get worse and back out slowly. Each big entrance has its own theme.
  All of it is made in code. To use your own tracks, add them in `Assets/Config/Audio/AudioConfig` > Music, or
  drop them in `Assets/Resources/Music/<Mood>/`. F9 > Game > Sound state shows the mood and what's playing.

## Part 2: the giant and the eye

- **From the start** it hangs on a chain far off, in the direction you face when you wake. It turns slowly, and
  four empty chains hang elsewhere round the horizon. The fog never hides it: it stays a hazy silhouette. The
  first clear view of it gently draws your eyes up to it, once. Each lamp you light jolts its chain.
- **The 5th lamp:**
  1. A red eye opens high in the sky, and your view is drawn up to it.
  2. The fog turns red.
  3. After 3 s the **dream** takes hold: the view stretches wide and running gets you nowhere.
  4. As the eye closes, the chain snaps like thunder, and the giant falls and lands with a boom. Its theme
     starts.
  5. A shockwave rolls out and shakes you as it passes.
  6. Then it **walks toward you** (7.5 m/s), each step a thud and a shake that grows as it nears. Now and
     then it stops to look round.
  7. Within 135 m it takes you. You wake by your last lamp, and the giant is back on the horizon. It walks
     again after 30 s.
- **The 10th lamp:** red searchlights from its eyes follow you, a little faster than you can run, and a crowd
  of 32 scours circles the edge of the light.
- **To test quickly:** F9 > Game > Giant > **Open the eye now**, or **Searchlights and crowd now**.
- All its numbers are in `Assets/Config/Entities/GiantConfig`. The plan's height (660 m) and angles (feet
  10°, neck 38°) set how far away it hangs: about 940 m. Your own giant model can go in **Custom Model**.

**Not yet:** the shockwave doesn't bend the trees and grass (that needs the wind shader, M6). The watchtower's
floodlights that drive it back come in part 6.

**Let me know:** does it read as a silhouette? Is the fall and walk scary, too slow or too fast? Does the
searchlight look right? What's the frame time with the crowd out?

## Part 3: the blind one ("the Auscult")

- **The 3rd lamp:** a low black storm gathers 45 m ahead of you, with red lightning forking down and thunder
  after it. Its theme starts, and in time with it:
  - at 3.5 s it **breaks out of the ground**
  - at 7.6 s it **roars**
  - at 13.1 s it **stamps twice**

  Then it hunts.
- **It's blind and hunts by sound.** It roams within 40 m of you (1.5 m/s), stopping now and then to cock its
  head and click.
  - **Noises send it rushing** to the spot (5.2 m/s): your running steps (heard from 22 m), walking (5 m),
    panting when winded (6 m), the flare gun, the car's chirp, a tree falling.
  - **It searches where it heard you**: 5 s on the spot, then 45 s round it.
  - **Within 4 m it hears you whatever you do**, unless you're crouching. Creep past it.
- **Within 2.6 m it takes you:** it leans its face down to yours, then a hard cut to black. It's gone for a
  minute after that.
- **Lights stop it:** it won't step into a lamp's circle, or a torch's or a refuge's.
- **It barges through trees and knocks them down**; they topple, thud, lie a while and sink away. Rocks,
  boulders and buildings it walks round.
- **Get 90 m away for a minute and it catches up**, 35 m behind you.
- A **camera flash** drives it off for 30 s. A **flare** in the sky makes it lose interest for a while.
- **To test quickly:** F9 > Game > Blind one > **Bring it now**.
- Numbers: `Assets/Config/Entities/BlindConfig`. Your own model can go in **Custom Model**.

**Let me know:** is it fair? Can you creep past it? Does the storm look right? Do the falling trees look OK?

## Part 4: crawlers

- **From the 2nd lamp** (Easy: 3rd, Hard: 1st), four of them (fewer on Easy, more on Hard) lie in wait 14-45 m
  round you, low in the mist. Each is a head on four long thin arms. Now and then one creeps to a new spot.
- **Their eyes shine back in your beam** from about 10 m away, and up close you hear them click.
- **Come within 1.8 m walking (2.5 m running) and it leaps onto your face:**
  - you can barely see and can't move
  - it shrieks, which the blind one can hear
  - another scour is on its way
  - **mash Space 8 times** to throw it off, or it lets go after 6 s

  **Crouch past one and it doesn't notice.** Standing still next to it is safe too.
- **They keep out of the light:** your beam on one close by, or a lamp's circle, sends it scuttling away.
- **To test quickly:** F9 > Game > Crawlers > **Bring them now**.
- Numbers: `Assets/Config/Entities/CrawlerConfig`. Your own model can go in **Custom Model** (a child named
  "Eyes" will glow).

**Let me know:** are they easy enough to spot (eyeshine), and is the face-grab scary without being annoying?

## Part 5: the flier

- **The 7th lamp:** a cry, then a vast ragged black bird (40 m across) dives in from ahead, straight over you,
  low enough to shake the ground. It climbs away, and as its theme's bell rings its one eye lights: a
  searchlight.
- **It circles stretches of the forest** 48 m up, sweeping its beam over them, moving on every half minute or
  so. For the first 15 s its beam keeps off you.
- **If the beam passes within 9 m of you, it snaps onto you** and circles over you, holding the light on you.
  - **To lose it:** get out of its sight for 4 s (under trees, under a roof, or in a safe light).
  - **Standing still in its light is safe.**
  - **Move in it for 5 s in all**, and it screeches, the light turns red, and **every scour comes at once**.

  It never takes you itself.
- **To test quickly:** F9 > Game > Flier > **Bring it now**.
- Numbers: `Assets/Config/Entities/FlierConfig`. Your own model can go in **Custom Model** (a child named "Eye"
  is where the light comes from).

**Let me know:** can you see it against the sky? Is the beam easy enough to dodge, and is losing it under the
trees fair?
