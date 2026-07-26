using Terraria;
using Terraria.Graphics.Effects;
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
            UpdateSkyActivation();
            OniDomainDeco.Update();
        }

        //领域是玩家主动能力，不进 ModSceneEffect 场景竞争，不占 boss 的天空/音乐槽位

        //激活期间每帧重激活，SkyManager.OnActivate 把本天空移到活动链表末尾、永远最后绘制压过其他天空

        private static void UpdateSkyActivation() {
            CustomSky sky = SkyManager.Instance[OniDomainSky.Name];
            if (sky == null) {
                return;
            }
            bool active = OniDomain.Local?.AnyActive ?? false;
            if (active) {
                SkyManager.Instance.Activate(OniDomainSky.Name);
                if (!Filters.Scene[OniDomainSky.Name].IsActive()) {
                    Filters.Scene.Activate(OniDomainSky.Name);
                }
            }
            else {
                if (sky.IsActive()) {
                    SkyManager.Instance.Deactivate(OniDomainSky.Name);
                }
                if (Filters.Scene[OniDomainSky.Name].IsActive()) {
                    Filters.Scene[OniDomainSky.Name].Deactivate();
                }
            }
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
            OniDomainPlayer domain = OniDomain.Local;
            float ura = domain?.UraSmooth ?? 0f;
            float omote = 0f;
            if (domain != null && domain.AnyActive && !domain.WorldIsUra) {
                omote = MathHelper.Clamp(domain.SpreadProgress, 0f, 1f) * (1f - ura);
            }
            if (omote <= 0.001f && ura <= 0.001f) {
                return;
            }

            //露天区域补一层柔和暮光，地下遮光仍由原版传播规则保留

            if (omote > 0.001f) {
                Color omoteTile = new(236, 166, 100);
                Color omoteBg = new(176, 111, 76);
                tileColor = Color.Lerp(tileColor, omoteTile, omote * 0.42f);
                backgroundColor = Color.Lerp(backgroundColor, omoteBg, omote * 0.32f);
            }

            //月光级冷灰蓝，日光换色而非熄灭

            Color uraTile = new(92, 97, 122);
            Color uraBg = new(46, 48, 62);
            tileColor = Color.Lerp(tileColor, uraTile, ura);
            backgroundColor = Color.Lerp(backgroundColor, uraBg, ura);
        }
    }
}
