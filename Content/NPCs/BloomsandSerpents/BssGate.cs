using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BloomsandSerpents
{
    /// <summary>荒花沙蟒内容门闸：出问题时改 Enabled 一键下线整树</summary>
    internal static class BssGate
    {
        internal const bool Enabled = true;
    }

    internal abstract class BssModItem : ModItem
    {
        public override bool IsLoadingEnabled(Mod mod) => BssGate.Enabled;
    }

    internal abstract class BssModNPC : ModNPC
    {
        public override bool IsLoadingEnabled(Mod mod) => BssGate.Enabled;
    }

    internal abstract class BssModProjectile : ModProjectile
    {
        public override bool IsLoadingEnabled(Mod mod) => BssGate.Enabled;
    }

    internal abstract class BssModSystem : ModSystem
    {
        public override bool IsLoadingEnabled(Mod mod) => BssGate.Enabled;
    }
}
