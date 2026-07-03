using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.Onikiris.OniDomains
{
    /// <summary>鬼域 ModSystem 驱动器：状态推进、装饰更新、光照接管</summary>
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

        //里世界：世界的光真的灭了，只留灯笼
        public override void ModifyLightingBrightness(ref float scale) {
            float ura = OniDomain.LocalUraSmooth;
            if (ura > 0.001f) {
                scale *= 1f - 0.60f * ura;
            }
        }

        public override void ModifySunLightColor(ref Color tileColor, ref Color backgroundColor) {
            float ura = OniDomain.LocalUraSmooth;
            if (ura <= 0.001f) {
                return;
            }
            //阴间月色：冷灰蓝，压掉日光
            Color uraTile = new(34, 36, 48);
            Color uraBg = new(14, 14, 22);
            tileColor = Color.Lerp(tileColor, uraTile, ura);
            backgroundColor = Color.Lerp(backgroundColor, uraBg, ura);
        }
    }
}
