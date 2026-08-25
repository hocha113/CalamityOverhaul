using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.NPCs
{
    /// <summary>铸造监工未完成，整树不进游戏；改 Enabled 即重新加载（镜像 UndrownedGate）。
    /// 注意：击杀记录载体 DungeonworldBossRecords 由两座 Boss 共用，
    /// 其加载门=UndrownedGate.Enabled || FoundryOverseerGate.Enabled</summary>
    internal static class FoundryOverseerGate
    {
        internal const bool Enabled = false;
    }

    internal abstract class OverseerModItem : ModItem
    {
        public override bool IsLoadingEnabled(Mod mod) => FoundryOverseerGate.Enabled;
    }

    internal abstract class OverseerModNPC : ModNPC
    {
        public override bool IsLoadingEnabled(Mod mod) => FoundryOverseerGate.Enabled;
    }

    internal abstract class OverseerModProjectile : ModProjectile
    {
        public override bool IsLoadingEnabled(Mod mod) => FoundryOverseerGate.Enabled;
    }

    internal abstract class OverseerModSystem : ModSystem
    {
        public override bool IsLoadingEnabled(Mod mod) => FoundryOverseerGate.Enabled;
    }

    internal abstract class OverseerModPlayer : ModPlayer
    {
        public override bool IsLoadingEnabled(Mod mod) => FoundryOverseerGate.Enabled;
    }
}
