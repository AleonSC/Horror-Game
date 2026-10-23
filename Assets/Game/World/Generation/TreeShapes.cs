namespace Game.World.Generation
{
    /// <summary>
    /// The heights of the tree variants (before scaling), shared by the plan (sightlines need them) and the
    /// build stage (which makes the meshes).
    /// </summary>
    public static class TreeShapes
    {
        public const int PineVariants = 8;
        public const int DeadVariants = 5;

        public static float Height(TreeKind kind, int variant) =>
            kind == TreeKind.Pine ? 14f + variant * 1.1f : 8f + variant * 1.7f;

        /// <summary>How far the crown spreads from the trunk (before scaling).</summary>
        public static float CrownRadius(TreeKind kind) => kind == TreeKind.Pine ? 3f : 3.5f;

        /// <summary>Where the crown starts, as a share of the height.</summary>
        public static float CrownStart(TreeKind kind) => kind == TreeKind.Pine ? 0.42f : 0.4f;
    }
}
