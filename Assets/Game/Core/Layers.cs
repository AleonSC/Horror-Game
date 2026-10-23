namespace Game.Core
{
    /// <summary>The physics/render layers from section 2.7 of the plan (set up in Project Settings > Tags and Layers).</summary>
    public static class Layers
    {
        public const int Default = 0;
        public const int Ground = 8;
        public const int Trees = 9;
        public const int Props = 10;
        public const int Pickups = 11;
        public const int Entities = 12;
        public const int Effects = 13;
        public const int PlayerBody = 29;
        public const int PortalFaces = 30;
        public const int Thermal = 31;

        public static int Mask(int layer) => 1 << layer;
    }
}
