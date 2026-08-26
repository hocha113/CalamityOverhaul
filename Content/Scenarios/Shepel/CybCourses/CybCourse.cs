using CalamityOverhaul.Common;
using CalamityOverhaul.Content.HackTimes;
using CalamityOverhaul.Content.RAMSystems;
using InnoVault.Narrative.Runtime;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shepel.CybCourses
{
    /// <summary>教程子世界环境曲认领：子世界环境档，在场即播</summary>
    internal sealed class CybCourseMusicClaim : MusicClaim
    {
        public override MusicTier Tier => MusicTier.SubworldAmbience;
        public override bool ShouldPlay() => CybCourse.IsActive;
        public override int GetMusicSlot() => MusicLoader.GetMusicSlot("CalamityOverhaul/Assets/Sounds/Music/AloneInTheBackalleys");
    }

    //Enter/Exit/Restart入口
    internal class CybCourse : ModSystem
    {
        //接受教程后回主世界发凭证；静态，子世界存档不同步
        private static bool _grantMewtwoOnExit;

        public static bool IsActive => CybCourseWorld.Active;

        public static void Enter() {
            ClearCrossWorldRefs(Main.LocalPlayer);
            CybCourseWorldGuard.Snapshot();
            CybCourseWorld.Enter();
        }

        //跨世界chest/sign/TileEntity索引→FindRecipes NRE(进出都清)
        private static void ClearCrossWorldRefs(Player p) {
            if (p == null || !p.active) {
                return;
            }
            p.chest = -1;
            p.sign = -1;
            p.SetTalkNPC(-1, fromNet: false);
            p.tileEntityAnchor.Clear();
            Main.npcChatText = string.Empty;
        }

        //FirstMetShepel_CybCourseAccept进子世界前调用
        internal static void ScheduleMewtwoGrant() => _grantMewtwoOnExit = true;

        //CybCoursePlayer.OnEnterWorld消费
        internal static bool TryConsumeGrantMewtwo() {
            if (!_grantMewtwoOnExit) return false;
            _grantMewtwoOnExit = false;
            return true;
        }

        /// <summary>退出子世界，清InfiniteHack/Outro/完成面板</summary>
        public static void Exit() {
            CybCourseCompletePanel.Hide();
            CybCourseKeyBindReminderPanel.Hide();
            NarrativeRunner.Reset();
            HackTime.InfiniteHack = false;
            ClearCrossWorldRefs(Main.LocalPlayer);
            CybCourseWorld.Exit();
        }

        /// <summary>RETRY软重启，不reload子世界</summary>
        public static void Restart() {
            CybCourseCompletePanel.Hide();

            CybTutorialLead.ResetForRetry();
            HackTimeTutorialLead.ResetForRetry();
            WheelTutorialLead.ResetForRetry();

            CybCourseGen.RestoreSnapshot();

            HackTime.Reset();
            RamSystem.Refill();
            Player p = Main.LocalPlayer;
            if (p != null && p.active) {
                p.Center = new Vector2(
                    CybCourseGen.SpawnTileX * 16f + 8f,
                    CybCourseGen.SpawnTileY * 16f - p.height * 0.5f);
                p.velocity = Vector2.Zero;
                p.statLife = p.statLifeMax2;
            }
        }

        public override void PostUpdateEverything() {
            if (!IsActive) {
                return;
            }
            //环境曲走 CybCourseMusicClaim 认领
            AddDeckLight();
        }

        //甲板自发冷青光：虚空夜空下平台不能漆黑一片
        private static void AddDeckLight() {
            if (Main.dedServ) {
                return;
            }
            float deckY = (CybCourseGen.SurfaceY - 1) * 16f;
            for (int x = CybCourseGen.PlatformLeft; x <= CybCourseGen.PlatformRight; x += 6) {
                Lighting.AddLight(new Vector2(x * 16f, deckY), 0.10f, 0.30f, 0.36f);
            }
            //装饰浮岛给微光，否则贴着近黑天幕根本看不见
            foreach (var (x0, x1, yTop) in CybCourseGen.AccentIslets) {
                Lighting.AddLight(
                    new Vector2((x0 + x1) * 0.5f * 16f, (yTop - 1) * 16f),
                    0.05f, 0.16f, 0.20f);
            }
        }
    }
}
