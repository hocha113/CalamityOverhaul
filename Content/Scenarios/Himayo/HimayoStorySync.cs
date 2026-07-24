using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend;
using CalamityOverhaul.Content.Narrative;
using CalamityOverhaul.Content.Narrative.Data;
using CalamityOverhaul.Content.Narrative.Data.Modules;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Scenarios.Himayo
{
    /// <summary>
    /// 真夜进度同步与试炼发放门禁<br/>
    /// 正常 FirstMetHimayo.OnCompleted → PostFirstMetIsComplete<br/>
    /// 兜底 初遇已触发且叙事空闲视为播完、拔刀后硬倒计时到期强制解锁
    /// </summary>
    internal static class HimayoStorySync
    {
        /// <summary>拔刀后初遇未落幕约90s强制开试炼，叙事忙时不计</summary>
        public const int TrialUnlockSafetyDuration = 60 * 90;

        public static HimayoStoryData Story
            => Main.LocalPlayer.GetModPlayer<StoryPlayer>().Get<HimayoStoryData>();

        public static bool FirstMet => Story.FirstMet;

        public static void MarkFirstMet() => Story.FirstMet = true;

        /// <summary>初遇播完，试炼发放门禁</summary>
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

        /// <summary>武装硬倒计时，已完成或已在倒数则跳过</summary>
        public static void ArmTrialUnlockSafety() {
            if (Story.PostFirstMetIsComplete || Story.TrialUnlockSafetyTicks > 0) {
                return;
            }
            Story.TrialUnlockSafetyTicks = TrialUnlockSafetyDuration;
        }

        /// <summary>本地玩家推进倒计时，叙事忙或初遇在播时暂停</summary>
        public static void TickTrialUnlockSafety(Player player) {
            if (player == null || !player.active || player.whoAmI != Main.myPlayer || Main.dedServ) {
                return;
            }
            if (Story.PostFirstMetIsComplete) {
                Story.TrialUnlockSafetyTicks = 0;
                return;
            }
            //仅鸟居拔刀后硬倒计时，刷刀不开试炼
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

        /// <summary>试炼委托可发门禁，任一兜底成功即可</summary>
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

            //软兜底，初遇已触发且未在播(含旧档缺PostFirstMet)
            if (Story.FirstMet && !NarrativeRouter.IsActive<FirstMetHimayo>()) {
                MarkPostFirstMetComplete();
                return true;
            }

            //硬兜底靠Tick，旧档已拔刀则武装倒计时
            if (Story.ToriiSwordTaken) {
                ArmTrialUnlockSafety();
            }

            return false;
        }

        public static HimayoGiftStoryData GiftStory
            => Main.LocalPlayer.GetModPlayer<StoryPlayer>().Get<HimayoGiftStoryData>();

        public static bool ReadGift(Func<HimayoGiftStoryData, bool> story, Func<HimayoGiftStoryData, bool> legacy) {
            if (story(GiftStory)) {
                return true;
            }

            return legacy(GiftStory);
        }

        public static void WriteGift(Action<HimayoGiftStoryData> story, Action<HimayoGiftStoryData> legacy) {
            story(GiftStory);
            legacy(GiftStory);
        }
    }
}
