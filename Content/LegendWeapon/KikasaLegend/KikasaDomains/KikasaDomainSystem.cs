using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDreams;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaVaults;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaWisps;
using System;
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
                //服务器只推进状态机镜像（快照喂入+确定性跟跑），表现层全数跳过
                KikasaDomain.UpdateAll();
                return;
            }
            //先定主导域再推进：本帧的表现闸门按上一帧的相位选，差一帧看不出来
            KikasaDomain.RefreshViewed();
            KikasaDomain.UpdateAll();
            UpdateSkyActivation();
            KikasaDomainDeco.Update();
            KikasaDiveClearing.Update();
            KikasaLakeFX.Update();
            KikasaWispFX.Update();
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

            //鬼梦天空：吃观看域的 DreamBlend，两片天空靠各自 alpha 交叉渐变
            CustomSky dreamSky = SkyManager.Instance[KikasaDreamSky.Name];
            if (dreamSky == null) {
                return;
            }
            bool dreamActive = KikasaDomain.ViewedDreamBlend > 0.01f;
            if (dreamActive) {
                SkyManager.Instance.Activate(KikasaDreamSky.Name);
                if (!Filters.Scene[KikasaDreamSky.Name].IsActive()) {
                    Filters.Scene.Activate(KikasaDreamSky.Name);
                }
            }
            else {
                if (dreamSky.IsActive()) {
                    SkyManager.Instance.Deactivate(KikasaDreamSky.Name);
                }
                if (Filters.Scene[KikasaDreamSky.Name].IsActive()) {
                    Filters.Scene[KikasaDreamSky.Name].Deactivate();
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
            KikasaDiveClearing.Clear();
            KikasaLakeFX.Clear();
            KikasaHoundReflection.Clear();
            KikasaWispFX.Clear();
            //卸世界后 PostUpdateEverything 不再跑,没人走关闭分支——
            //血湖/鬼梦天空与滤镜会一路残留到标题界面(反馈四·#16),这里强制收场
            ForceDeactivateSky(KikasaDomainSky.Name);
            ForceDeactivateSky(KikasaDreamSky.Name);
        }

        /// <summary>强关一片天空与同名滤镜:Deactivate 走淡出、Reset 立即断,滤镜透明度一并归零</summary>
        private static void ForceDeactivateSky(string name) {
            CustomSky sky = SkyManager.Instance[name];
            if (sky != null) {
                if (sky.IsActive()) {
                    SkyManager.Instance.Deactivate(name);
                }
                sky.Reset();
            }
            Filter filter = Filters.Scene[name];
            if (filter != null) {
                if (filter.IsActive()) {
                    filter.Deactivate();
                }
                filter.GetShader()?.UseOpacity(0f);
            }
        }

        //血暮压光：scale 乘的是逐格衰减率（上游 LightingEngine 把它乘进
        //LightDecayThroughAir/Solid），逐格复利、指数放大——旧值 0.22 让十格外的
        //火把光只剩约 8%，夜晚与地下"领域亮、世界黑"即源于此。
        //氛围交给调色/滤镜/保底天光承担，这里只留极轻的空气变稠感

        public override void ModifyLightingBrightness(ref float scale) {
            float presence = KikasaDomain.ViewedPresence;
            if (presence > 0.001f) {
                float dream = KikasaDomain.ViewedDreamBlend;
                float dim = MathHelper.Lerp(
                    0.03f + 0.02f * KikasaDomain.ViewedRainBlend,
                    //梦里空气再稠一丝，沉感靠梦色板与压光滤镜，不靠掐灭光源
                    0.05f, dream);
                scale *= 1f - dim * presence;
            }
        }

        public override void ModifySunLightColor(ref Color tileColor, ref Color backgroundColor) {
            float presence = KikasaDomain.ViewedPresence;
            if (presence <= 0.001f) {
                return;
            }

            //血暮染色：物块走尘玫瑰低饱和光（红是身份不是照明，玩家/敌人/物块保留本色可读；
            //旧 (214,96,82) 把整个世界染成鲑红，与红天红湖叠成整屏单色相），
            //背景压向暗酒红让地形剪影从天空里剥出来；
            //鬼雨异化转冷雨压顶（湿墨色板），染得更重更沉；鬼梦再向暗红黑深一层

            float rain = KikasaDomain.ViewedRainBlend;
            float dream = KikasaDomain.ViewedDreamBlend;
            Color duskTile = Color.Lerp(new(200, 140, 128), new(52, 62, 68), rain);
            Color duskBg = Color.Lerp(new(62, 22, 36), new(34, 42, 48), rain);
            //梦色板：物块暗红余温，背景压向黑红，地形从红空里剥成剪影
            duskTile = Color.Lerp(duskTile, new(150, 52, 44), dream);
            duskBg = Color.Lerp(duskBg, new(64, 12, 14), dream);
            tileColor = Color.Lerp(tileColor, duskTile,
                presence * MathHelper.Lerp(MathHelper.Lerp(0.4f, 0.55f, rain), 0.60f, dream));
            backgroundColor = Color.Lerp(backgroundColor, duskBg,
                presence * MathHelper.Lerp(MathHelper.Lerp(0.55f, 0.72f, rain), 0.80f, dream));

            //保底环境光（对齐鬼切"日光换色而非熄灭"）：领域自带血暮天光，夜里点不灭。
            //上面的部分插值在夜晚基色近黑时抬不起亮度，这里按形态给露天日光一个下限；
            //白天原有观感高于下限，分毫不动

            Color floorTile = Color.Lerp(new(128, 72, 68), new(84, 102, 110), rain);
            floorTile = Color.Lerp(floorTile, new(118, 46, 42), dream);
            Color floorBg = Color.Lerp(new(56, 22, 30), new(46, 58, 66), rain);
            floorBg = Color.Lerp(floorBg, new(64, 18, 18), dream);
            RaiseToFloor(ref tileColor, floorTile, presence);
            RaiseToFloor(ref backgroundColor, floorBg, presence);
        }

        //逐通道抬到下限：只补不足，不动已亮的白天

        private static void RaiseToFloor(ref Color color, Color floor, float presence) {
            color.R = Math.Max(color.R, (byte)(floor.R * presence));
            color.G = Math.Max(color.G, (byte)(floor.G * presence));
            color.B = Math.Max(color.B, (byte)(floor.B * presence));
        }
    }
}
