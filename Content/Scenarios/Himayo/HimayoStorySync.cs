using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend;
using CalamityOverhaul.Content.Narrative;
using CalamityOverhaul.Content.Narrative.Data;
using CalamityOverhaul.Content.Narrative.Data.Modules;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Himayo
{
    /// <summary>
    /// 真夜剧情进度同步 + 鬼切试炼发放门禁。<br/>
    /// 正常路径：FirstMetHimayo OnCompleted → PostFirstMetIsComplete。<br/>
    /// 兜底：①初遇已触发且叙事空闲 → 视为播完；②鸟居拔刀后硬性倒计时到期 → 强制解锁
    /// </summary>
    internal static class HimayoStorySync
    {
        /// <summary>拔刀后若初遇未正常落幕，约 90s 强制开试炼（叙事忙碌时不计时）</summary>
        public const int TrialUnlockSafetyDuration = 60 * 90;

        public static HimayoStoryData Story
            => Main.LocalPlayer.GetModPlayer<StoryPlayer>().Get<HimayoStoryData>();

        public static bool FirstMet => Story.FirstMet;

        public static void MarkFirstMet() => Story.FirstMet = true;

        /// <summary>初遇对话已完整播完（试炼委托发放门禁）</summary>
        public static bool PostFirstMetIsComplete => Story.PostFirstMetIsComplete;

        public static void MarkPostFirstMetComplete() {
            Story.PostFirstMetIsComplete = true;
            Story.TrialUnlockSafetyTicks = 0;
        }

        public static bool ToriiSwordTaken => Story.ToriiSwordTaken;

        public static void MarkToriiSwordTaken() {
            Story.ToriiSwordTaken = true;
            ArmTrialUnlockSafety();
        }

        /// <summary>武装硬性倒计时（已完成/已在倒数则不动）</summary>
        public static void ArmTrialUnlockSafety() {
            if (Story.PostFirstMetIsComplete || Story.TrialUnlockSafetyTicks > 0) {
                return;
            }
            Story.TrialUnlockSafetyTicks = TrialUnlockSafetyDuration;
        }

        /// <summary>
        /// 每帧推进倒计时：仅本地玩家；叙事忙碌或初遇正在播时暂停，避免正常演出被抢跑
        /// </summary>
        public static void TickTrialUnlockSafety(Player player) {
            if (player == null || !player.active || player.whoAmI != Main.myPlayer || Main.dedServ) {
                return;
            }
            if (Story.PostFirstMetIsComplete) {
                Story.TrialUnlockSafetyTicks = 0;
                return;
            }
            //仅鸟居拔刀后才走硬倒计时，创造模式刷刀不会误开试炼
            if (!Story.ToriiSwordTaken) {
                return;
            }

            ArmTrialUnlockSafety();
            if (Story.TrialUnlockSafetyTicks <= 0) {
                return;
            }

            if (NarrativeTriggerGate.IsBusy || NarrativeRouter.IsActive<FirstMetHimayo>()) {
                return;
            }

            Story.TrialUnlockSafetyTicks--;
            if (Story.TrialUnlockSafetyTicks <= 0) {
                MarkPostFirstMetComplete();
            }
        }

        /// <summary>
        /// 鬼切试炼委托是否允许发放。多层门禁，任一兜底成功即视为可发
        /// </summary>
        public static bool CanStartOnikiriTrialQuests(Player player) {
            if (player == null || !player.active || !player.HasItem(OnikiriOverride.ID)) {
                return false;
            }
            if (NarrativeTriggerGate.IsBusy) {
                return false;
            }

            if (Story.PostFirstMetIsComplete) {
                return true;
            }

            //软兜底：初遇已触发，且当前没有初遇在播 → OnCompleted 丢失时仍可开线
            //（含旧档：FirstMet=true 但尚无 PostFirstMet 标记）
            if (Story.FirstMet && !NarrativeRouter.IsActive<FirstMetHimayo>()) {
                MarkPostFirstMetComplete();
                return true;
            }

            //硬兜底依赖 Tick 到期写 PostFirstMet；此处确保旧档已拔刀则武装倒计时
            if (Story.ToriiSwordTaken) {
                ArmTrialUnlockSafety();
            }

            return false;
        }
    }
}
