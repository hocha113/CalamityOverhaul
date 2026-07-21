using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniDomains
{
    /// <summary>领域系统卸载兜底</summary>
    internal class OniDomainSystem : ModSystem
    {
        public override void PostUpdateEverything() {
            if (Main.dedServ) {
                return;
            }
            OniDomain.Local?.UpdateLocal();
            OniDomainDeco.Update();
        }

        public override void ClearWorld() {
            if (Main.dedServ) {
                return;
            }
            OniDomain.Local?.ResetDomain();
            OniDomainDeco.Clear();
        }

        //里世界压光、氛围级而非致盲级，剪影可读性靠淡色雾空反衬

        public override void ModifyLightingBrightness(ref float scale) {
            float ura = OniDomain.LocalUraSmooth;
            if (ura > 0.001f) {
                scale *= 1f - 0.35f * ura;
            }
        }

        public override void ModifySunLightColor(ref Color tileColor, ref Color backgroundColor) {
            float ura = OniDomain.LocalUraSmooth;
            if (ura <= 0.001f) {
                return;
            }
            //月光级冷灰蓝，日光换色而非熄灭

            Color uraTile = new(92, 97, 122);
            Color uraBg = new(46, 48, 62);
            tileColor = Color.Lerp(tileColor, uraTile, ura);
            backgroundColor = Color.Lerp(backgroundColor, uraBg, ura);
        }
    }
}
