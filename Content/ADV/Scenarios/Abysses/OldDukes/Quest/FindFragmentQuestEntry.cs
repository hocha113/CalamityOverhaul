using CalamityOverhaul.Content.ADV.EntrustManager;
using CalamityOverhaul.OtherMods.ImproveGame;
using System.Collections.Generic;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Quest
{
    /// <summary>
    /// 收集海洋碎片委托条目——<see cref="EntrustEntryData"/> 子类，
    /// 追踪玩家收集海洋残片的进度，
    /// 在追踪窗口中显示当前数量与收集提示
    /// </summary>
    internal class FindFragmentQuestEntry : EntrustEntryData
    {
        private const int TargetCount = 777;

        /// <summary>"目标" 文本</summary>
        public LocalizedText ObjectiveFormat { get; init; }
        /// <summary>"收集海洋残片" 文本</summary>
        public LocalizedText CollectFormat { get; init; }
        /// <summary>"当前拥有" 文本</summary>
        public LocalizedText CurrentFormat { get; init; }
        /// <summary>"返回营地提交" 文本</summary>
        public LocalizedText ReturnFormat { get; init; }
        /// <summary>"任务完成！" 文本</summary>
        public LocalizedText QuestCompleteFormat { get; init; }
        /// <summary>"钓鱼或者搜刮海洋区域的生物" 文本</summary>
        public LocalizedText HintFormat { get; init; }

        private int fragmentCount;

        public FindFragmentQuestEntry(string key, LocalizedText title, LocalizedText summary, LocalizedText category)
            : base(key, title, summary, category) { }

        //追踪窗口使用极简HUD样式时，需要在内容区顶部预留5px，
        //让标题下划线与首行描述之间留出可见的呼吸空间
        public override float GetTrackerContentTopPadding() => 5f;

        public override void OnUpdate() {
            if (Status == QuestEntryStatus.Completed || Status == QuestEntryStatus.Failed) return;

            fragmentCount = GetFragmentCount();
            Progress = MathHelper.Clamp(fragmentCount / (float)TargetCount, 0f, 1f);
        }

        public override List<string> GetTrackerDetails() {
            if (!Main.LocalPlayer.TryGetADVSave(out var save)) return [Summary];

            if (save.Get<OldDukeADVData>().OldDukeFindFragmentsQuestCompleted) {
                return [QuestCompleteFormat?.Value ?? "Quest Complete!"];
            }

            List<string> lines = [];

            lines.Add($"{ObjectiveFormat?.Value ?? ""}: {CollectFormat?.Value ?? ""}");
            lines.Add($"{CurrentFormat?.Value ?? ""}: {fragmentCount}/{TargetCount}");

            if (fragmentCount >= TargetCount) {
                lines.Add($"> {ReturnFormat?.Value ?? ""} <");
            }
            else {
                lines.Add(HintFormat?.Value ?? "");
            }

            return lines;
        }

        /// <summary>
        /// 获取玩家背包中的海洋残片数量（统计所有储物位置）
        /// </summary>
        public static int GetFragmentCount() {
            int count = 0;
            Player player = Main.LocalPlayer;
            int fragmentType = ModContent.ItemType<Oceanfragments>();

            var bigBags = player.GetBigBagItems() ?? [];
            Item[][] inventories = [
                player.inventory,
                player.bank.item,
                player.bank2.item,
                player.bank3.item,
                player.bank4.item,
                [.. bigBags],
            ];

            foreach (var inventorie in inventories) {
                for (int i = 0; i < inventorie.Length; i++) {
                    if (inventorie[i].type == fragmentType) {
                        count += inventorie[i].stack;
                    }
                }
            }

            return count;
        }
    }
}
