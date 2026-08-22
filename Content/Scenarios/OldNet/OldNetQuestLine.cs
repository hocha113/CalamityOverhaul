using CalamityOverhaul.Content.EntrustManager;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.TrialQuests;
using CalamityOverhaul.Content.Narrative.Data;
using CalamityOverhaul.Content.Narrative.Data.Modules;
using System.Collections.Generic;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.OldNet
{
    /// <summary>
    /// 首潜委托："越墙深潜"，把旧网入口暴露进任务书
    /// 完成判据 = 一次安全登出（OldNetPlayer.SettleAndLogout 写 DiveCompleted）
    /// 逐帧同步注册的既有惯例（DraedonQuestLine 同款泵）
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
            EnsureEntry(manager, data);
        }

        private static void EnsureEntry(QuestManagerUI manager, OldNetGuideData data) {
            EntrustEntryData existing = manager.GetEntry(QuestKey);
            if (existing is OldNetQuestEntry) {
                if (data.DiveCompleted && existing.Status != QuestEntryStatus.Completed) {
                    existing.Status = QuestEntryStatus.Completed;
                    existing.Progress = 1f;
                    manager.MarkFilterDirty();
                }
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
                //旧网入口在 SHPC 坠舱，关注侧栏直接套 SHPC 简约文字提示
                TrackerStyle = new SHPCTrackerWidgetStyle(),
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
