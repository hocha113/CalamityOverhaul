using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.NPCs
{
    /// <summary>不溺者内容树总门禁（2026-08-27 重做批交付开启，镜像 DeepGaolWraithGate）；关闭 Enabled 即整树下线</summary>
    internal static class UndrownedGate
    {
        internal const bool Enabled = true;
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
