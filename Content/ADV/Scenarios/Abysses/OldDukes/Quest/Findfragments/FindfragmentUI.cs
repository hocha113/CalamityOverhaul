using CalamityOverhaul.Content.ADV.ADVQuestTracker;
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
    internal class FindFragmentUI : BaseQuestTrackerUI
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

        protected override QuestTrackerStyle GetQuestStyle() => QuestTrackerStyle.Sulfsea;

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

        public override bool CanOpne => !CWRWorld.HasBoss && FindfragmentFish.CanAttemptQuestFishing(player);

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

        protected override float CalculateContentHeight() {
            const float titleScale = 0.72f;
            const float textScale = 0.62f;

            float topPadding = 8f;
            float titleHeight = CalculateTextHeight(QuestTitle.Value, titleScale);
            float titleBottomMargin = 4f;
            float dividerHeight = 2f;
            float dividerBottomMargin = 12f;

            //根据任务完成状态计算不同的内容高度
            bool questCompleted = Main.LocalPlayer.TryGetADVSave(out var save)
                && save.OldDukeFindFragmentsQuestCompleted;

            float contentBlockHeight;

            if (questCompleted) {
                //完成状态
                float completeTextHeight = CalculateTextHeight(QuestCompleteText.Value, textScale * 1.2f);
                float progressBarHeight = 6f;

                contentBlockHeight = completeTextHeight + 5f + progressBarHeight;
            }
            else {
                //进行中状态
                string objectiveText = $"{ObjectiveText.Value}: {CollectFragmentsText.Value}";
                float objHeight = CalculateTextHeight(objectiveText, textScale);

                string countText = $"{CurrentFragmentsText.Value}: 777/777"; //使用最大值占位
                float countHeight = CalculateTextHeight(countText, textScale);

                //提示文本（两种状态取较大的）
                float hintHeight1 = CalculateTextHeight($"> {ReturnToCampsiteText.Value} <", textScale * 1.1f);
                float hintHeight2 = CalculateTextHeight(HintText.Value, textScale);
                float hintHeight = Math.Max(hintHeight1, hintHeight2);

                contentBlockHeight = objHeight
                    + countHeight
                    + hintHeight;
            }

            return topPadding
                + titleHeight + titleBottomMargin
                + dividerHeight + dividerBottomMargin
                + contentBlockHeight;
        }

        protected override void UpdatePanelHeight() {
            base.UpdatePanelHeight();
            currentPanelHeight += 50f;
        }

        /// <summary>
        /// 重写标题行效果，添加发光效果
        /// </summary>
        protected override void DrawTitleLineEffect(SpriteBatch spriteBatch, string text, Vector2 position, Color color, float scale, float alpha, int lineIndex) {
            //标题发光效果
            Color titleGlow = new Color(140, 180, 70) * (alpha * 0.6f);
            for (int i = 0; i < 4; i++) {
                float a = MathHelper.TwoPi * i / 4f;
                Vector2 off = a.ToRotationVector2() * 1.5f;
                Utils.DrawBorderString(spriteBatch, text, position + off, titleGlow * 0.5f, scale);
            }

            //绘制主文本
            Utils.DrawBorderString(spriteBatch, text, position, color, scale);
        }

        protected override void DrawContent(SpriteBatch spriteBatch, float alpha) {
            if (!Main.LocalPlayer.TryGetADVSave(out var save)) {
                return;
            }

            bool questCompleted = save.OldDukeFindFragmentsQuestCompleted;

            const float titleScale = 0.72f;
            const float textScale = 0.62f;

            //使用基类的标题绘制接口，自动支持换行和特殊效果
            Vector2 titlePos = DrawPosition + new Vector2(10, 8);
            float titleHeight = DrawTitle(spriteBatch, titlePos, alpha, titleScale);

            //绘制分隔线
            Vector2 dividerStart = titlePos + new Vector2(0, titleHeight + 4);
            Vector2 dividerEnd = dividerStart + new Vector2(PanelWidth - 20, 0);
            currentStyle?.DrawDivider(spriteBatch, dividerStart, dividerEnd, alpha);

            //绘制任务内容
            Vector2 contentPos = dividerStart + new Vector2(0, 12);
            if (questCompleted) {
                DrawQuestCompleted(spriteBatch, contentPos, alpha, textScale);
            }
            else {
                DrawQuestInProgress(spriteBatch, contentPos, alpha, textScale);
            }
        }

        private void DrawQuestInProgress(SpriteBatch spriteBatch, Vector2 startPos, float alpha, float textScale) {
            int fragmentCount = GetFragmentCount();
            Color textColor = currentStyle?.GetTextColor(alpha) ?? Color.White * alpha;

            //目标文本（使用换行接口）
            string objectiveText = $"{ObjectiveText.Value}: {CollectFragmentsText.Value}";
            float objHeight = DrawObjectiveText(spriteBatch, objectiveText, startPos, alpha, textScale);

            //当前数量
            Vector2 countPos = startPos + new Vector2(0, objHeight + 3);
            string countText = $"{CurrentFragmentsText.Value}: {fragmentCount}/777";

            Color countColor = fragmentCount >= 777
                ? Color.LimeGreen * (alpha * ((float)Math.Sin(pulseTimer * 3f) * 0.3f + 0.7f))
                : textColor;

            float countHeight = DrawWrappedText(spriteBatch, countText, countPos, countColor, textScale, alpha);

            //返回提示或收集提示
            Vector2 hintPos = countPos + new Vector2(0, countHeight + 3);
            if (fragmentCount >= 777) {
                float blink = (float)Math.Sin(pulseTimer * 4f) * 0.5f + 0.5f;
                Color returnColor = new Color(160, 220, 100) * (alpha * blink);
                DrawWrappedText(spriteBatch, $"> {ReturnToCampsiteText.Value} <", hintPos, returnColor, textScale * 1.1f, alpha);
            }
            else {
                DrawHintText(spriteBatch, HintText.Value, hintPos, new Color(140, 170, 75), alpha, textScale);
            }

            //进度条
            Vector2 progressBarPos = startPos + new Vector2(0, 65);
            DrawProgressBar(spriteBatch, progressBarPos, alpha);
        }

        private void DrawQuestCompleted(SpriteBatch spriteBatch, Vector2 startPos, float alpha, float textScale) {
            float pulse = (float)Math.Sin(pulseTimer * 2f) * 0.3f + 0.7f;
            Color completeColor = Color.Lerp(new Color(180, 220, 100), Color.LimeGreen, pulse) * alpha;

            float completeHeight = DrawWrappedText(spriteBatch, QuestCompleteText.Value, startPos, completeColor, textScale * 1.2f, alpha);

            Vector2 progressBarPos = startPos + new Vector2(0, completeHeight + 5);
            DrawProgressBar(spriteBatch, progressBarPos, alpha);
        }
    }
}
