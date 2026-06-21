using CalamityOverhaul.Content.HackTimes;
using CalamityOverhaul.Content.Narrative.Runtime;
using CalamityOverhaul.Content.RAMSystems;
using InnoVault.Narrative.Runtime;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shepel.CybCourses
{
    //超梦教程关卡的入口控制
    //通过调用CybCourseWorld.Enter()进入，CybCourseWorld.Exit()退出
    //RETRY软重启时调用CybCourse.Restart()，不需要重新加载子世界
    internal class CybCourse : ModSystem
    {
        //接受教程后进入子世界前置为true，退出时发放超梦接入凭证
        //用静态字段而非存档标记，避免子世界存档与主世界存档不同步导致标记丢失
        private static bool _grantMewtwoOnExit;

        public static bool IsActive => CybCourseWorld.Active;

        public static void Enter() {
            //在切换子世界前清理玩家身上的"跨世界引用"，否则进入子世界后
            //RecipeBrowser/MagicStorage/Fargo 等 Mod 在 OnEnterWorld 里调 FindRecipes 时
            //Recipe.CollectItemsToCraftWithFrom 会拿主世界的 chest/sign/TileEntity 索引去访问子世界的
            //Main.chest[] / TileEntity.ByID（已被换成空的），从而抛 NullReferenceException 闪退
            Player p = Main.LocalPlayer;
            if (p != null && p.active) {
                p.chest = -1;
                p.sign = -1;
                p.SetTalkNPC(-1, fromNet: false);
                p.tileEntityAnchor.Clear();
                Main.npcChatText = string.Empty;
            }
            //进入子世界前拍主世界快照，回主世界时补 Boss 进度与城镇 NPC
            CybCourseWorldGuard.Snapshot();
            CybCourseWorld.Enter();
        }

        //由FirstMetShepel_CybCourseAccept在进入子世界前调用，标记回到主世界后需发放凭证
        internal static void ScheduleMewtwoGrant() => _grantMewtwoOnExit = true;

        //回到主世界时由CybCoursePlayer.OnEnterWorld调用，返回true表示需要发放
        internal static bool TryConsumeGrantMewtwo() {
            if (!_grantMewtwoOnExit) return false;
            _grantMewtwoOnExit = false;
            return true;
        }

        /// <summary>
        /// 退出教程子世界，清理 InfiniteHack/Outro/完成面板
        /// </summary>
        public static void Exit() {
            CybCourseCompletePanel.Hide();
            CybCourseKeyBindReminderPanel.Hide();
            NarrativeRunner.Reset();
            HackTime.InfiniteHack = false;
            CybCourseWorld.Exit();
        }

        /// <summary>
        /// RETRY 软重启：不 reload 子世界，重置教程状态并重生测试 NPC
        /// </summary>
        public static void Restart() {
            //1. 关闭面板
            CybCourseCompletePanel.Hide();

            //2. 重置教程 ModSystem 内部状态（包括清理 SantaNK1）
            CybTutorialLead.ResetForRetry();
            HackTimeTutorialLead.ResetForRetry();

            //3. 回滚物块到生成时的快照（包括墙体/帧数据/液体/坡度），并重新挂载MK2 的 TP 实体
            CybCourseGen.RestoreSnapshot();

            //4. 教程对话由 ResetForRetry 清零 _introAttempted，可再次自动开场

            //5. 重置玩家位置 / RAM / 骇客时间
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

            //6. 触发开场；_introAttempted 已在 ResetForRetry 清零
        }

        public override void PostUpdateEverything() {
            if (!IsActive) {
                return;
            }
            Main.newMusic = Main.musicBox2 = MusicLoader.GetMusicSlot("CalamityOverhaul/Assets/Sounds/Music/AloneInTheBackalleys");
        }
    }
}
