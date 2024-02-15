using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Summon.Extras;
using Microsoft.Xna.Framework.Graphics;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content
{
    public enum WhipHitTypeEnum : byte
    {
        ElementWhip = 1,
        BleedingScourge,
        AzureDragonRage,
        GhostFireWhip,
        WhiplashGalactica,
        AllhallowsGoldWhip
    }

    public static class WhipHitDate
    {
        public static Texture2D Tex(WhipHitTypeEnum hitType) {
            switch (hitType) {
                case WhipHitTypeEnum.ElementWhip:
                    return CWRUtils.GetT2DValue(ModContent.GetInstance<ElementWhip>().Texture);
                case WhipHitTypeEnum.BleedingScourge:
                    return CWRUtils.GetT2DValue(ModContent.GetInstance<BleedingScourge>().Texture);
                case WhipHitTypeEnum.AzureDragonRage:
                    return CWRUtils.GetT2DValue(ModContent.GetInstance<AzureDragonRage>().Texture);
                case WhipHitTypeEnum.GhostFireWhip:
                    return CWRUtils.GetT2DValue(ModContent.GetInstance<GhostFireWhip>().Texture);
                case WhipHitTypeEnum.WhiplashGalactica:
                    return CWRUtils.GetT2DValue(ModContent.GetInstance<WhiplashGalactica>().Texture);
                case WhipHitTypeEnum.AllhallowsGoldWhip:
                    return CWRUtils.GetT2DValue(ModContent.GetInstance<AllhallowsGoldWhip>().Texture);
                default:
                    return CWRUtils.GetT2DValue(CWRConstant.Placeholder);
            }
        }
    }
}
