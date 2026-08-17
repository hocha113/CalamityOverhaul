using CalamityOverhaul.Content.EntrustManager;
using CalamityOverhaul.Content.Narrative.Data;
using CalamityOverhaul.Content.Narrative.Data.Modules;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.OldNet
{
    /// <summary>
    /// 首潜委托："越墙深潜"——把旧网入口暴露进任务书。
    /// 完成判据 = 一次安全登出（OldNetPlayer.SettleAndLogout 写 DiveCompleted）。
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
            EntrustEntryData entry = manager.GetEntry(QuestKey);
            if (entry == null) {
                entry = new EntrustEntryData(QuestKey,
                    OldNetTexts.EntrustTitle, OldNetTexts.EntrustSummary, OldNetTexts.EntrustCategory) {
                    Priority = 30,
                };
                //已完成的老档：静默补注册为完成态，不再弹新委托通知
                if (data.DiveCompleted) {
                    entry.Status = QuestEntryStatus.Completed;
                    entry.Progress = 1f;
                    entry.IsNew = false;
                }
                manager.RegisterQuest(entry);
                return;
            }

            if (data.DiveCompleted && entry.Status != QuestEntryStatus.Completed) {
                entry.Status = QuestEntryStatus.Completed;
                entry.Progress = 1f;
                manager.MarkFilterDirty();
            }
        }
    }
}
