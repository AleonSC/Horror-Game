# M0 setup: the parts done in Unity and on GitHub

Claude wrote the code for milestone 0. These are the steps that need your hands. Do them in order.

## 1. Pull

1. **Close Unity first.** One of the settings changes (Input System only) is only picked up when Unity starts.
2. If `Assets/Tree9` is still in your folder, copy it to your Desktop first (the pull would delete it),
   and copy it back in after.
3. GitHub Desktop: **Fetch origin**, then **Pull origin**.
4. Open the project from Unity Hub. It will take a while the first time.

**Check:** the Console (Window > General > Console) shows no red errors. Yellow warnings saying there's no
mixer yet are expected until step 3.

## 2. Try it

1. Open `Assets/OutdoorsScene` (the template's scene, kept for now) and press Play.
2. Press **F9**: the dev window opens on the left (dark panel, amber headings). The Game tab shows the run
   number, seed, time and lamps lit.
3. Press **F11**: it jumps to the Session tab, the session recorder. Flip the switches in the Game tab and you'll
   see each event appear here.
4. Stop Play. A log file appears in `SessionLogs/` in the project folder (git ignores it; send it to Claude if
   asked).

**Check the tests:** Window > General > Test Runner. Run all under **EditMode** (29 tests), then
**PlayMode** (3 tests). All should be green.

## 3. Make the audio mixer (about 10 minutes)

Unity doesn't let code create mixers, so this one is by hand. Names must be spelled exactly as written.

1. In the Project window, make a folder `Assets/Audio`. Right-click in it > **Create > Audio Mixer**, name it
   `GameMixer`.
2. Open **Window > Audio > Audio Mixer**.
3. **Groups.** Select a group and click **+** next to "Groups" to add a child under it:
   - under **Master**: `Music`, `UI`, `Effects`
   - under **Effects**: `Player`, `World`, `Entities`, `Stingers`
   - under **World**: `Outside`, `Inside`
4. **Effects on groups** (click **Add Effect** at the bottom of a group's strip):
   - **Entities**: add **Lowpass Simple**.
   - **Player**: add **SFX Reverb**.
5. **Snapshots.** Rename the existing "Snapshot" to `Outdoors` (right-click > Rename). Click **+** next to
   "Snapshots" to add `Sheltered`, `DarkRoom`, `Dread`, `Hush`, `Paused`.
6. **Set each snapshot.** Click a snapshot, then set its values. Anything not listed stays as in Outdoors.

   | Snapshot | Settings |
   | --- | --- |
   | Outdoors | all faders 0 dB; Entities lowpass cutoff 22000 Hz; Player reverb **Room -10000** (off) |
   | Sheltered | Outside **-80 dB**; Entities **-12 dB**, lowpass cutoff **380 Hz** |
   | DarkRoom | Outside **-80 dB**; Entities **-80 dB**; Player reverb **Room 0**, **Decay Time 6** |
   | Dread | World **-20 dB** |
   | Hush | Music **-40 dB**; World **-40 dB**; Entities **-30 dB** |
   | Paused | nothing (the game pauses sounds itself) |

   (Starting points from the plan; tune them by ear later.)
7. **Volume sliders for the options menu.** Select the **Master** group; in the Inspector, right-click the
   word **Volume** > **Expose 'Volume (of Master)' to script**. Do the same for **Music** and **Effects**.
   Then, top right of the Audio Mixer window, open **Exposed Parameters** and rename them (double-click) to
   `MasterVolume`, `MusicVolume`, `EffectsVolume`.
8. Select `Assets/Config/Audio/AudioConfig` and drag `GameMixer` into its **Mixer** field.

**Check:** press Play. The yellow "no mixer" warnings are gone. Open F9 and flip **Sheltered** or pick **Hush**:
in the Audio Mixer window the faders move to that snapshot.

Then commit and push (GitHub Desktop) so the mixer is in git.

## 4. Add Cinemachine

Window > Package Manager > **Unity Registry** > search **Cinemachine** > **Install**. Commit and push the
changed `Packages/` files.

## 5. Turn on CI (automatic checks on every push)

This lets Claude compile and test in the cloud instead of waiting for screenshots.

1. **Get your licence file.** Unity Hub must be signed in with your Personal licence. In File Explorer, paste
   `C:\ProgramData\Unity` into the address bar. Open **Unity_lic.ulf** with Notepad and copy everything in it.
2. On GitHub, open the repo > **Settings > Secrets and variables > Actions > New repository secret**. Add three:
   - `UNITY_LICENSE`: paste the whole file contents
   - `UNITY_EMAIL`: your Unity account email
   - `UNITY_PASSWORD`: your Unity account password

   Secrets are encrypted: nobody can read them back, including Claude, and they're not visible in the public repo.
3. Open the **Actions** tab > **CI** > **Run workflow** (or just push anything). The first run is slow (up
   to an hour or so); later runs reuse a cache.

**Check:** the run goes green. The Windows build is under the run's **Artifacts** (kept 7 days). If it goes
red, tell Claude; it can read the logs.
