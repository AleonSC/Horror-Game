namespace Game.Audio
{
    /// <summary>
    /// The game's built-in sounds (made by <see cref="SoundSynth"/> at startup). New ones go on the end:
    /// AudioConfig's overrides store them by number.
    /// </summary>
    public enum Sound
    {
        Creep, Appear, Pound, Crescendo, Portal, Shriek, Moan, Heartbeat, Breath, Click, LampOn,
        Pickup, Ignite, Lighter, FlareShot, FlareBurst, Crack, Shutter, Chirp, DoorOpen, DoorShut, Clunk, Eat,
        Step, Thunder, Boom, GiantStep, ChainJolt,
        Rustle, Twig, Creak, WindLoop, Drone, TensionBed,
        RainLoop, Hoot, Croak, Squeak, Caw, Splash, Hooves,
        Rotors, Static, RadioSong,
        DeerGrunt, DeerBleat, DeerSnort, DeerAlarm, Gallop,
    }
}
