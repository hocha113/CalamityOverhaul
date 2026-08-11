using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaVaults;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains
{
    /// <summary>血湖领域系统调度与卸载兜底</summary>
    internal class KikasaDomainSystem : ModSystem
    {
        public override void PostUpdateEverything() {
            if (Main.dedServ) {
                return;
            }
            //先定主导域再推进：本帧的表现闸门按上一帧的相位选，差一帧看不出来
            KikasaDomain.RefreshViewed();
            KikasaDomain.UpdateAll();
            UpdateSkyActivation();
            KikasaDomainDeco.Update();
            KikasaLakeFX.Update();
        }

        //领域是玩家主动能力，不进 ModSceneEffect 场景竞争；激活期间每帧重激活，
        //SkyManager.OnActivate 把本天空移到活动链表末尾、永远最后绘制压过其他天空

        private static void UpdateSkyActivation() {
            CustomSky sky = SkyManager.Instance[KikasaDomainSky.Name];
            if (sky == null) {
                return;
            }
            bool active = KikasaDomain.Viewed?.AnyActive ?? false;
            if (active) {
                SkyManager.Instance.Activate(KikasaDomainSky.Name);
                if (!Filters.Scene[KikasaDomainSky.Name].IsActive()) {
                    Filters.Scene.Activate(KikasaDomainSky.Name);
                }
            }
            else {
                if (sky.IsActive()) {
                    SkyManager.Instance.Deactivate(KikasaDomainSky.Name);
                }
                if (Filters.Scene[KikasaDomainSky.Name].IsActive()) {
                    Filters.Scene[KikasaDomainSky.Name].Deactivate();
                }
            }
        }

        public override void ClearWorld() {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < Main.maxPlayers; i++) {
                if (Main.player[i]?.TryGetModPlayer(out KikasaDomainPlayer domain) == true) {
                    domain.ResetDomain();
                }
            }
            KikasaDomain.RefreshViewed();
            KikasaDomainDeco.Clear();
            KikasaLakeFX.Clear();
        }

        //血暮压光、氛围级而非致盲级：湖面反光与天空亮红反衬剪影；鬼雨异化再压一档

        public override void ModifyLightingBrightness(ref float scale) {
            float presence = KikasaDomain.ViewedPresence;
            if (presence > 0.001f) {
                scale *= 1f - (0.22f + 0.10f * KikasaDomain.ViewedRainBlend) * presence;
            }
        }

        public override void ModifySunLightColor(ref Color tileColor, ref Color backgroundColor) {
            float presence = KikasaDomain.ViewedPresence;
            if (presence <= 0.001f) {
                return;
            }

            //血暮染色：物块留可读性，背景染重些让地形剪影从血空里剥出来；
            //鬼雨异化转冷雨压顶（湿墨色板），染得更重更沉

            float rain = KikasaDomain.ViewedRainBlend;
            Color duskTile = Color.Lerp(new(214, 96, 82), new(52, 62, 68), rain);
            Color duskBg = Color.Lerp(new(126, 34, 32), new(34, 42, 48), rain);
            tileColor = Color.Lerp(tileColor, duskTile, presence * MathHelper.Lerp(0.4f, 0.55f, rain));
            backgroundColor = Color.Lerp(backgroundColor, duskBg, presence * MathHelper.Lerp(0.5f, 0.72f, rain));
        }
    }
}
