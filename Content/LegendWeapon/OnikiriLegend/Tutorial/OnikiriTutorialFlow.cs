using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Tutorial
{
    /// <summary>鬼切教程状态机的本地玩家门面</summary>
    internal static class OnikiriTutorialFlow
    {
        /// <summary>开场询问,只在从未答复过的存档上出现</summary>
        internal const int Step_Ask = 0;
        internal const int Step_HudIntro = 1;
        internal const int Step_Mei = 2;
        internal const int Step_Codex = 3;
        //步号已落过档，改名可以，改值不行
        internal const int Step_Sigil = 4;
        internal const int Step_Domain = 5;
        internal const int Step_Prepare = 6;
        internal const int Step_OpenOmote = 7;
        internal const int Step_FlipUra = 8;
        internal const int Step_Dismember = 9;
        internal const int Step_Backlash = 10;
        internal const int Step_CloseEye = 11;
        internal const int Step_Done = 12;

        /// <summary>
        /// 存档里的讲解进度 = 已读完的最后一张讲解卡的步号(0 = 一张没读)。
        /// 逐张写点,中途死亡或退世界后从下一张接着讲,不从头翻一遍
        /// </summary>
        internal const int Checkpoint_ExplainDone = Step_Domain;
        internal const int AssistDelayFrames = 60 * 12;
        internal const int LegacySkipDelayFrames = 60 * 9;
        /// <summary>实操步兜底:久攻不下就允许直接跳过本步</summary>
        internal const int PracticeSkipDelayFrames = 60 * 35;
        /// <summary>讲解步终极自动推进:玩家找不到该点哪也不会永远停在这</summary>
        internal const int ExplainAutoAdvanceFrames = 60 * 75;
        /// <summary>实操准备的强推期限,越过后不再等相位稳态</summary>
        internal const int PrepareTimeoutFrames = 60 * 20;
        /// <summary>进步骤时鬼域就不在预期相位,给这么久归位,超时按现状认账</summary>
        internal const int PracticePrimeGraceFrames = 60 * 6;
        /// <summary>行囊持续无刀多久后自动收起教习并补符</summary>
        internal const int NeedBladeAbortFrames = 60 * 12;

        /// <summary>纯讲解步:只靠读与点,失败不影响世界状态</summary>
        internal static bool IsExplanatoryStep(int step)
            => step is >= Step_HudIntro and <= Step_Domain;

        /// <summary>实操步:动鬼域或落刀,卡片退到边角且不吞世界点击</summary>
        internal static bool IsPracticeStep(int step)
            => step is >= Step_Prepare and < Step_Done;

        private static OnikiriTutorialPlayer Local
            => Main.LocalPlayer?.GetModPlayer<OnikiriTutorialPlayer>();

        internal static int CurrentStep => Local?.CurrentStep ?? -1;
        internal static int StepTimer => Local?.StepTimer ?? 0;
        internal static bool IsRunning => Local?.IsRunning ?? false;
        internal static OnikiriTutorialFeedback Feedback
            => Local?.Feedback ?? OnikiriTutorialFeedback.None;
        internal static bool CanSkipPracticeStep => Local?.CanSkipPracticeStep == true;
        /// <summary>询问卡走的是"旧档补讲"版本的措辞</summary>
        internal static bool IsRefresherAsk => Local?.IsRefresherAsk == true;
        internal static NPC TutorialTarget => Local?.TutorialTarget;

        internal static void Tick(GameTime _)
            => Local?.TickTutorial();

        internal static void HandlePrimaryAction()
            => Local?.HandlePrimaryAction();

        internal static void HandleSecondaryAction()
            => Local?.HandleSecondaryAction();

        /// <summary>卡片右上角的收起:任何步骤都能退出,并补一枚稽古符</summary>
        internal static void HandleAbortAction()
            => Local?.AbortTutorial();

        internal static bool PollTutorialUiClick(bool mouseDown)
            => Local?.PollTutorialUiClick(mouseDown) == true;

        internal static void Reset()
            => Local?.ResetAllRuntime();

        internal static void ResetIfHolderLost()
            => Local?.Suspend(releaseTarget: true);

        internal static void DeferAfterQueueAbandon()
            => Local?.DeferAfterQueueAbandon();

        internal static bool TryGetRequiredDismemberTarget(Player player, out NPC target) {
            target = null;
            if (player == null || player.whoAmI != Main.myPlayer
                || CurrentStep != Step_Dismember) {
                return false;
            }
            target = TutorialTarget;
            return true;
        }

        internal static void NotifyDismemberMiss(Player player) {
            if (player?.whoAmI == Main.myPlayer) {
                Local?.NotifyDismemberMiss();
            }
        }

        internal static bool TryConsumeDismemberInput(Player player)
            => player?.whoAmI == Main.myPlayer && Local?.TryConsumeDismemberInput() == true;

        internal static void BeginMeiTransactionOnOpen()
            => Local?.BeginMeiTransaction();

        internal static void RestoreMeiSnapshotOnClose()
            => Local?.RestoreMeiSnapshotIfNeeded();
    }
}
