using CalamityOverhaul.Content.EntrustManager;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.TrialQuests;
using CalamityOverhaul.Content.Narrative;
using CalamityOverhaul.Content.Narrative.Data;
using CalamityOverhaul.Content.Narrative.Data.Modules;
using CalamityOverhaul.Content.Structures;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.OldNet
{
    /// <summary>
    /// 首潜委托："越墙深潜"，把旧网入口暴露进任务书
    /// 完成判据 = 一次安全登出（OldNetPlayer.SettleAndLogout 写 DiveCompleted）
    /// 逐帧同步注册的既有惯例（DraedonQuestLine 同款泵）
    /// 派发与追踪窗显示都跟 SHPC 试炼线同口径：手持 SHPC 才发单、才显示
    /// </summary>
    internal class OldNetQuestLine : ModSystem
    {
        internal const string QuestKey = "OldNet_FirstDive";

        public override void PostUpdateEverything() {
            if (Main.dedServ || Main.gameMenu) {
                return;
            }
            QuestManagerUI manager = QuestManagerUI.Instance;
            Player player = Main.LocalPlayer;
            if (manager == null || player?.active != true) {
                return;
            }

            OldNetGuideData data = player.GetModPlayer<StoryPlayer>().Get<OldNetGuideData>();
            EnsureEntry(manager, player, data);
        }

        //入口是否算"存在"：深潜仅单人开放，且玩家没把坠舱密度设为灭绝（否则终端根本没落地）
        private static bool EntranceEligible
            => Main.netMode == NetmodeID.SinglePlayer && SHPCCradleGen.Enabled;

        //派发门禁照抄 SHPC 试炼线：手持 SHPC 才发单，演出进行中暂缓
        private static bool CanDispatch(Player player)
            => !NarrativeTriggerGate.IsBusy && player.GetItem().type == SHPCOverride.ID;

        private static void EnsureEntry(QuestManagerUI manager, Player player, OldNetGuideData data) {
            EntrustEntryData existing = manager.GetEntry(QuestKey);
            if (existing is OldNetQuestEntry) {
                if (data.DiveCompleted && existing.Status != QuestEntryStatus.Completed) {
                    existing.Status = QuestEntryStatus.Completed;
                    existing.Progress = 1f;
                    manager.MarkFilterDirty();
                }
                return;
            }

            //没握着 SHPC 就不落委托：这单本来就是 SHPC 的活，跟其余试炼一样只对持枪者开
            if (!CanDispatch(player)) {
                return;
            }

            //还没首发过、且入口本身不成立（多人/坠舱被禁）时不主动派发，避免每次进世界都白弹一条
            if (!data.EntrustIntroduced && !data.DiveCompleted && !EntranceEligible) {
                return;
            }

            //裸 EntrustEntryData 无追踪样式，会掉进默认暗色方框；换掉并保留关注/挂起/完成态
            QuestEntryStatus? keepStatus = existing?.Status;
            float keepProgress = existing?.Progress ?? 0f;
            bool keepIsNew = existing?.IsNew ?? false;
            if (existing != null) {
                manager.UnregisterQuest(QuestKey);
            }

            OldNetQuestEntry entry = new(QuestKey,
                OldNetTexts.EntrustTitle, OldNetTexts.EntrustSummary, OldNetTexts.EntrustCategory) {
                Priority = 30,
                //旧网入口在 SHPC 坠舱，委托人与关注侧栏都归 SHPC
                Provider = EntrustProviders.SHPC,
                TrackerStyle = new SHPCTrackerWidgetStyle(),
                //追踪窗仅手持 SHPC 时显示
                TrackerVisibilityCheck = static () => Main.LocalPlayer.GetItem().type == SHPCOverride.ID,
            };

            if (data.DiveCompleted) {
                entry.Status = QuestEntryStatus.Completed;
                entry.Progress = 1f;
                entry.IsNew = false;
            }
            else if (keepStatus.HasValue) {
                entry.Status = keepStatus.Value;
                entry.Progress = keepProgress;
                entry.IsNew = keepIsNew;
            }
            else if (data.EntrustIntroduced) {
                //本会话内是首次注册，但跨会话早已首发过：直接落关注态，不重触发"新任务"提示
                entry.Status = QuestEntryStatus.Tracked;
            }

            data.EntrustIntroduced = true;
            manager.RegisterQuest(entry);
        }
    }

    /// <summary>追踪窗只给下一动作的短提示，不把委托摘要整段塞进去</summary>
    internal sealed class OldNetQuestEntry : EntrustEntryData
    {
        public OldNetQuestEntry(string key, LocalizedText title, LocalizedText summary, LocalizedText category)
            : base(key, title, summary, category) { }

        public override float GetTrackerContentTopPadding() => 5f;

        public override List<string> GetTrackerDetails() {
            if (OldNetWorld.Active) {
                return [OldNetTexts.TrackerDive.Value];
            }
            return [OldNetTexts.TrackerOverworld.Value];
        }
    }
}
