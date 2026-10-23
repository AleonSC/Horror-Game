# Scary Game (remake)

A first-person forest horror game in Unity 6 (HDRP), rebuilt from scratch.

- **The plan:** [`REBUILD_PLAN.md`](REBUILD_PLAN.md) describes the whole game and the order it's built in.
- **Milestone guides:** [`docs/M0-setup.md`](docs/M0-setup.md) (setup), [`docs/M1-look.md`](docs/M1-look.md) (the look test scene).
- **The prototype** this remake is based on lives on the `hdrp` branch (code, shaders, and its
  `HANDOFF.md` notes). Use it as a reference for behaviour and feel, not structure.

## Layout

| Folder | What's there |
| --- | --- |
| `Assets/Game/Core` | event bus, services, the run, config base types, player preferences |
| `Assets/Game/Audio` | sound states (mixer snapshots) |
| `Assets/Game/Rendering` | the look: fog, sky, moon, exposure, colour, ground mist, themes |
| `Assets/Game/Player` | flashlight; a simple walker for test scenes (the real player comes in M2) |
| `Assets/Game/Editor` | editor tools: Game > Build Look Test Scene |
| `Assets/Game/DevTools` | dev window (F9), session recorder (F11); editor and development builds only |
| `Assets/Game/Tests` | edit-mode and play-mode tests |
| `Assets/Config` | tuning assets; `Resources/GameSetup` lists them all |

Asset Store packs stay out of git (the repo is public): put them in `Assets/Packs/`.
