using CalamityOverhaul.Content.ADV.Common;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Draedons.Quest.DeploySignaltowers
{
    /// <summary>
    /// 信号塔搭建任务追踪UI(嘉登科技风格)
    /// </summary>
    internal class DeploySignaltowerTrackerUI : BaseQuestTrackerUI
    {
        public static DeploySignaltowerTrackerUI Instance => UIHandleLoader.GetUIHandleOfType<DeploySignaltowerTrackerUI>();

        public override string LocalizationCategory => "UI";

        public override int TargetNPCType => -1;//信号塔任务不需要NPC

        protected override void SetupLocalizedTexts() {
            QuestTitle = this.GetLocalization(nameof(QuestTitle), () => "量子纠缠网络部署");
            DamageContribution = this.GetLocalization(nameof(DamageContribution), () => "部署进度");
            RequiredContribution = this.GetLocalization(nameof(RequiredContribution), () => "目标:10座信号塔");
        }

        public override bool CanOpne {
            get {
                if (!Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                    return false;
                }

                ADVSave save = halibutPlayer.ADCSave;
                if (save == null) {
                    return false;
                }

                //如果任务未接受或已完成则不显示
                if (!save.DeploySignaltowerQuestAccepted || save.DeploySignaltowerQuestCompleted) {
                    return false;
                }

                //如果玩家不在世界中则不显示
                if (Main.LocalPlayer == null || !Main.LocalPlayer.active) {
                    return false;
                }

                return true;
            }
        }

        protected override (float current, float total, bool isActive) GetTrackingData() {
            if (!Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                return (0, DeploySignaltowerCheck.TargetTowerCount, false);
            }

            ADVSave save = halibutPlayer.ADCSave;
            if (save == null) {
                return (0, DeploySignaltowerCheck.TargetTowerCount, false);
            }

            float current = DeploySignaltowerCheck.DeployedTowerCount;
            float total = DeploySignaltowerCheck.TargetTowerCount;
            bool isActive = save.DeploySignaltowerQuestAccepted && !save.DeploySignaltowerQuestCompleted;

            return (current, total, isActive);
        }

        protected override float GetRequiredContribution() {
            return 1.0f;//信号塔任务不需要贡献度要求只需要完成全部
        }

        protected override void UpdatePanelHeight() {
            base.UpdatePanelHeight();
            currentPanelHeight += 30f; //为额外信息增加高度
        }

        protected override void DrawPanel(SpriteBatch spriteBatch, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //阴影
            Rectangle shadowRect = UIHitBox;
            shadowRect.Offset(4, 4);
            spriteBatch.Draw(pixel, shadowRect, new Rectangle(0, 0, 1, 1), Color.Black * (alpha * 0.6f));

            //主背景渐变(科技蓝色调)
            int segs = 25;
            for (int i = 0; i < segs; i++) {
                float t = i / (float)segs;
                float t2 = (i + 1) / (float)segs;
                int y1 = (int)(DrawPosition.Y + t * currentPanelHeight);
                int y2 = (int)(DrawPosition.Y + t2 * currentPanelHeight);
                Rectangle r = new Rectangle((int)DrawPosition.X, y1, (int)PanelWidth, Math.Max(1, y2 - y1));

                Color techDark = new Color(8, 15, 28);
                Color techMid = new Color(15, 30, 50);
                Color techEdge = new Color(30, 60, 90);

                float pulse = (float)Math.Sin(pulseTimer * 0.8f + t * 2.5f) * 0.5f + 0.5f;
                Color blendBase = Color.Lerp(techDark, techMid, pulse);
                Color c = Color.Lerp(blendBase, techEdge, t * 0.4f);
                c *= alpha * 0.9f;

                spriteBatch.Draw(pixel, r, new Rectangle(0, 0, 1, 1), c);
            }

            //全息扫描效果
            float scanPulse = (float)Math.Sin(pulseTimer * 1.5f) * 0.5f + 0.5f;
            Color scanOverlay = new Color(20, 50, 80) * (alpha * 0.2f * scanPulse);
            spriteBatch.Draw(pixel, UIHitBox, new Rectangle(0, 0, 1, 1), scanOverlay);

            //科技边框
            DrawTechFrame(spriteBatch, UIHitBox, alpha, borderGlow);

            //边缘数据流光效
            DrawDataStreamEdge(spriteBatch, UIHitBox, alpha);
        }

        private void DrawDataStreamEdge(SpriteBatch spriteBatch, Rectangle rect, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //顶部数据流
            float streamOffset = (float)Math.Sin(pulseTimer * 2f) * 10f;
            for (int i = 0; i < 3; i++) {
                float x = rect.X + streamOffset + i * 30f;
                if (x > rect.X && x < rect.Right - 20) {
                    Color streamColor = new Color(80, 200, 255) * (alpha * 0.3f * (1f - i * 0.3f));
                    spriteBatch.Draw(pixel, new Rectangle((int)x, rect.Y + 2, 15, 2), streamColor);
                }
            }

            //底部数据流
            for (int i = 0; i < 3; i++) {
                float x = rect.Right - streamOffset - i * 30f;
                if (x > rect.X + 20 && x < rect.Right) {
                    Color streamColor = new Color(100, 220, 255) * (alpha * 0.3f * (1f - i * 0.3f));
                    spriteBatch.Draw(pixel, new Rectangle((int)x - 15, rect.Bottom - 4, 15, 2), streamColor);
                }
            }
        }

        protected override void DrawContent(SpriteBatch spriteBatch, float alpha) {
            var font = FontAssets.MouseText.Value;
            const float titleScale = 0.72f;
            const float textScale = 0.62f;

            //标题
            Vector2 titlePos = DrawPosition + new Vector2(10, 8);
            Color titleColor = new Color(100, 220, 255) * alpha;

            //标题加强发光效果
            Color titleGlow = new Color(80, 200, 255) * (alpha * 0.6f);
            for (int i = 0; i < 4; i++) {
                float a = MathHelper.TwoPi * i / 4f;
                Vector2 off = a.ToRotationVector2() * 1.5f;
                Utils.DrawBorderString(spriteBatch, QuestTitle.Value, titlePos + off, titleGlow * 0.5f, titleScale);
            }

            Utils.DrawBorderString(spriteBatch, QuestTitle.Value, titlePos, titleColor, titleScale);

            //分隔线
            float titleHeight = font.MeasureString(QuestTitle.Value).Y * titleScale;
            Vector2 dividerStart = titlePos + new Vector2(0, titleHeight + 4);
            Vector2 dividerEnd = dividerStart + new Vector2(PanelWidth - 20, 0);
            DrawGradientLine(spriteBatch, dividerStart, dividerEnd,
                new Color(80, 200, 255) * (alpha * 0.9f), new Color(80, 200, 255) * (alpha * 0.1f), 1.5f);

            //获取最近的目标点
            SignalTowerTargetPoint nearestTarget = SignalTowerTargetManager.GetNearestTarget(Main.LocalPlayer);
            if (nearestTarget != null) {
                bool playerInRange = nearestTarget.IsPlayerInRange(Main.LocalPlayer);

                Vector2 targetInfoPos = dividerStart + new Vector2(0, 12);

                //目标编号和状态
                string targetText = $"最近的目标点: {nearestTarget.Index + 1}号纠缠节点";
                Color targetTextColor = playerInRange
                    ? Color.Lerp(new Color(255, 200, 100), Color.LimeGreen, 0.5f) * alpha
                    : new Color(255, 200, 100) * alpha;

                Utils.DrawBorderString(spriteBatch, targetText, targetInfoPos, targetTextColor, textScale);

                //距离或状态
                Vector2 distancePos = targetInfoPos + new Vector2(0, 15);
                string distanceText;
                Color distanceColor;

                if (playerInRange) {
                    distanceText = "状态: 范围内";
                    distanceColor = Color.LimeGreen * alpha;

                    //添加脉冲效果
                    float pulse = (float)Math.Sin(pulseTimer * 3f) * 0.3f + 0.7f;
                    distanceColor *= pulse;
                }
                else {
                    float distance = Vector2.Distance(Main.LocalPlayer.Center, nearestTarget.WorldPosition) / 16f;
                    distanceText = $"距离: {(int)distance}m";
                    distanceColor = new Color(200, 230, 255) * alpha;
                }

                Utils.DrawBorderString(spriteBatch, distanceText, distancePos, distanceColor, textScale * 0.9f);

                //进度文本
                Vector2 progressTextPos = distancePos + new Vector2(0, 15);
                string progressText = $"{DamageContribution.Value}: ";
                Utils.DrawBorderString(spriteBatch, progressText, progressTextPos,
                    new Color(200, 230, 255) * alpha, textScale);

                //进度数字
                Vector2 numberPos = progressTextPos + new Vector2(font.MeasureString(progressText).X * textScale, 0);
                string numberText = $"{DeploySignaltowerCheck.DeployedTowerCount}/{DeploySignaltowerCheck.TargetTowerCount}";

                Color numberColor;
                if (DeploySignaltowerCheck.DeployedTowerCount >= DeploySignaltowerCheck.TargetTowerCount) {
                    numberColor = Color.LimeGreen;
                }
                else {
                    numberColor = Color.Lerp(new Color(100, 200, 255), Color.Cyan,
                        DeploySignaltowerCheck.DeployedTowerCount / (float)DeploySignaltowerCheck.TargetTowerCount);
                }

                Utils.DrawBorderString(spriteBatch, numberText, numberPos, numberColor * alpha, 0.7f);

                //进度条
                DrawProgressBar(spriteBatch, progressTextPos + new Vector2(0, 18), alpha);
            }
            else {
                //没有目标,显示完成状态
                Vector2 progressTextPos = dividerStart + new Vector2(0, 12);
                string completedText = "任务完成!";
                Utils.DrawBorderString(spriteBatch, completedText, progressTextPos,
                    Color.Gold * alpha, textScale * 1.2f);

                Vector2 numberPos = progressTextPos + new Vector2(0, 20);
                string numberText = $"{DeploySignaltowerCheck.DeployedTowerCount}/{DeploySignaltowerCheck.TargetTowerCount}";
                Utils.DrawBorderString(spriteBatch, numberText, numberPos, Color.LimeGreen * alpha, 0.7f);

                DrawProgressBar(spriteBatch, progressTextPos + new Vector2(0, 45), alpha);
            }
        }

        protected override void DrawProgressBar(SpriteBatch spriteBatch, Vector2 position, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            float barWidth = PanelWidth - 20;
            float barHeight = 8;

            //背景
            Rectangle barBg = new Rectangle((int)position.X, (int)position.Y, (int)barWidth, (int)barHeight);
            spriteBatch.Draw(pixel, barBg, new Rectangle(0, 0, 1, 1), new Color(10, 20, 35) * (alpha * 0.8f));

            //进度填充
            float progress = DeploySignaltowerCheck.DeployedTowerCount / (float)DeploySignaltowerCheck.TargetTowerCount;
            float fillWidth = barWidth * Math.Min(progress, 1f);

            if (fillWidth > 0) {
                Rectangle barFill = new Rectangle((int)position.X + 1, (int)position.Y + 1, (int)fillWidth - 2, (int)barHeight - 2);

                //渐变填充色
                Color fillStart = new Color(60, 180, 255);
                Color fillEnd = new Color(100, 220, 255);

                //绘制渐变填充
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
                Color glowColor = new Color(100, 220, 255) * (alpha * 0.4f);
                spriteBatch.Draw(pixel, new Rectangle(barFill.X, barFill.Y - 1, barFill.Width, 1), glowColor);
                spriteBatch.Draw(pixel, new Rectangle(barFill.X, barFill.Bottom, barFill.Width, 1), glowColor);
            }

            //边框
            Color borderColor = new Color(80, 200, 255) * (alpha * 0.8f);
            spriteBatch.Draw(pixel, new Rectangle(barBg.X, barBg.Y, barBg.Width, 1), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(barBg.X, barBg.Bottom - 1, barBg.Width, 1), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(barBg.X, barBg.Y, 1, barBg.Height), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(barBg.Right - 1, barBg.Y, 1, barBg.Height), borderColor);

            //数据流动画
            DrawProgressDataFlow(spriteBatch, barBg, alpha, progress);
        }

        private void DrawProgressDataFlow(SpriteBatch spriteBatch, Rectangle barRect, float alpha, float progress) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //只在未完成时显示数据流
            if (progress >= 1f) return;

            float flowSpeed = (float)Main.timeForVisualEffects * 0.05f;
            int flowCount = 3;

            for (int i = 0; i < flowCount; i++) {
                float offset = (flowSpeed + i * 0.3f) % 1.2f - 0.1f;
                int flowX = (int)(barRect.X + offset * barRect.Width);

                if (flowX >= barRect.X && flowX <= barRect.Right) {
                    float flowAlpha = (float)Math.Sin((offset + 0.1f) * MathHelper.Pi) * 0.8f;
                    Color flowColor = new Color(120, 240, 255) * (alpha * flowAlpha * 0.5f);

                    spriteBatch.Draw(pixel, new Rectangle(flowX - 1, barRect.Y, 3, barRect.Height), flowColor);
                }
            }
        }

        private static void DrawTechFrame(SpriteBatch sb, Rectangle rect, float alpha, float pulse) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Color techEdge = Color.Lerp(new Color(60, 180, 255), new Color(100, 220, 255), pulse) * (alpha * 0.9f);

            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 3), new Rectangle(0, 0, 1, 1), techEdge);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 3, rect.Width, 3), new Rectangle(0, 0, 1, 1), techEdge * 0.8f);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, 3, rect.Height), new Rectangle(0, 0, 1, 1), techEdge * 0.95f);
            sb.Draw(pixel, new Rectangle(rect.Right - 3, rect.Y, 3, rect.Height), new Rectangle(0, 0, 1, 1), techEdge * 0.95f);

            Rectangle inner = rect;
            inner.Inflate(-6, -6);
            Color innerC = new Color(100, 220, 255) * (alpha * 0.18f * pulse);
            sb.Draw(pixel, new Rectangle(inner.X, inner.Y, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerC);
            sb.Draw(pixel, new Rectangle(inner.X, inner.Bottom - 1, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerC * 0.7f);
            sb.Draw(pixel, new Rectangle(inner.X, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerC * 0.9f);
            sb.Draw(pixel, new Rectangle(inner.Right - 1, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerC * 0.9f);

            DrawCornerNode(sb, new Vector2(rect.X + 8, rect.Y + 8), alpha * 0.95f, pulse);
            DrawCornerNode(sb, new Vector2(rect.Right - 8, rect.Y + 8), alpha * 0.95f, pulse);
            DrawCornerNode(sb, new Vector2(rect.X + 8, rect.Bottom - 8), alpha * 0.7f, pulse);
            DrawCornerNode(sb, new Vector2(rect.Right - 8, rect.Bottom - 8), alpha * 0.7f, pulse);
        }

        private static void DrawCornerNode(SpriteBatch sb, Vector2 pos, float a, float pulse) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float size = 5f + (float)Math.Sin(pulse * MathHelper.TwoPi) * 1f;
            Color c = new Color(100, 220, 255) * (a * (0.8f + pulse * 0.2f));

            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), c, 0f, new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.2f), SpriteEffects.None, 0f);
            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), c * 0.9f, MathHelper.PiOver2, new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.2f), SpriteEffects.None, 0f);
            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), c * 0.5f, 0f, new Vector2(0.5f, 0.5f), new Vector2(size * 0.4f, size * 0.4f), SpriteEffects.None, 0f);
        }
    }
}
