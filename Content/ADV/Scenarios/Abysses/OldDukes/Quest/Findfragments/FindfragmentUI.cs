using CalamityOverhaul.Content.ADV.Common;
using CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Items;
using CalamityOverhaul.OtherMods.ImproveGame.Ammos;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Quest.Findfragments
{
    /// <summary>
    /// 寻找海洋碎片任务追踪UI
    /// </summary>
    internal class FindFragmentUI : BaseSulfurQuestTrackerUI
    {
        public static FindFragmentUI Instance => UIHandleLoader.GetUIHandleOfType<FindFragmentUI>();

        public override string LocalizationCategory => "UI";

        public override int TargetNPCType => -1;

        //本地化文本
        public static LocalizedText ObjectiveText { get; private set; }
        public static LocalizedText CollectFragmentsText { get; private set; }
        public static LocalizedText CurrentFragmentsText { get; private set; }
        public static LocalizedText ReturnToCampsiteText { get; private set; }
        public static LocalizedText QuestCompleteText { get; private set; }
        public static LocalizedText HintText { get; private set; }

        public override void SetStaticDefaults() {
            base.SetStaticDefaults();

            ObjectiveText = this.GetLocalization(nameof(ObjectiveText), () => "目标");
            CollectFragmentsText = this.GetLocalization(nameof(CollectFragmentsText), () => "收集海洋残片");
            CurrentFragmentsText = this.GetLocalization(nameof(CurrentFragmentsText), () => "当前拥有");
            ReturnToCampsiteText = this.GetLocalization(nameof(ReturnToCampsiteText), () => "返回营地提交");
            QuestCompleteText = this.GetLocalization(nameof(QuestCompleteText), () => "任务完成！");
            HintText = this.GetLocalization(nameof(HintText), () => "钓鱼或者搜刮海洋区域的生物");
        }

        protected override void SetupLocalizedTexts() {
            QuestTitle = this.GetLocalization(nameof(QuestTitle), () => "深渊在呼唤");
            DamageContribution = this.GetLocalization(nameof(DamageContribution), () => "收集进度");
            RequiredContribution = this.GetLocalization(nameof(RequiredContribution), () => "目标: 收集777块海洋残片");
        }

        public override bool CanOpne => FindfragmentFish.CanAttemptQuestFishing(player);

        protected override (float current, float total, bool isActive) GetTrackingData() {
            if (!Main.LocalPlayer.TryGetADVSave(out var save)) {
                return (0, 777, false);
            }

            float current = GetFragmentCount();
            float total = 777;
            bool isActive = save.OldDukeFindFragmentsQuestTriggered && !save.OldDukeFindFragmentsQuestCompleted;

            return (current, total, isActive);
        }

        /// <summary>
        /// 获取玩家背包中的海洋残片数量
        /// </summary>
        public static int GetFragmentCount() {
            int count = 0;
            Player player = Main.LocalPlayer;
            int fragmentType = ModContent.ItemType<Oceanfragments>();

            var bigBags = player.GetBigBagItems() ?? [];
            //依次从各个储物位置消耗
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

        protected override float GetRequiredContribution() {
            return 777;
        }

        protected override void UpdatePanelHeight() {
            base.UpdatePanelHeight();
            currentPanelHeight += 50f;
        }

        protected override void DrawQuestContent(SpriteBatch spriteBatch, Vector2 startPos, float alpha, float textScale) {
            if (!Main.LocalPlayer.TryGetADVSave(out var save)) {
                return;
            }

            bool questCompleted = save.OldDukeFindFragmentsQuestCompleted;

            if (questCompleted) {
                DrawQuestCompleted(spriteBatch, startPos, alpha, textScale);
            }
            else {
                DrawQuestInProgress(spriteBatch, startPos, alpha, textScale);
            }
        }

        private void DrawQuestInProgress(SpriteBatch spriteBatch, Vector2 startPos, float alpha, float textScale) {
            int fragmentCount = GetFragmentCount();

            //目标文本
            string objectiveText = $"{ObjectiveText.Value}: {CollectFragmentsText.Value}";
            Color objectiveColor = new Color(200, 220, 150) * alpha;
            Utils.DrawBorderString(spriteBatch, objectiveText, startPos, objectiveColor, textScale);

            //当前数量
            Vector2 countPos = startPos + new Vector2(0, 18);
            string countText = $"{CurrentFragmentsText.Value}: {fragmentCount}/777";

            Color countColor;
            if (fragmentCount >= 777) {
                float pulse = (float)Math.Sin(pulseTimer * 3f) * 0.3f + 0.7f;
                countColor = Color.LimeGreen * (alpha * pulse);
            }
            else {
                countColor = new Color(180, 200, 130) * alpha;
            }

            Utils.DrawBorderString(spriteBatch, countText, countPos, countColor, textScale);

            //返回提示
            if (fragmentCount >= 777) {
                Vector2 returnPos = countPos + new Vector2(0, 18);
                float blink = (float)Math.Sin(pulseTimer * 4f) * 0.5f + 0.5f;
                Color returnColor = new Color(160, 220, 100) * (alpha * blink);
                Utils.DrawBorderString(spriteBatch, $"> {ReturnToCampsiteText.Value} <", returnPos, returnColor, textScale * 1.1f);
            }
            else {
                Vector2 hintPos = countPos + new Vector2(0, 18);
                Color hintColor = new Color(140, 170, 75) * (alpha * 0.7f);
                Utils.DrawBorderString(spriteBatch, HintText.Value, hintPos, hintColor, textScale);
            }

            //进度条
            Vector2 progressBarPos = startPos + new Vector2(0, 65);
            DrawProgressBar(spriteBatch, progressBarPos, alpha);
        }

        private void DrawQuestCompleted(SpriteBatch spriteBatch, Vector2 startPos, float alpha, float textScale) {
            float pulse = (float)Math.Sin(pulseTimer * 2f) * 0.3f + 0.7f;
            Color completeColor = Color.Lerp(new Color(180, 220, 100), Color.LimeGreen, pulse) * alpha;

            Utils.DrawBorderString(spriteBatch, QuestCompleteText.Value, startPos, completeColor, textScale * 1.2f);

            Vector2 progressBarPos = startPos + new Vector2(0, 35);
            DrawProgressBar(spriteBatch, progressBarPos, alpha);
        }
    }
}
