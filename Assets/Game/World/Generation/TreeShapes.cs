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

        public static int Count(TreeKind kind, WorldGenSettings s)
        {
            var custom = kind == TreeKind.Pine ? s.livingTreeHeights : s.deadTreeHeights;
            return custom != null && custom.Length > 0 ? custom.Length : kind == TreeKind.Pine ? PineVariants : DeadVariants;
        }

        public static float Height(TreeKind kind, int variant, WorldGenSettings s)
        {
            var custom = kind == TreeKind.Pine ? s.livingTreeHeights : s.deadTreeHeights;
            if (custom != null && custom.Length > 0) return custom[variant % custom.Length];
            return Height(kind, variant);
        }

        /// <summary>The built-in trees' heights.</summary>
        public static float Height(TreeKind kind, int variant) =>
            kind == TreeKind.Pine ? 14f + variant * 1.1f : 8f + variant * 1.7f;

        /// <summary>How far the crown spreads from the trunk (before scaling).</summary>
        public static float CrownRadius(TreeKind kind) => kind == TreeKind.Pine ? 3f : 3.5f;

        /// <summary>Where the crown starts, as a share of the height.</summary>
        public static float CrownStart(TreeKind kind) => kind == TreeKind.Pine ? 0.42f : 0.4f;
    }
}
