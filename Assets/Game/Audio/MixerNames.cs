namespace Game.Audio
{
    /// <summary>Names in the AudioMixer asset. They must match it exactly.</summary>
    public static class MixerNames
    {
        // Groups: Master > {Music, UI, Effects > {Player, World > {Outside, Inside}, Entities, Stingers}}
        // (Effects gathers everything that isn't music or UI so one volume slider covers it.)
        public const string Master = "Master";
        public const string Music = "Music";
        public const string UI = "UI";
        public const string Effects = "Effects";
        public const string Player = "Player";
        public const string World = "World";
        public const string Outside = "Outside";
        public const string Inside = "Inside";
        public const string Entities = "Entities";
        public const string Stingers = "Stingers";

        // Exposed parameters (volume in dB) for the options menu.
        public const string MasterVolume = "MasterVolume";
        public const string MusicVolume = "MusicVolume";
        public const string EffectsVolume = "EffectsVolume";
    }
}
