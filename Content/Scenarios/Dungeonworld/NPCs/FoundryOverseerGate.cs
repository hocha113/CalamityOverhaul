using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.NPCs
{
    /// <summary>铸造监工内容树总门禁（2026-08-27 重做批交付开启，镜像 UndrownedGate）；关闭 Enabled 即整树下线。
    /// 注意：击杀记录载体 DungeonworldBossRecords 由三座 Boss 共用，
    /// 其加载门=任一 Boss 门禁开启</summary>
    internal static class FoundryOverseerGate
    {
        internal const bool Enabled = true;
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
