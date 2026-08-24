using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.ScrapCommanders
{
    /// <summary>废钢统帅未完成，整树不进游戏；改 Enabled 即重新加载</summary>
    internal static class ScrapCommanderGate
    {
        internal const bool Enabled = false;
    }

    internal abstract class ScrapModItem : ModItem
    {
        public override bool IsLoadingEnabled(Mod mod) => ScrapCommanderGate.Enabled;
    }

    internal abstract class ScrapModNPC : ModNPC
    {
        public override bool IsLoadingEnabled(Mod mod) => ScrapCommanderGate.Enabled;
    }

    internal abstract class ScrapModProjectile : ModProjectile
    {
        public override bool IsLoadingEnabled(Mod mod) => ScrapCommanderGate.Enabled;
    }

    internal abstract class ScrapModSystem : ModSystem
    {
        public override bool IsLoadingEnabled(Mod mod) => ScrapCommanderGate.Enabled;
    }
}
