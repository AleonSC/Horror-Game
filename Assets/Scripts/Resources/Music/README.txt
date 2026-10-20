Soundtrack folders for MusicManager.

Put audio files (.ogg, .wav, .mp3...) in a subfolder named after the mood they're for:

  Calm      nothing around, exploring
  Intro     while the opening lines are on screen
  Tension   an entity is out there watching (within 30m)
  Danger    one is right on you, in its crescendo, charging, or taking you
  Watcher   the eye in the sky / the giant
  GameOver  after you've been taken
  Stingers  one-off stings, played with MusicManager.Instance.PlayStinger()

Several tracks in one folder make a shuffled playlist; a single track loops.
A mood with no tracks falls back to a calmer one (Danger -> Tension -> Calm).
Tip: in each clip's import settings, set Load Type to Streaming for long tracks.
