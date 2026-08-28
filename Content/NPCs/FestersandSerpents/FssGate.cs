using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.FestersandSerpents
{
    /// <summary>脓蕾沙蟒内容门闸：出问题时改 Enabled 一键下线整树</summary>
    internal static class FssGate
    {
        internal const bool Enabled = true;
    }

    internal abstract class FssModItem : ModItem
    {
        public override bool IsLoadingEnabled(Mod mod) => FssGate.Enabled;
    }

    internal abstract class FssModNPC : ModNPC
    {
        public override bool IsLoadingEnabled(Mod mod) => FssGate.Enabled;
    }

    internal abstract class FssModProjectile : ModProjectile
    {
        public override bool IsLoadingEnabled(Mod mod) => FssGate.Enabled;
    }

    internal abstract class FssModSystem : ModSystem
    {
        public override bool IsLoadingEnabled(Mod mod) => FssGate.Enabled;
    }
}
