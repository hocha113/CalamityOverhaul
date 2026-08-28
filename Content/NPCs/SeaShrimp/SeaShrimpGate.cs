using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.SeaShrimp
{
    /// <summary>渊晶海虾开发门控：未完成不进游戏；改 Enabled 即重新加载</summary>
    internal static class SeaShrimpGate
    {
        internal const bool Enabled = true;
    }

    internal abstract class SeaShrimpModItem : ModItem
    {
        public override bool IsLoadingEnabled(Mod mod) => SeaShrimpGate.Enabled;
    }

    internal abstract class SeaShrimpModNPC : ModNPC
    {
        public override bool IsLoadingEnabled(Mod mod) => SeaShrimpGate.Enabled;
    }

    internal abstract class SeaShrimpModProjectile : ModProjectile
    {
        public override bool IsLoadingEnabled(Mod mod) => SeaShrimpGate.Enabled;
    }

    internal abstract class SeaShrimpModSystem : ModSystem
    {
        public override bool IsLoadingEnabled(Mod mod) => SeaShrimpGate.Enabled;
    }
}
