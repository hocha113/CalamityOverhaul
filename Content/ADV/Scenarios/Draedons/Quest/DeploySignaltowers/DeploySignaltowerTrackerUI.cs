using CalamityOverhaul.Content.ADV.ADVQuestTracker;
using CalamityOverhaul.Content.ADV.Scenarios.Draedons.Quest.DeploySignaltowers.SignalTower;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Draedons.Quest.DeploySignaltowers
{
    /// <summary>
    /// 信号塔搭建任务追踪UI
    /// </summary>
    internal class DeploySignaltowerTrackerUI : BaseQuestTrackerUI
    {
        public static DeploySignaltowerTrackerUI Instance => UIHandleLoader.GetUIHandleOfType<DeploySignaltowerTrackerUI>();

        public override string LocalizationCategory => "UI";

        public override int TargetNPCType => -1; //信号塔任务不需要NPC

        //本地化文本
        public static LocalizedText NearestTargetText { get; private set; }
        public static LocalizedText NodeText { get; private set; }
        public static LocalizedText StatusText { get; private set; }
        public static LocalizedText InRangeText { get; private set; }
        public static LocalizedText DistanceText { get; private set; }
        public static LocalizedText QuestCompleteText { get; private set; }

        protected override QuestTrackerStyle GetQuestStyle() => QuestTrackerStyle.Draedon;

        public override void SetStaticDefaults() {
            base.SetStaticDefaults();

            NearestTargetText = this.GetLocalization(nameof(NearestTargetText), () => "最近的目标点");
            NodeText = this.GetLocalization(nameof(NodeText), () => "[NUM]号纠缠节点");
            StatusText = this.GetLocalization(nameof(StatusText), () => "状态");
            InRangeText = this.GetLocalization(nameof(InRangeText), () => "范围内");
            DistanceText = this.GetLocalization(nameof(DistanceText), () => "距离");
            QuestCompleteText = this.GetLocalization(nameof(QuestCompleteText), () => "任务完成!");
        }

        protected override void SetupLocalizedTexts() {
            QuestTitle = this.GetLocalization(nameof(QuestTitle), () => "量子纠缠网络部署");
            DamageContribution = this.GetLocalization(nameof(DamageContribution), () => "部署进度");
            RequiredContribution = this.GetLocalization(nameof(RequiredContribution), () => "目标:10座信号塔");
        }

        public override bool CanOpne {
            get {
                if (Main.LocalPlayer == null || !Main.LocalPlayer.active) {
                    return false;
                }

                if (!Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                    return false;
                }

                if (DSTPlayer.HasDeploySignaltowerQuestByWorld) {
                    return SignalTowerTargetManager.TargetPoints.Count > 0;
                }

                ADVSave save = halibutPlayer.ADVSave;
                if (save == null) {
                    return false;
                }

                if (save.DeploySignaltowerQuestCompleted) {
                    return false;
                }

                if (!save.DeploySignaltowerQuestAccepted) {
                    return false;
                }

                return SignalTowerTargetManager.TargetPoints.Count > 0;
            }
        }

        protected override (float current, float total, bool isActive) GetTrackingData() {
            if (!Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                return (0, DeploySignaltowerCheck.TargetTowerCount, false);
            }

            ADVSave save = halibutPlayer.ADVSave;
            if (save == null) {
                return (0, DeploySignaltowerCheck.TargetTowerCount, false);
            }

            float current = DeploySignaltowerCheck.DeployedTowerCount;
            float total = DeploySignaltowerCheck.TargetTowerCount;
            bool isActive = save.DeploySignaltowerQuestAccepted && !save.DeploySignaltowerQuestCompleted;

            return (current, total, isActive);
        }

        protected override float GetRequiredContribution() {
            return 1.0f; //信号塔任务需要完成全部
        }

        protected override void UpdatePanelHeight() {
            base.UpdatePanelHeight();
            currentPanelHeight += 30f; //为额外信息增加高度
        }

        /// <summary>
        /// 重写标题行绘制，添加强化发光效果
        /// </summary>
        protected override void DrawTitleLine(SpriteBatch spriteBatch, string text, Vector2 position, Color color, float scale, float alpha) {
            // 标题加强发光效果
            Color titleGlow = new Color(80, 200, 255) * (alpha * 0.6f);
            for (int i = 0; i < 4; i++) {
                float a = MathHelper.TwoPi * i / 4f;
                Vector2 off = a.ToRotationVector2() * 1.5f;
                Utils.DrawBorderString(spriteBatch, text, position + off, titleGlow * 0.5f, scale);
            }

            //绘制主文本
            Utils.DrawBorderString(spriteBatch, text, position, color, scale);
        }

        protected override void DrawContent(SpriteBatch spriteBatch, float alpha) {
            var font = FontAssets.MouseText.Value;
            const float titleScale = 0.72f;
            const float textScale = 0.62f;

            //使用基类的标题绘制接口，自动支持换行和特殊效果
            Vector2 titlePos = DrawPosition + new Vector2(10, 8);
            float titleHeight = DrawTitle(spriteBatch, titlePos, alpha, titleScale);

            //分隔线
            Vector2 dividerStart = titlePos + new Vector2(0, titleHeight + 4);
            Vector2 dividerEnd = dividerStart + new Vector2(PanelWidth - 20, 0);
            currentStyle?.DrawDivider(spriteBatch, dividerStart, dividerEnd, alpha);

            //获取最近的目标点
            SignalTowerTargetPoint nearestTarget = SignalTowerTargetManager.GetNearestTarget(Main.LocalPlayer);
            if (nearestTarget != null) {
                DrawTargetInfo(spriteBatch, dividerStart, nearestTarget, alpha, textScale);
            }
            else {
                DrawQuestComplete(spriteBatch, dividerStart, alpha, textScale);
            }
        }

        private void DrawTargetInfo(SpriteBatch spriteBatch, Vector2 startPos, SignalTowerTargetPoint target, float alpha, float textScale) {
            var font = FontAssets.MouseText.Value;
            bool playerInRange = target.IsPlayerInRange(Main.LocalPlayer);

            Vector2 targetInfoPos = startPos + new Vector2(0, 12);

            //目标编号和状态
            string targetText = $"{NearestTargetText.Value}: {NodeText.Value.Replace("[NUM]", (target.Index + 1).ToString())}";
            Color targetTextColor = playerInRange
                ? Color.Lerp(new Color(255, 200, 100), Color.LimeGreen, 0.5f) * alpha
                : new Color(255, 200, 100) * alpha;

            Utils.DrawBorderString(spriteBatch, targetText, targetInfoPos, targetTextColor, textScale);

            //距离或状态
            Vector2 distancePos = targetInfoPos + new Vector2(0, 15);
            string distanceText;
            Color distanceColor;

            if (playerInRange) {
                distanceText = $"{StatusText.Value}: {InRangeText.Value}";
                distanceColor = Color.LimeGreen * alpha;

                //添加脉冲效果
                float pulse = (float)Math.Sin(pulseTimer * 3f) * 0.3f + 0.7f;
                distanceColor *= pulse;
            }
            else {
                float distance = Vector2.Distance(Main.LocalPlayer.Center, target.WorldPosition) / 16f;
                distanceText = $"{DistanceText.Value}: {(int)distance}m";
                distanceColor = new Color(200, 230, 255) * alpha;
            }

            Utils.DrawBorderString(spriteBatch, distanceText, distancePos, distanceColor, textScale * 0.9f);

            //进度文本
            Vector2 progressTextPos = distancePos + new Vector2(0, 15);
            DrawContributionText(spriteBatch, progressTextPos, alpha, textScale);

            //进度条
            DrawProgressBar(spriteBatch, progressTextPos + new Vector2(0, 18), alpha);
        }

        private void DrawQuestComplete(SpriteBatch spriteBatch, Vector2 startPos, float alpha, float textScale) {
            Vector2 progressTextPos = startPos + new Vector2(0, 12);
            Utils.DrawBorderString(spriteBatch, QuestCompleteText.Value, progressTextPos,
                Color.Gold * alpha, textScale * 1.2f);

            Vector2 numberPos = progressTextPos + new Vector2(0, 20);
            string numberText = $"{DeploySignaltowerCheck.DeployedTowerCount}/{DeploySignaltowerCheck.TargetTowerCount}";
            Utils.DrawBorderString(spriteBatch, numberText, numberPos, Color.LimeGreen * alpha, 0.7f);

            DrawProgressBar(spriteBatch, progressTextPos + new Vector2(0, 45), alpha);
        }

        protected override void DrawContributionText(SpriteBatch spriteBatch, Vector2 position, float alpha, float textScale) {
            var font = FontAssets.MouseText.Value;
            string progressText = $"{DamageContribution.Value}: ";
            Utils.DrawBorderString(spriteBatch, progressText, position,
                new Color(200, 230, 255) * alpha, textScale);

            //进度数字
            Vector2 numberPos = position + new Vector2(font.MeasureString(progressText).X * textScale, 0);
            string numberText = $"{DeploySignaltowerCheck.DeployedTowerCount}/{DeploySignaltowerCheck.TargetTowerCount}";

            Color numberColor = DeploySignaltowerCheck.DeployedTowerCount >= DeploySignaltowerCheck.TargetTowerCount
                ? Color.LimeGreen
                : Color.Lerp(new Color(100, 200, 255), Color.Cyan,
                    DeploySignaltowerCheck.DeployedTowerCount / (float)DeploySignaltowerCheck.TargetTowerCount);

            Utils.DrawBorderString(spriteBatch, numberText, numberPos, numberColor * alpha, 0.7f);
        }
    }
}
