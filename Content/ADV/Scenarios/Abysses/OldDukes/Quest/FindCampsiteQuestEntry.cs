using CalamityOverhaul.Content.ADV.EntrustManager;
using CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Campsites;
using System.Collections.Generic;
using Terraria;
using Terraria.Localization;

namespace CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Quest
{
    /// <summary>
    /// 寻找老公爵营地委托条目——<see cref="EntrustEntryData"/> 子类，
    /// 追踪玩家前往营地与老公爵对话的进度，
    /// 在追踪窗口中显示距离和交互提示
    /// </summary>
    internal class FindCampsiteQuestEntry : EntrustEntryData
    {
        /// <summary>"目标" 文本</summary>
        public LocalizedText ObjectiveFormat { get; init; }
        /// <summary>"前往老公爵营地" 文本</summary>
        public LocalizedText LocationFormat { get; init; }
        /// <summary>"距离" 文本</summary>
        public LocalizedText DistanceFormat { get; init; }
        /// <summary>"与老公爵对话" 文本</summary>
        public LocalizedText InteractFormat { get; init; }
        /// <summary>"任务完成！" 文本</summary>
        public LocalizedText QuestCompleteFormat { get; init; }
        /// <summary>"持有海洋碎片可查看方向" 文本</summary>
        public LocalizedText HoldFragmentHintFormat { get; init; }

        private float distanceToCampsite;
        private bool canInteract;

        public FindCampsiteQuestEntry(string key, LocalizedText title, LocalizedText summary, LocalizedText category)
            : base(key, title, summary, category) { }

        //追踪窗口使用极简HUD样式时，需要在内容区顶部预留5px，
        //让标题下划线与首行描述之间留出可见的呼吸空间
        public override float GetTrackerContentTopPadding() => 5f;

        public override void OnUpdate() {
            if (Status == QuestEntryStatus.Completed || Status == QuestEntryStatus.Failed) return;

            if (!Main.LocalPlayer.TryGetADVSave(out var save)) return;

            if (save.Get<OldDukeADVData>().OldDukeFirstCampsiteDialogueCompleted) {
                Progress = 1f;
                return;
            }

            if (OldDukeCampsite.IsGenerated) {
                distanceToCampsite = Vector2.Distance(Main.LocalPlayer.Center, OldDukeCampsite.CampsitePosition) / 16f;
                canInteract = OldDukeCampsite.CanInteract();
                //用距离反推进度：离3000m到0m映射为0~1
                Progress = MathHelper.Clamp(1f - distanceToCampsite / 3000f, 0f, 0.99f);
            }
        }

        public override List<string> GetTrackerDetails() {
            if (!Main.LocalPlayer.TryGetADVSave(out var save)) return [Summary];

            if (save.Get<OldDukeADVData>().OldDukeFirstCampsiteDialogueCompleted) {
                return [QuestCompleteFormat?.Value ?? "Quest Complete!"];
            }

            List<string> lines = [];

            lines.Add($"{ObjectiveFormat?.Value ?? ""}: {LocationFormat?.Value ?? ""}");

            if (OldDukeCampsite.IsGenerated) {
                if (canInteract) {
                    lines.Add($"> {InteractFormat?.Value ?? ""} <");
                }
                else {
                    lines.Add($"{DistanceFormat?.Value ?? ""}: {(int)distanceToCampsite}m");
                }
            }

            lines.Add(HoldFragmentHintFormat?.Value ?? "");

            return lines;
        }
    }
}
