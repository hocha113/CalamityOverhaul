using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.NPCs
{
    /// <summary>深牢怨灵未完成，整树不进游戏；改 Enabled 即重新加载</summary>
    internal static class DeepGaolWraithGate
    {
        internal const bool Enabled = false;
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
