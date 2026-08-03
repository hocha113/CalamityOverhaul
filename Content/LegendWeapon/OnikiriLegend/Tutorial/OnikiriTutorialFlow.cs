using Microsoft.Xna.Framework;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Tutorial
{
    /// <summary>鬼切教程状态机的本地玩家门面</summary>
    internal static class OnikiriTutorialFlow
    {
        internal const int Step_HudIntro = 0;
        internal const int Step_Mei = 1;
        internal const int Step_Register = 2;
        internal const int Step_Prepare = 3;
        internal const int Step_OpenOmote = 4;
        internal const int Step_FlipUra = 5;
        internal const int Step_Dismember = 6;
        internal const int Step_Backlash = 7;
        internal const int Step_CloseEye = 8;
        internal const int Step_Done = 9;

        internal const int Checkpoint_Hud = 1;
        internal const int AssistDelayFrames = 60 * 12;
        internal const int LegacySkipDelayFrames = 60 * 9;

        private static OnikiriTutorialPlayer Local
            => Main.LocalPlayer?.GetModPlayer<OnikiriTutorialPlayer>();

        internal static int CurrentStep => Local?.CurrentStep ?? -1;
        internal static int StepTimer => Local?.StepTimer ?? 0;
        internal static bool IsRunning => Local?.IsRunning ?? false;
        internal static OnikiriTutorialFeedback Feedback
            => Local?.Feedback ?? OnikiriTutorialFeedback.None;
        internal static NPC TutorialTarget => Local?.TutorialTarget;

        internal static void Tick(GameTime _)
            => Local?.TickTutorial();

        internal static void HandlePrimaryAction()
            => Local?.HandlePrimaryAction();

        internal static void HandleSecondaryAction()
            => Local?.HandleSecondaryAction();

        internal static bool PollTutorialUiClick(bool mouseDown)
            => Local?.PollTutorialUiClick(mouseDown) == true;

        internal static void Reset()
            => Local?.ResetAllRuntime();

        internal static void ResetIfHolderLost()
            => Local?.Suspend(releaseTarget: true);

        internal static void DeferAfterQueueAbandon()
            => Local?.DeferAfterQueueAbandon();

        internal static bool TryGetRequiredDismemberTarget(Player player, out NPC target)
        {
            target = null;
            if (player == null || player.whoAmI != Main.myPlayer
                || CurrentStep != Step_Dismember) {
                return false;
            }
            target = TutorialTarget;
            return true;
        }

        internal static void NotifyDismemberMiss(Player player)
        {
            if (player?.whoAmI == Main.myPlayer) {
                Local?.NotifyDismemberMiss();
            }
        }

        internal static bool TryConsumeDismemberInput(Player player)
            => player?.whoAmI == Main.myPlayer && Local?.TryConsumeDismemberInput() == true;

        internal static void BeginMeiTransaction(Inscriptions.OniMeiStore current)
            => Local?.BeginMeiTransaction(current);

        internal static void RestoreMeiSnapshotOnClose()
            => Local?.RestoreMeiSnapshotIfNeeded();
    }
}
