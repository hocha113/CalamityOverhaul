using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.NPCs
{
    /// <summary>不溺者未完成，整树不进游戏；改 Enabled 即重新加载（镜像 DeepGaolWraithGate）</summary>
    internal static class UndrownedGate
    {
        internal const bool Enabled = false;
    }

    internal abstract class UndrownedModItem : ModItem
    {
        public override bool IsLoadingEnabled(Mod mod) => UndrownedGate.Enabled;
    }

    internal abstract class UndrownedModNPC : ModNPC
    {
        public override bool IsLoadingEnabled(Mod mod) => UndrownedGate.Enabled;
    }

    internal abstract class UndrownedModProjectile : ModProjectile
    {
        public override bool IsLoadingEnabled(Mod mod) => UndrownedGate.Enabled;
    }

    internal abstract class UndrownedModSystem : ModSystem
    {
        public override bool IsLoadingEnabled(Mod mod) => UndrownedGate.Enabled;
    }

    internal abstract class UndrownedModPlayer : ModPlayer
    {
        public override bool IsLoadingEnabled(Mod mod) => UndrownedGate.Enabled;
    }
}
