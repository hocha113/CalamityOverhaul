using CalamityOverhaul.Content.HackTimes;
using CalamityOverhaul.Content.RAMSystems;
using InnoVault.Narrative.Runtime;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shepel.CybCourses
{
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
            Main.newMusic = Main.musicBox2 = MusicLoader.GetMusicSlot("CalamityOverhaul/Assets/Sounds/Music/AloneInTheBackalleys");
        }
    }
}
