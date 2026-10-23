# M4: the loop

Goal: the game's core loop works. You light the ten lamps one by one while more and more scours come, with
items to help you, places to shelter, a pause menu and difficulty levels.

## 1. Pull and play

1. GitHub Desktop: **Fetch origin**, then **Pull origin**. Open the project and check the Console for red errors.
2. Open `Assets/Scenes/Forest.unity` and press **Play**. (No need to rebuild the scene.)

## 2. Controls

| Key | What it does |
| --- | --- |
| 1-8, mouse wheel | pick a hotbar slot |
| F | use what's held |
| Left click | crank, take, eat, search |
| E | open and shut doors |
| Esc | pause menu |
| F9 | dev window (you can walk with it open) |

## 3. What's new

**Escalation** (all of it is in `Assets/Config/Entities/ScourConfig` > How many):
- The first scour comes after 60 s of calm, as before.
- **Each lamp you light brings one more** scour, 4-10 s later.
- **One more joins every minute** for ten minutes after the calm.
- There's no cap. With all ten lamps lit and the ten minutes gone, that's 21 scours.
- **Lighting the 10th lamp** unlocks the lone door, with a heavy clunk you hear everywhere. Opening it shows
  nothing yet: the dark room behind it comes in M5.

**The hotbar.** It shows for a few seconds after anything changes. You start with just the flashlight, and
holding anything else puts the flashlight away (it's dark, but it doesn't drain).

| Item | Where | F does |
| --- | --- | --- |
| Batteries | 30 in the forest (a faint pulsing green light), on shack tables (half of them), 2 in each backpack, 2 in the glove box | put one in: +10% |
| Torch | every shack table | light it, using 10% of a lighter's fuel. Burns 120 s. A 7 m safe circle. Putting it away puts it out. |
| Lighter | every shack table | open or shut it. 60 s of fuel. A 3.5 m safe circle. |
| Flare gun | campsite backpacks | fire it. Aimed at least 30° up, it bursts at the top of its arc: red light everywhere, and every scour is driven off. Aimed lower, it's wasted. |
| Glowsticks (10) | campsite backpacks | crack and toss one 3 m. It glows for 4 minutes. Only a light: scours ignore it. |
| Camera (3 shots) | the car's trunk | a flash that drives off every scour in frame, up to 40 m away, for 30 s |
| Key fob | 20% of shack tables and backpacks | the car chirps and flashes its lights, wherever it is |
| Snack | each tent | eaten on the spot: 25% more sprint time for the rest of the run (the stamina bar gets longer) |

**Places:**
- **Shacks and the cabin** are now hollow, each with a door (E) and a table. Being inside brings the muffled
  **Sheltered** sound.
- **Tents** are low: crouch (Ctrl) to crawl in.
- **The car** has a glove box (click the passenger window) and a trunk (click the back).
- Each place has a 5 m **refuge** round it: you can't be taken there, and scours circle the edge.

**Pause (Esc):** time and sound stop, and letterbox bars slide in. The menu has Return, Restart (asks
first), Options and Quit. Options covers:
- mouse sensitivity
- master, effects and music volume
- motion blur on/off and strength
- **difficulty**: Easy, Normal, Hard or Custom (Custom gives you a slider for each setting)

Your choices are saved to your settings file.

**Difficulty** changes, each by the plan's multiplier:
- battery life
- batteries in the forest (from the next world, so press Restart)
- how often extra scours join, and how many
- how long facing one down takes
- lamp cranking speed

Values for the creatures still to come (the blind one, crawlers, moths) are saved now and used once they
arrive.

**Dev window (F9) > Game:** the difficulty, **Light the nearest lamp** (for testing escalation fast), what
you're carrying, **Give one of every item**, and how many scours the clock has added so far.

## 4. What to look for

- Is the escalation right? Does it get scary at a good pace, and is 10 lamps hard but possible?
- Do the items feel useful? Is the torch's 7 m circle too safe?
- Are the tents and shacks easy to get into?
- Is anything hard to find or aim at (the glove box, the trunk, the backpack)?
- Which difficulty feels right?
- **Frame time** with lots of scours out (F9 shows it). Ten lamps' worth plus the clock is a lot of them.

All the numbers are in:
- `Assets/Config/Player/ItemsConfig`
- `Entities/ScourConfig` > How many
- `World/WorldGenConfig` > Pickups

## 5. Not yet (on purpose)

- **The map and the drone** (M7), the **polaroid** the camera ejects (M7), the radio, the **inventory screen**
  (Tab, M7), the pickup halo and the HUD's final look (M7).
- **Lamp narration** (M7), the **crowd of 32 scours** at 10 lamps and the giant's searchlights (M5), what's
  behind the lone door (M5).
- **The car's seat and headlights, the watchtower's floodlights** (M5).
- The options for **Classic look** and **ray tracing** (later, once we know what they should be).
- The main menu's difficulty picker comes with the main menu in M7. For now, difficulty is in the pause menu.
