using CalamityOverhaul.Content.QuestLogs.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.QuestLogs.Styles
{
    public class HotwindQuestLogStyle : IQuestLogStyle
    {
        //动画计时器
        private float flowTimer;
        private float pulseTimer;
        private float bloomTimer;

        public void DrawBackground(SpriteBatch spriteBatch, QuestLog log, Rectangle panelRect) {
            pulseTimer += 0.025f;
            bloomTimer += 0.015f;

            Texture2D pixel = VaultAsset.placeholder2.Value;

            //绘制深色阴影
            Rectangle shadowRect = panelRect;
            shadowRect.Offset(6, 6);
            spriteBatch.Draw(pixel, shadowRect, Color.Black * 0.6f);

            //绘制半透明黑色背景
            spriteBatch.Draw(pixel, panelRect, Color.Black * 0.85f);

            //绘制内部渐变效果
            int gradientSteps = 20;
            for (int i = 0; i < gradientSteps; i++) {
                float t = i / (float)gradientSteps;
                int y = panelRect.Y + (int)(t * panelRect.Height);
                int height = Math.Max(1, panelRect.Height / gradientSteps);
                Rectangle gradRect = new Rectangle(panelRect.X, y, panelRect.Width, height);
                Color gradColor = Color.Lerp(new Color(20, 10, 5), new Color(40, 20, 10), t);
                spriteBatch.Draw(pixel, gradRect, gradColor * 0.3f);
            }

            //绘制纵向渐变屏幕泛光动画
            DrawBloomEffect(spriteBatch, pixel, panelRect);

            //绘制脉冲光效
            float pulse = (float)Math.Sin(pulseTimer * 2f) * 0.5f + 0.5f;
            Color pulseColor = new Color(255, 140, 60) * (0.08f * pulse);
            spriteBatch.Draw(pixel, panelRect, pulseColor);

            //绘制边框
            int border = 3;
            Color edgeColor = Color.Lerp(new Color(255, 120, 40), new Color(255, 180, 100), pulse);
            spriteBatch.Draw(pixel, new Rectangle(panelRect.X, panelRect.Y, panelRect.Width, border), edgeColor * 0.95f);
            spriteBatch.Draw(pixel, new Rectangle(panelRect.X, panelRect.Bottom - border, panelRect.Width, border), edgeColor * 0.75f);
            spriteBatch.Draw(pixel, new Rectangle(panelRect.X, panelRect.Y, border, panelRect.Height), edgeColor * 0.85f);
            spriteBatch.Draw(pixel, new Rectangle(panelRect.Right - border, panelRect.Y, border, panelRect.Height), edgeColor * 0.85f);

            //绘制内边框发光
            Rectangle innerRect = panelRect;
            innerRect.Inflate(-6, -6);
            Color innerGlow = new Color(255, 140, 60) * (0.15f * pulse);
            spriteBatch.Draw(pixel, new Rectangle(innerRect.X, innerRect.Y, innerRect.Width, 1), innerGlow);
            spriteBatch.Draw(pixel, new Rectangle(innerRect.X, innerRect.Bottom - 1, innerRect.Width, 1), innerGlow * 0.7f);
            spriteBatch.Draw(pixel, new Rectangle(innerRect.X, innerRect.Y, 1, innerRect.Height), innerGlow * 0.85f);
            spriteBatch.Draw(pixel, new Rectangle(innerRect.Right - 1, innerRect.Y, 1, innerRect.Height), innerGlow * 0.85f);

            //绘制角落装饰
            DrawCornerMark(spriteBatch, new Vector2(panelRect.X + 12, panelRect.Y + 12), pulse);
            DrawCornerMark(spriteBatch, new Vector2(panelRect.Right - 12, panelRect.Y + 12), pulse);
            DrawCornerMark(spriteBatch, new Vector2(panelRect.X + 12, panelRect.Bottom - 12), pulse * 0.7f);
            DrawCornerMark(spriteBatch, new Vector2(panelRect.Right - 12, panelRect.Bottom - 12), pulse * 0.7f);
        }

        private void DrawBloomEffect(SpriteBatch spriteBatch, Texture2D pixel, Rectangle panelRect) {
            //创建纵向多层渐变泛光效果，从左到右流动
            int bloomLayers = 4;

            for (int layer = 0; layer < bloomLayers; layer++) {
                //每层有不同的速度和相位偏移
                float layerSpeed = 0.8f + layer * 0.15f;
                float layerOffset = (bloomTimer * layerSpeed + layer * 1.2f) % MathHelper.TwoPi;

                //使用平滑的往复运动而非简单的正弦
                float rawPosition = (float)Math.Sin(layerOffset);
                float bloomPosition = rawPosition * 0.5f + 0.5f;

                //计算泛光中心X位置
                int centerX = panelRect.X + (int)(bloomPosition * panelRect.Width);

                //绘制渐变泛光柱
                int bloomWidth = 120 + layer * 30;
                int bloomSteps = 50;

                for (int i = 0; i < bloomSteps; i++) {
                    float t = i / (float)bloomSteps;
                    //计算距离中心的归一化距离
                    float distance = Math.Abs(t - 0.5f) * 2f;
                    //使用更平滑的衰减曲线
                    float alpha = 1f - distance;
                    alpha = (float)Math.Pow(alpha, 3.5);

                    int x = centerX - bloomWidth / 2 + (int)(t * bloomWidth);

                    //确保不超出面板范围
                    if (x < panelRect.X || x >= panelRect.Right) continue;

                    int width = Math.Max(1, bloomWidth / bloomSteps);
                    Rectangle bloomRect = new Rectangle(x, panelRect.Y, width, panelRect.Height);

                    //多层动态颜色渐变
                    Color bloomColor1 = new Color(255, 100, 30);
                    Color bloomColor2 = new Color(255, 160, 60);
                    Color bloomColor3 = new Color(255, 200, 100);
                    Color bloomColor4 = new Color(255, 140, 50);

                    //根据层数和位置创建复杂的颜色混合
                    float colorPhase = (t + layer * 0.25f) % 1f;
                    Color finalColor;

                    if (layer % 2 == 0) {
                        if (colorPhase < 0.5f) {
                            finalColor = Color.Lerp(bloomColor1, bloomColor2, colorPhase * 2f);
                        }
                        else {
                            finalColor = Color.Lerp(bloomColor2, bloomColor3, (colorPhase - 0.5f) * 2f);
                        }
                    }
                    else {
                        if (colorPhase < 0.5f) {
                            finalColor = Color.Lerp(bloomColor4, bloomColor3, colorPhase * 2f);
                        }
                        else {
                            finalColor = Color.Lerp(bloomColor3, bloomColor1, (colorPhase - 0.5f) * 2f);
                        }
                    }

                    //添加基于位置的亮度变化
                    float brightnessVariation = (float)Math.Sin(colorPhase * MathHelper.TwoPi + bloomTimer * 2f) * 0.15f + 1f;
                    finalColor = new Color(
                        (int)(finalColor.R * brightnessVariation),
                        (int)(finalColor.G * brightnessVariation),
                        (int)(finalColor.B * brightnessVariation)
                    );

                    //每层的基础透明度递减
                    float layerAlpha = 0.12f - layer * 0.025f;
                    spriteBatch.Draw(pixel, bloomRect, finalColor * (alpha * layerAlpha));
                }
            }
        }

        public void DrawNode(SpriteBatch spriteBatch, QuestNode node, Vector2 drawPos, float scale, bool isHovered) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            int size = (int)(48 * scale);
            Rectangle nodeRect = new Rectangle((int)drawPos.X - size / 2, (int)drawPos.Y - size / 2, size, size);

            //根据任务状态确定颜色
            Color baseColor = node.IsCompleted ? new Color(80, 200, 100) :
                             (node.IsUnlocked ? new Color(255, 140, 60) : new Color(100, 100, 110));

            if (isHovered) {
                baseColor = Color.Lerp(baseColor, Color.White, 0.4f);
            }

            //绘制节点阴影
            Rectangle shadowRect = nodeRect;
            shadowRect.Offset(4, 4);
            spriteBatch.Draw(pixel, shadowRect, Color.Black * 0.5f);

            //绘制节点背景
            spriteBatch.Draw(pixel, nodeRect, baseColor * 0.7f);

            //绘制节点发光效果
            if (node.IsUnlocked || node.IsCompleted) {
                float glowPulse = (float)Math.Sin(Main.GameUpdateCount * 0.05f) * 0.5f + 0.5f;
                Color glowColor = node.IsCompleted ? new Color(100, 255, 120) : new Color(255, 180, 100);

                for (int i = 0; i < 4; i++) {
                    float angle = MathHelper.TwoPi * i / 4f;
                    Vector2 offset = angle.ToRotationVector2() * (3f * glowPulse);
                    Rectangle glowRect = nodeRect;
                    glowRect.Offset((int)offset.X, (int)offset.Y);
                    spriteBatch.Draw(pixel, glowRect, glowColor * (0.3f * glowPulse));
                }
            }

            //绘制节点边框
            int borderWidth = 2;
            Color edgeColor = node.IsCompleted ? new Color(120, 255, 140) :
                             (node.IsUnlocked ? new Color(255, 160, 80) : new Color(120, 120, 130));

            if (isHovered) {
                edgeColor = Color.White;
                borderWidth = 3;
            }

            spriteBatch.Draw(pixel, new Rectangle(nodeRect.X, nodeRect.Y, nodeRect.Width, borderWidth), edgeColor);
            spriteBatch.Draw(pixel, new Rectangle(nodeRect.X, nodeRect.Bottom - borderWidth, nodeRect.Width, borderWidth), edgeColor * 0.8f);
            spriteBatch.Draw(pixel, new Rectangle(nodeRect.X, nodeRect.Y, borderWidth, nodeRect.Height), edgeColor * 0.9f);
            spriteBatch.Draw(pixel, new Rectangle(nodeRect.Right - borderWidth, nodeRect.Y, borderWidth, nodeRect.Height), edgeColor * 0.9f);

            //绘制任务类型图标
            DrawQuestTypeIcon(spriteBatch, node, drawPos, scale);

            //绘制节点名称
            Vector2 namePos = new Vector2(drawPos.X, drawPos.Y + size / 2 + 8);
            Vector2 nameSize = FontAssets.MouseText.Value.MeasureString(node.Name) * 0.75f;

            Color textColor = node.IsCompleted ? new Color(140, 255, 160) :
                             (node.IsUnlocked ? new Color(255, 200, 140) : new Color(140, 140, 150));

            if (isHovered) {
                textColor = Color.White;
            }

            Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value, node.Name,
                namePos.X, namePos.Y, textColor, Color.Black, nameSize / 2, 0.75f);
        }

        public void DrawConnection(SpriteBatch spriteBatch, Vector2 start, Vector2 end, bool isUnlocked) {
            flowTimer += 0.025f;
            if (flowTimer > MathHelper.TwoPi) flowTimer -= MathHelper.TwoPi;

            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 diff = end - start;
            float length = diff.Length();
            float rotation = diff.ToRotation();

            //加粗的连接线宽度
            int lineWidth = 8;

            //绘制外层阴影
            Color shadowColor = Color.Black * 0.4f;
            spriteBatch.Draw(pixel, start + new Vector2(2, 2).RotatedBy(rotation),
                new Rectangle(0, 0, (int)length, lineWidth), shadowColor, rotation,
                new Vector2(0, lineWidth / 2f), 1f, SpriteEffects.None, 0f);

            //绘制基础暗色背景层
            Color lineColor = isUnlocked ? new Color(60, 45, 30) : new Color(40, 40, 45);
            spriteBatch.Draw(pixel, start, new Rectangle(0, 0, (int)length, lineWidth),
                lineColor * 0.9f, rotation, new Vector2(0, lineWidth / 2f), 1f, SpriteEffects.None, 0f);

            if (isUnlocked) {
                //绘制主动流动的渐变动画
                DrawFlowingGradient(spriteBatch, pixel, start, end, length, rotation, lineWidth);

                //绘制外发光效果
                Color glowColor = new Color(255, 140, 60) * 0.3f;
                int glowWidth = lineWidth + 6;
                spriteBatch.Draw(pixel, start, new Rectangle(0, 0, (int)length, glowWidth),
                    glowColor, rotation, new Vector2(0, glowWidth / 2f), 1f, SpriteEffects.None, 0f);
            }
            else {
                //未解锁状态的暗淡虚线效果
                DrawDashedLine(spriteBatch, pixel, start, length, rotation, lineWidth);
            }
        }

        private void DrawFlowingGradient(SpriteBatch spriteBatch, Texture2D pixel, Vector2 start, Vector2 end, float length, float rotation, int lineWidth) {
            //创建持续流动的渐变效果，从起点流向终点
            int segments = Math.Max((int)(length / 12f), 3);
            Vector2 direction = (end - start) / length;

            //流动偏移，确保是从0到1的连续运动
            float flowProgress = (flowTimer * 0.2f) % 1f;

            for (int i = 0; i < segments; i++) {
                float segmentStart = i / (float)segments;
                float segmentEnd = (i + 1) / (float)segments;
                float segmentLength = length * (segmentEnd - segmentStart);

                //计算当前段在流动动画中的位置
                float segmentCenter = (segmentStart + segmentEnd) * 0.5f;
                float flowPosition = (segmentCenter + flowProgress) % 1f;

                //创建多重渐变颜色系统
                Color[] gradientColors = new Color[] {
                    new Color(255, 80, 20),//深橙红
                    new Color(255, 140, 40),//橙色
                    new Color(255, 200, 80),//金橙色
                    new Color(255, 160, 50),//亮橙色
                    new Color(255, 100, 30)//回到深橙
                };

                //使用平滑插值计算颜色
                float colorIndex = flowPosition * (gradientColors.Length - 1);
                int index1 = (int)colorIndex;
                int index2 = (index1 + 1) % gradientColors.Length;
                float blend = colorIndex - index1;

                Color segmentColor = Color.Lerp(gradientColors[index1], gradientColors[index2], blend);

                //添加亮度脉冲效果
                float brightnessPulse = (float)Math.Sin(flowPosition * MathHelper.TwoPi * 2f + flowTimer) * 0.2f + 1f;
                segmentColor = new Color(
                    (int)(segmentColor.R * brightnessPulse),
                    (int)(segmentColor.G * brightnessPulse),
                    (int)(segmentColor.B * brightnessPulse)
                );

                //绘制主渐变层
                Vector2 segmentPos = start + direction * (length * segmentStart);
                int mainWidth = lineWidth - 2;

                float baseAlpha = 0.75f;
                //根据流动位置调整透明度，创建拖尾效果
                float positionAlpha = (float)Math.Sin(flowPosition * MathHelper.Pi);
                float finalAlpha = baseAlpha + positionAlpha * 0.25f;

                spriteBatch.Draw(pixel, segmentPos, new Rectangle(0, 0, (int)segmentLength, mainWidth),
                    segmentColor * finalAlpha, rotation, new Vector2(0, mainWidth / 2f), 1f, SpriteEffects.None, 0f);

                //添加内部高光层
                int highlightWidth = mainWidth - 2;
                Color highlightColor = Color.Lerp(segmentColor, Color.White, 0.4f);
                spriteBatch.Draw(pixel, segmentPos, new Rectangle(0, 0, (int)segmentLength, highlightWidth),
                    highlightColor * (finalAlpha * 0.5f), rotation, new Vector2(0, highlightWidth / 2f), 1f, SpriteEffects.None, 0f);
            }

            //添加流动的能量脉冲点
            int pulseCount = Math.Max((int)(length / 60f), 2);
            for (int i = 0; i < pulseCount; i++) {
                //每个脉冲有不同的相位
                float pulsePhase = (i / (float)pulseCount + flowProgress) % 1f;
                Vector2 pulsePos = start + (end - start) * pulsePhase;

                //使用正弦函数创建渐隐渐现效果
                float pulseAlpha = (float)Math.Sin(pulsePhase * MathHelper.Pi);
                pulseAlpha = (float)Math.Pow(pulseAlpha, 1.5f);

                //核心亮点
                Color pulseColor = new Color(255, 240, 200) * (pulseAlpha * 0.9f);
                spriteBatch.Draw(pixel, pulsePos, new Rectangle(0, 0, 1, 1), pulseColor,
                    rotation, new Vector2(0, 0.5f), new Vector2(16, 6), SpriteEffects.None, 0f);

                //外层光晕
                Color glowColor = new Color(255, 200, 120) * (pulseAlpha * 0.6f);
                spriteBatch.Draw(pixel, pulsePos, new Rectangle(0, 0, 1, 1), glowColor,
                    rotation, new Vector2(0, 0.5f), new Vector2(24, 10), SpriteEffects.None, 0f);
            }
        }

        private void DrawDashedLine(SpriteBatch spriteBatch, Texture2D pixel, Vector2 start, float length, float rotation, int lineWidth) {
            //绘制虚线效果表示未解锁
            int dashLength = 14;
            int gapLength = 10;
            int totalLength = dashLength + gapLength;
            int dashCount = (int)(length / totalLength);

            for (int i = 0; i < dashCount; i++) {
                float dashStart = i * totalLength;
                Vector2 dashPos = start + new Vector2(dashStart, 0).RotatedBy(rotation);

                Color dashColor = new Color(70, 70, 80) * 0.6f;
                spriteBatch.Draw(pixel, dashPos, new Rectangle(0, 0, dashLength, lineWidth),
                    dashColor, rotation, new Vector2(0, lineWidth / 2f), 1f, SpriteEffects.None, 0f);
            }
        }

        public Vector4 GetPadding() {
            return new Vector4(15, 35, 15, 15);//Left, Top, Right, Bottom
        }

        public void DrawQuestDetail(SpriteBatch spriteBatch, QuestNode node, Rectangle panelRect, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //绘制半透明背景遮罩
            Rectangle fullScreen = new Rectangle(0, 0, Main.screenWidth, Main.screenHeight);
            spriteBatch.Draw(pixel, fullScreen, Color.Black * (0.6f * alpha));

            //绘制详情面板阴影
            Rectangle shadowRect = panelRect;
            shadowRect.Offset(8, 8);
            spriteBatch.Draw(pixel, shadowRect, Color.Black * (0.7f * alpha));

            //绘制面板背景
            spriteBatch.Draw(pixel, panelRect, new Color(15, 10, 5) * alpha);

            //绘制渐变效果
            int gradSteps = 15;
            for (int i = 0; i < gradSteps; i++) {
                float t = i / (float)gradSteps;
                int y = panelRect.Y + (int)(t * panelRect.Height);
                int h = Math.Max(1, panelRect.Height / gradSteps);
                Rectangle gRect = new Rectangle(panelRect.X, y, panelRect.Width, h);
                Color gColor = Color.Lerp(new Color(25, 15, 10), new Color(50, 30, 20), t);
                spriteBatch.Draw(pixel, gRect, gColor * (alpha * 0.4f));
            }

            //绘制边框
            float pulse = (float)Math.Sin(pulseTimer * 2.5f) * 0.5f + 0.5f;
            Color edgeColor = Color.Lerp(new Color(255, 120, 40), new Color(255, 180, 100), pulse) * alpha;

            int border = 4;
            spriteBatch.Draw(pixel, new Rectangle(panelRect.X, panelRect.Y, panelRect.Width, border), edgeColor);
            spriteBatch.Draw(pixel, new Rectangle(panelRect.X, panelRect.Bottom - border, panelRect.Width, border), edgeColor * 0.8f);
            spriteBatch.Draw(pixel, new Rectangle(panelRect.X, panelRect.Y, border, panelRect.Height), edgeColor * 0.9f);
            spriteBatch.Draw(pixel, new Rectangle(panelRect.Right - border, panelRect.Y, border, panelRect.Height), edgeColor * 0.9f);

            //绘制内容
            DrawDetailContent(spriteBatch, node, panelRect, alpha);
        }

        private void DrawDetailContent(SpriteBatch spriteBatch, QuestNode node, Rectangle panelRect, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            int padding = 20;
            int currentY = panelRect.Y + padding;

            //绘制任务标题
            Vector2 titlePos = new Vector2(panelRect.X + padding, currentY);
            Color titleColor = node.IsCompleted ? new Color(140, 255, 160) : new Color(255, 200, 140);
            Utils.DrawBorderString(spriteBatch, node.Name, titlePos, titleColor * alpha, 1.2f);
            currentY += (int)(FontAssets.MouseText.Value.MeasureString(node.Name).Y * 1.2f) + 10;

            //绘制分隔线
            Rectangle divider = new Rectangle(panelRect.X + padding, currentY, panelRect.Width - padding * 2, 2);
            spriteBatch.Draw(pixel, divider, new Color(255, 140, 60) * (alpha * 0.6f));
            currentY += 15;

            //绘制任务描述
            string description = string.IsNullOrEmpty(node.DetailedDescription) ? node.Description : node.DetailedDescription;
            Vector2 descPos = new Vector2(panelRect.X + padding, currentY);
            Utils.DrawBorderString(spriteBatch, description, descPos, Color.White * alpha, 0.85f);
            currentY += (int)(FontAssets.MouseText.Value.MeasureString(description).Y * 0.85f) + 20;

            //绘制任务目标
            if (node.Objectives != null && node.Objectives.Count > 0) {
                Utils.DrawBorderString(spriteBatch, "任务目标:", new Vector2(panelRect.X + padding, currentY),
                    new Color(255, 200, 140) * alpha, 0.9f);
                currentY += 25;

                foreach (var objective in node.Objectives) {
                    string objText = $"• {objective.Description} ({objective.CurrentProgress}/{objective.RequiredProgress})";
                    Color objColor = objective.IsCompleted ? new Color(140, 255, 160) : Color.White;
                    Utils.DrawBorderString(spriteBatch, objText, new Vector2(panelRect.X + padding + 10, currentY),
                        objColor * alpha, 0.8f);
                    currentY += 22;
                }
                currentY += 10;
            }

            //绘制任务奖励
            if (node.Rewards != null && node.Rewards.Count > 0) {
                Utils.DrawBorderString(spriteBatch, "任务奖励:", new Vector2(panelRect.X + padding, currentY),
                    new Color(255, 200, 140) * alpha, 0.9f);
                currentY += 25;

                int rewardX = panelRect.X + padding + 10;
                foreach (var reward in node.Rewards) {
                    //绘制奖励物品图标(简化版本，这里可以根据实际情况扩展)
                    Rectangle rewardRect = new Rectangle(rewardX, currentY, 32, 32);
                    Color rewardColor = reward.Claimed ? new Color(100, 100, 110) : new Color(255, 200, 120);
                    spriteBatch.Draw(pixel, rewardRect, rewardColor * (alpha * 0.6f));

                    //绘制奖励数量
                    string amountText = $"x{reward.Amount}";
                    Vector2 amountPos = new Vector2(rewardX + 36, currentY + 8);
                    Utils.DrawBorderString(spriteBatch, amountText, amountPos, Color.White * alpha, 0.75f);

                    rewardX += 100;
                    if (rewardX > panelRect.Right - padding - 100) {
                        rewardX = panelRect.X + padding + 10;
                        currentY += 40;
                    }
                }
                currentY += 50;
            }

            //绘制领取按钮(如果任务已完成但未领取奖励)
            if (node.IsCompleted && node.Rewards != null && node.Rewards.Exists(r => !r.Claimed)) {
                Rectangle buttonRect = new Rectangle(panelRect.X + panelRect.Width / 2 - 60,
                    panelRect.Bottom - padding - 40, 120, 35);

                bool hoverButton = buttonRect.Contains(Main.MouseScreen.ToPoint());
                Color buttonColor = hoverButton ? new Color(255, 180, 100) : new Color(200, 120, 60);

                spriteBatch.Draw(pixel, buttonRect, buttonColor * alpha);

                //按钮边框
                int btnBorder = 2;
                Color btnEdge = new Color(255, 200, 120) * alpha;
                spriteBatch.Draw(pixel, new Rectangle(buttonRect.X, buttonRect.Y, buttonRect.Width, btnBorder), btnEdge);
                spriteBatch.Draw(pixel, new Rectangle(buttonRect.X, buttonRect.Bottom - btnBorder, buttonRect.Width, btnBorder), btnEdge);
                spriteBatch.Draw(pixel, new Rectangle(buttonRect.X, buttonRect.Y, btnBorder, buttonRect.Height), btnEdge);
                spriteBatch.Draw(pixel, new Rectangle(buttonRect.Right - btnBorder, buttonRect.Y, btnBorder, buttonRect.Height), btnEdge);

                //按钮文字
                string btnText = "领取奖励";
                Vector2 btnTextSize = FontAssets.MouseText.Value.MeasureString(btnText) * 0.85f;
                Vector2 btnTextPos = new Vector2(buttonRect.X + buttonRect.Width / 2, buttonRect.Y + buttonRect.Height / 2);
                Utils.DrawBorderString(spriteBatch, btnText, btnTextPos, Color.White * alpha, 0.85f, 0.5f, 0.5f);
            }
        }

        private void DrawQuestTypeIcon(SpriteBatch spriteBatch, QuestNode node, Vector2 center, float scale) {
            //根据任务类型绘制不同的标记
            string typeIcon = node.Type switch {
                QuestType.Main => "!",
                QuestType.Side => "?",
                QuestType.Daily => "D",
                QuestType.Achievement => "★",
                _ => "?"
            };

            Color iconColor = node.Type switch {
                QuestType.Main => new Color(255, 80, 80),
                QuestType.Side => new Color(80, 180, 255),
                QuestType.Daily => new Color(255, 200, 80),
                QuestType.Achievement => new Color(255, 220, 100),
                _ => Color.White
            };

            Vector2 iconPos = center - new Vector2(0, 8 * scale);
            Utils.DrawBorderString(spriteBatch, typeIcon, iconPos, iconColor, 0.9f * scale, 0.5f, 0.5f);
        }

        private void DrawCornerMark(SpriteBatch spriteBatch, Vector2 pos, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            float size = 7f;
            Color markColor = new Color(255, 150, 70) * alpha;

            //绘制十字形装饰
            spriteBatch.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), markColor, 0f,
                new Vector2(0.5f, 0.5f), new Vector2(size * 1.3f, size * 0.35f), SpriteEffects.None, 0f);
            spriteBatch.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), markColor * 0.85f, MathHelper.PiOver2,
                new Vector2(0.5f, 0.5f), new Vector2(size * 1.3f, size * 0.35f), SpriteEffects.None, 0f);

            //中心点
            spriteBatch.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), markColor * 0.7f, 0f,
                new Vector2(0.5f, 0.5f), new Vector2(size * 0.4f, size * 0.4f), SpriteEffects.None, 0f);
        }
    }
}
