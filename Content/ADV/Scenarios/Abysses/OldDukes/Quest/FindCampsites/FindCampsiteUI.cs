using CalamityOverhaul.Content.ADV.ADVQuestTracker;
using CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Campsites;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Quest.FindCampsites
{
    /// <summary>
    /// 寻找老公爵营地任务追踪UI
    /// </summary>
    internal class FindCampsiteUI : BaseQuestTrackerUI
    {
        public static FindCampsiteUI Instance => UIHandleLoader.GetUIHandleOfType<FindCampsiteUI>();

        public override string LocalizationCategory => "UI";

        public override int TargetNPCType => -1;

        //本地化文本
        public static LocalizedText ObjectiveText { get; private set; }
        public static LocalizedText LocationText { get; private set; }
        public static LocalizedText DistanceText { get; private set; }
        public static LocalizedText InteractText { get; private set; }
        public static LocalizedText QuestCompleteText { get; private set; }
        public static LocalizedText HoldFragmentHintText { get; private set; }

        protected override QuestTrackerStyle GetQuestStyle() => QuestTrackerStyle.Sulfsea;

        public override void SetStaticDefaults() {
            base.SetStaticDefaults();

            ObjectiveText = this.GetLocalization(nameof(ObjectiveText), () => "目标");
            LocationText = this.GetLocalization(nameof(LocationText), () => "前往老公爵营地");
            DistanceText = this.GetLocalization(nameof(DistanceText), () => "距离");
            InteractText = this.GetLocalization(nameof(InteractText), () => "与老公爵对话");
            QuestCompleteText = this.GetLocalization(nameof(QuestCompleteText), () => "任务完成！");
            HoldFragmentHintText = this.GetLocalization(nameof(HoldFragmentHintText), () => "持有海洋碎片可查看方向");
        }

        protected override void SetupLocalizedTexts() {
            QuestTitle = this.GetLocalization(nameof(QuestTitle), () => "深渊在呼唤");
            DamageContribution = this.GetLocalization(nameof(DamageContribution), () => "任务进度");
            RequiredContribution = this.GetLocalization(nameof(RequiredContribution), () => "目标: 找到并与老公爵对话");
        }

        public override bool CanOpne {
            get {
                if (Main.LocalPlayer == null || !Main.LocalPlayer.active) {
                    return false;
                }

                if (!Main.LocalPlayer.TryGetADVSave(out var save)) {
                    return false;
                }

                if (!save.OldDukeCooperationAccepted) {
                    return false;
                }

                if (save.OldDukeFirstCampsiteDialogueCompleted) {
                    return false;
                }

                return OldDukeCampsite.IsGenerated;
            }
        }

        protected override (float current, float total, bool isActive) GetTrackingData() {
            if (!Main.LocalPlayer.TryGetADVSave(out var save)) {
                return (0, 1, false);
            }

            float current = save.OldDukeFirstCampsiteDialogueCompleted ? 1f : 0f;
            float total = 1f;
            bool isActive = save.OldDukeCooperationAccepted && !save.OldDukeFirstCampsiteDialogueCompleted;

            return (current, total, isActive);
        }

        protected override float GetRequiredContribution() {
            return 1.0f;
        }

        protected override void UpdatePanelHeight() {
            base.UpdatePanelHeight();
            currentPanelHeight += 50f;
        }

        /// <summary>
        /// 重写标题行绘制，添加发光效果
        /// </summary>
        protected override void DrawTitleLine(SpriteBatch spriteBatch, string text, Vector2 position, Color color, float scale, float alpha) {
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

            var font = FontAssets.MouseText.Value;
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
            bool questCompleted = save.OldDukeFirstCampsiteDialogueCompleted;

            if (questCompleted) {
                DrawQuestCompleted(spriteBatch, contentPos, alpha, textScale);
            }
            else {
                DrawQuestInProgress(spriteBatch, contentPos, alpha, textScale);
            }
        }

        private void DrawQuestInProgress(SpriteBatch spriteBatch, Vector2 startPos, float alpha, float textScale) {
            Color textColor = currentStyle?.GetTextColor(alpha) ?? Color.White * alpha;

            //目标文本
            string objectiveText = $"{ObjectiveText.Value}: {LocationText.Value}";
            Utils.DrawBorderString(spriteBatch, objectiveText, startPos, textColor, textScale);

            //距离信息
            if (OldDukeCampsite.IsGenerated) {
                Vector2 distancePos = startPos + new Vector2(0, 18);
                float distance = Vector2.Distance(Main.LocalPlayer.Center, OldDukeCampsite.CampsitePosition) / 16f;
                string distanceText = $"{DistanceText.Value}: {(int)distance}m";

                Color distanceColor = distance < 50f
                    ? Color.LimeGreen * (alpha * ((float)Math.Sin(pulseTimer * 3f) * 0.3f + 0.7f))
                    : textColor;

                Utils.DrawBorderString(spriteBatch, distanceText, distancePos, distanceColor, textScale);

                //交互提示
                Vector2 hintPos = distancePos + new Vector2(0, 18);
                if (OldDukeCampsite.CanInteract()) {
                    float blink = (float)Math.Sin(pulseTimer * 4f) * 0.5f + 0.5f;
                    Color interactColor = new Color(160, 220, 100) * (alpha * blink);
                    Utils.DrawBorderString(spriteBatch, $"> {InteractText.Value} <", hintPos, interactColor, textScale * 1.1f);
                }
                else {
                    Color hintColor = new Color(140, 170, 75) * (alpha * 0.7f);
                    Utils.DrawBorderString(spriteBatch, HoldFragmentHintText.Value, hintPos, hintColor, textScale);
                }
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

        protected override void DrawProgressBar(SpriteBatch spriteBatch, Vector2 position, float alpha) {
            float barWidth = PanelWidth - 20;
            float barHeight = 8;
            Rectangle barRect = new Rectangle((int)position.X, (int)position.Y, (int)barWidth, (int)barHeight);

            //使用距离作为进度
            float total = Vector2.Distance(Main.LocalPlayer.Center, OldDukeCampsite.CampsitePosition) / 16f;
            float current = 3000f;
            float progress = 1f - total / current;

            currentStyle?.DrawProgressBar(spriteBatch, barRect, progress, alpha);
        }
    }
}
