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
            //先定主导域再推进：本帧的表现闸门按上一帧的相位选，差一帧看不出来
            OniDomain.RefreshViewed();
            OniDomain.UpdateAll();
            UpdateSkyActivation();
            OniDomainDeco.Update();
        }

        //领域是玩家主动能力，不进 ModSceneEffect 场景竞争，不占 boss 的天空/音乐槽位

        //激活期间每帧重激活：SkyManager.OnActivate 把本天空移到活动链表末尾、永远最后绘制，
        //领域背景覆盖优先级最高，Boss/事件天空一律被压是设计意图（拍板 2026/8/31，
        //回滚 0.9202 反馈十一·#40 时误改的"上升沿让位"策略），与血湖 KikasaDomainSystem 同策

        private static void UpdateSkyActivation() {
            CustomSky sky = SkyManager.Instance[OniDomainSky.Name];
            if (sky == null) {
                return;
            }
            bool active = OniDomain.Viewed?.AnyActive ?? false;
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
            for (int i = 0; i < Main.maxPlayers; i++) {
                if (Main.player[i]?.TryGetModPlayer(out OniDomainPlayer domain) == true) {
                    domain.ResetDomain();
                }
            }
            OniDomain.RefreshViewed();
            OniDomainDeco.Clear();
            //与血湖天空同型的卸世界收场:标题界面没人跑关闭分支(同反馈四·#16)
            CustomSky sky = SkyManager.Instance[OniDomainSky.Name];
            if (sky != null) {
                if (sky.IsActive()) {
                    SkyManager.Instance.Deactivate(OniDomainSky.Name);
                }
                sky.Reset();
            }
            Filter filter = Filters.Scene[OniDomainSky.Name];
            if (filter != null) {
                if (filter.IsActive()) {
                    filter.Deactivate();
                }
                filter.GetShader()?.UseOpacity(0f);
            }
        }

        //里世界压光、氛围级而非致盲级，剪影可读性靠淡色雾空反衬

        public override void ModifyLightingBrightness(ref float scale) {
            float ura = OniDomain.ViewedUraSmooth;
            if (ura > 0.001f) {
                scale *= 1f - 0.35f * ura;
            }
        }

        public override void ModifySunLightColor(ref Color tileColor, ref Color backgroundColor) {
            OniDomainPlayer domain = OniDomain.Viewed;
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
