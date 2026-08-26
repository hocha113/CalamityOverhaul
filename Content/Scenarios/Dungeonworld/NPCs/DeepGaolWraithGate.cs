using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.NPCs
{
    /// <summary>深牢怨灵内容树总门禁（2026-08-27 重做批交付开启）；关闭 Enabled 即整树下线</summary>
    internal static class DeepGaolWraithGate
    {
        internal const bool Enabled = true;
    }

    internal abstract class GaolModItem : ModItem
    {
        public override bool IsLoadingEnabled(Mod mod) => DeepGaolWraithGate.Enabled;
    }

    internal abstract class GaolModNPC : ModNPC
    {
        public override bool IsLoadingEnabled(Mod mod) => DeepGaolWraithGate.Enabled;
    }

    internal abstract class GaolModProjectile : ModProjectile
    {
        public override bool IsLoadingEnabled(Mod mod) => DeepGaolWraithGate.Enabled;
    }

    internal abstract class GaolModSystem : ModSystem
    {
        public override bool IsLoadingEnabled(Mod mod) => DeepGaolWraithGate.Enabled;
    }
}
