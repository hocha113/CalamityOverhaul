using CalamityOverhaul.Content.ADV.Common;
using CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Campsites;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Quest.FindCampsites
{
    /// <summary>
    /// 寻找老公爵营地任务追踪UI
    /// </summary>
    internal class FindCampsiteUI : BaseSulfurQuestTrackerUI
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

                //只有在接受合作后且未完成对话前显示
                if (!save.OldDukeCooperationAccepted) {
                    return false;
                }

                //如果已经完成首次对话，不再显示
                if (save.OldDukeFirstCampsiteDialogueCompleted) {
                    return false;
                }

                //营地必须已生成
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

        protected override void DrawQuestContent(SpriteBatch spriteBatch, Vector2 startPos, float alpha, float textScale) {
            if (!Main.LocalPlayer.TryGetADVSave(out var save)) {
                return;
            }

            bool questCompleted = save.OldDukeFirstCampsiteDialogueCompleted;

            if (questCompleted) {
                DrawQuestCompleted(spriteBatch, startPos, alpha, textScale);
            }
            else {
                DrawQuestInProgress(spriteBatch, startPos, alpha, textScale);
            }
        }

        /// <summary>
        /// 绘制任务进行中的内容
        /// </summary>
        private void DrawQuestInProgress(SpriteBatch spriteBatch, Vector2 startPos, float alpha, float textScale) {
            //目标文本
            string objectiveText = $"{ObjectiveText.Value}: {LocationText.Value}";
            Color objectiveColor = new Color(200, 220, 150) * alpha;
            Utils.DrawBorderString(spriteBatch, objectiveText, startPos, objectiveColor, textScale);

            //距离信息
            if (OldDukeCampsite.IsGenerated) {
                Vector2 distancePos = startPos + new Vector2(0, 18);
                float distance = Vector2.Distance(Main.LocalPlayer.Center, OldDukeCampsite.CampsitePosition) / 16f;
                string distanceText = $"{DistanceText.Value}: {(int)distance}m";

                Color distanceColor;
                if (distance < 50f) {
                    //接近时显示绿色
                    float pulse = (float)Math.Sin(pulseTimer * 3f) * 0.3f + 0.7f;
                    distanceColor = Color.LimeGreen * (alpha * pulse);
                }
                else {
                    distanceColor = new Color(180, 200, 130) * alpha;
                }

                Utils.DrawBorderString(spriteBatch, distanceText, distancePos, distanceColor, textScale);

                //交互提示
                if (OldDukeCampsite.CanInteract()) {
                    Vector2 interactPos = distancePos + new Vector2(0, 18);
                    float blink = (float)Math.Sin(pulseTimer * 4f) * 0.5f + 0.5f;
                    Color interactColor = new Color(160, 220, 100) * (alpha * blink);
                    Utils.DrawBorderString(spriteBatch, $"> {InteractText.Value} <", interactPos, interactColor, textScale * 1.1f);
                }
                else {
                    //提示持有碎片
                    Vector2 hintPos = distancePos + new Vector2(0, 18);
                    Color hintColor = new Color(140, 170, 75) * (alpha * 0.7f);
                    Utils.DrawBorderString(spriteBatch, HoldFragmentHintText.Value, hintPos, hintColor, textScale);
                }
            }

            //进度条
            Vector2 progressBarPos = startPos + new Vector2(0, 65);
            DrawProgressBar(spriteBatch, progressBarPos, alpha);
        }

        /// <summary>
        /// 绘制任务完成状态
        /// </summary>
        private void DrawQuestCompleted(SpriteBatch spriteBatch, Vector2 startPos, float alpha, float textScale) {
            float pulse = (float)Math.Sin(pulseTimer * 2f) * 0.3f + 0.7f;
            Color completeColor = Color.Lerp(new Color(180, 220, 100), Color.LimeGreen, pulse) * alpha;

            Utils.DrawBorderString(spriteBatch, QuestCompleteText.Value, startPos, completeColor, textScale * 1.2f);

            Vector2 progressBarPos = startPos + new Vector2(0, 35);
            DrawProgressBar(spriteBatch, progressBarPos, alpha);
        }

        protected override void DrawProgressBar(SpriteBatch spriteBatch, Vector2 position, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            float barWidth = PanelWidth - 20;
            float barHeight = 8;

            //背景
            Rectangle barBg = new Rectangle((int)position.X, (int)position.Y, (int)barWidth, (int)barHeight);
            spriteBatch.Draw(pixel, barBg, new Rectangle(0, 0, 1, 1), new Color(10, 15, 8) * (alpha * 0.8f));

            //进度填充
            var (current, total, _) = GetTrackingData();
            total = Vector2.Distance(Main.LocalPlayer.Center, OldDukeCampsite.CampsitePosition) / 16f;
            current = 3000f;
            float progress = 1f - total / current;
            float fillWidth = barWidth * Math.Min(progress, 1f);

            if (fillWidth > 0) {
                Rectangle barFill = new Rectangle((int)position.X + 1, (int)position.Y + 1, (int)fillWidth - 2, (int)barHeight - 2);

                //硫磺海进度条颜色
                Color fillStart = new Color(100, 140, 50);
                Color fillEnd = new Color(160, 190, 80);

                //绘制渐变进度条
                int segmentCount = 20;
                for (int i = 0; i < segmentCount; i++) {
                    float t = i / (float)segmentCount;
                    float t2 = (i + 1) / (float)segmentCount;
                    int x1 = (int)(barFill.X + t * barFill.Width);
                    int x2 = (int)(barFill.X + t2 * barFill.Width);

                    Color segColor = Color.Lerp(fillStart, fillEnd, t);
                    float pulse = (float)Math.Sin(pulseTimer * 2f + t * MathHelper.Pi) * 0.3f + 0.7f;

                    spriteBatch.Draw(pixel, new Rectangle(x1, barFill.Y, Math.Max(1, x2 - x1), barFill.Height),
                        segColor * (alpha * pulse));
                }

                //发光效果
                Color glowColor = new Color(160, 190, 80) * (alpha * 0.4f);
                spriteBatch.Draw(pixel, new Rectangle(barFill.X, barFill.Y - 1, barFill.Width, 1), glowColor);
                spriteBatch.Draw(pixel, new Rectangle(barFill.X, barFill.Bottom, barFill.Width, 1), glowColor);
            }

            //边框
            Color borderColor = new Color(100, 140, 50) * (alpha * 0.8f);
            spriteBatch.Draw(pixel, new Rectangle(barBg.X, barBg.Y, barBg.Width, 1), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(barBg.X, barBg.Bottom - 1, barBg.Width, 1), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(barBg.X, barBg.Y, 1, barBg.Height), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(barBg.Right - 1, barBg.Y, 1, barBg.Height), borderColor);
        }
    }
}
