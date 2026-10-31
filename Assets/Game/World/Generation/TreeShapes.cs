namespace Game.World.Generation
{
    /// <summary>
    /// How many tree variants there are and how tall each is (before scaling), shared by the plan (sightlines
    /// need the heights) and the build stage (which makes or loads the trees). The built-in trees have fixed
    /// heights; your own trees (WorldArtConfig) bring theirs, through the settings.
    /// </summary>
    public static class TreeShapes
    {
        public const int PineVariants = 8;
        public const int DeadVariants = 5;
        public const int BirchVariants = 4;
        public const int WillowVariants = 3;

        // Your own trees replace the oaks and the dead trees; birches and willows are always the built-in ones.
        static float[] Custom(TreeKind kind, WorldGenSettings s) =>
            kind == TreeKind.Pine ? s.livingTreeHeights : kind == TreeKind.Dead ? s.deadTreeHeights : null;

        public static int Count(TreeKind kind, WorldGenSettings s)
        {
            var custom = Custom(kind, s);
            if (custom != null && custom.Length > 0) return custom.Length;
            switch (kind)
            {
                case TreeKind.Pine: return PineVariants;
                case TreeKind.Birch: return BirchVariants;
                case TreeKind.Willow: return WillowVariants;
                default: return DeadVariants;
            }
        }

        public static float Height(TreeKind kind, int variant, WorldGenSettings s)
        {
            var custom = Custom(kind, s);
            if (custom != null && custom.Length > 0) return custom[variant % custom.Length];
            return Height(kind, variant);
        }

        /// <summary>The built-in trees' heights.</summary>
        public static float Height(TreeKind kind, int variant)
        {
            switch (kind)
            {
                case TreeKind.Pine: return 10.5f + variant * 0.8f; // ("Pine" is the living tree: an oak now)
                case TreeKind.Birch: return 11f + variant * 1.3f;
                case TreeKind.Willow: return 8f + variant * 1.1f;
                default: return 8f + variant * 1.7f;
            }
        }

        /// <summary>How far the crown spreads from the trunk (before scaling). (A willow's leans out over the water.)</summary>
        public static float CrownRadius(TreeKind kind) =>
            kind == TreeKind.Pine ? 5.5f : kind == TreeKind.Birch ? 2.6f : kind == TreeKind.Willow ? 7f : 3.5f;

        /// <summary>Where the crown starts, as a share of the height.</summary>
        public static float CrownStart(TreeKind kind) =>
            kind == TreeKind.Pine ? 0.33f : kind == TreeKind.Birch ? 0.45f : kind == TreeKind.Willow ? 0.3f : 0.4f;
    }
}
