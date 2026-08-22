using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces.Banish;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces.DomainFreeze;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces.Restart;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces.Teleport;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces
{
    /// <summary>赛博空间 System，PostUpdateEverything 推进子系统；L3 接管期驱动天空与光照</summary>
    internal class CyberspaceSystem : ModSystem
    {
        public override void PostUpdateEverything() {
            //先定主导域再推进：表现闸门按上一帧相位选，差一帧不可见
            Cyberspace.RefreshViewed();
            Cyberspace.Update();
            CyberBanish.Update();
            CyberBossExecution.Update();
            CyberDomainFreeze.Update();
            CyberTeleport.Update();
            CyberRestart.Update();
            if (!Main.dedServ) {
                UpdateSkyActivation();
                CyberspaceDeco.Update();
            }
        }

        //领域是玩家主动能力，不进 ModSceneEffect 场景竞争；
        //激活期间每帧重激活，把本天空挤到活动链表末尾压过其他天空

        private static void UpdateSkyActivation() {
            CustomSky sky = SkyManager.Instance[CyberspaceSky.Name];
            if (sky == null) {
                return;
            }
            bool active = Cyberspace.ViewedTakeover > 0.003f;
            if (active) {
                SkyManager.Instance.Activate(CyberspaceSky.Name);
                if (!Filters.Scene[CyberspaceSky.Name].IsActive()) {
                    Filters.Scene.Activate(CyberspaceSky.Name);
                }
            }
            else {
                if (sky.IsActive()) {
                    SkyManager.Instance.Deactivate(CyberspaceSky.Name);
                }
                if (Filters.Scene[CyberspaceSky.Name].IsActive()) {
                    Filters.Scene[CyberspaceSky.Name].Deactivate();
                }
            }
        }

        //接管压光：氛围级而非致盲级，战斗可读性优先

        public override void ModifyLightingBrightness(ref float scale) {
            float t = Cyberspace.ViewedTakeover;
            if (t > 0.001f) {
                scale *= 1f - 0.30f * t;
            }
        }

        public override void ModifySunLightColor(ref Color tileColor, ref Color backgroundColor) {
            float t = Cyberspace.ViewedTakeover;
            if (t <= 0.001f) {
                return;
            }
            //日光换色而非熄灭：染色重心在背景，背景沉黑让地形剪影剥出来(纵深线索)，
            //地砖只轻染，黑墙气质靠"黑场景+红轮廓"的对比而非满屏红罩
            Color cyberTile = new(126, 72, 68);
            Color cyberBg = new(40, 18, 20);
            tileColor = Color.Lerp(tileColor, cyberTile, t * 0.35f);
            backgroundColor = Color.Lerp(backgroundColor, cyberBg, t * 0.68f);
        }

        public override void ClearWorld() => ResetAll();

        internal static void ResetAll() {
            Cyberspace.Reset();
            CyberBanish.Reset();
            CyberBossExecution.Reset();
            CyberDomainFreeze.Reset();
            CyberTeleport.Reset();
            CyberRestart.Reset();
        }
    }
}
