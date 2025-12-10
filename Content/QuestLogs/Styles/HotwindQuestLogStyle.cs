using CalamityOverhaul.Content.QuestLogs.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.QuestLogs.Styles
{
    public class HotwindQuestLogStyle : IQuestLogStyle
    {
        //动画计时器
        private float flowTimer;
        private float pulseTimer;
        private float bloomTimer;

        public void UpdateStyle() {
            flowTimer += 0.025f;
            if (flowTimer > MathHelper.TwoPi) flowTimer -= MathHelper.TwoPi;
            pulseTimer += 0.025f;
            bloomTimer += 0.015f;
        }

        public void DrawBackground(SpriteBatch spriteBatch, QuestLog log, Rectangle panelRect) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //绘制深色阴影
            Rectangle shadowRect = panelRect;
            shadowRect.Offset(6, 6);
            spriteBatch.Draw(pixel, shadowRect, Color.Black * 0.6f * log.MainPanelAlpha);

            //绘制半透明黑色背景
            spriteBatch.Draw(pixel, panelRect, Color.Black * 0.85f * log.MainPanelAlpha);

            //绘制内部渐变效果
            int gradientSteps = 20;
            for (int i = 0; i < gradientSteps; i++) {
                float t = i / (float)gradientSteps;
                int y = panelRect.Y + (int)(t * panelRect.Height);
                int height = Math.Max(1, panelRect.Height / gradientSteps);
                Rectangle gradRect = new Rectangle(panelRect.X, y, panelRect.Width, height);
                Color gradColor = Color.Lerp(new Color(20, 10, 5), new Color(40, 20, 10), t);
                spriteBatch.Draw(pixel, gradRect, gradColor * 0.3f * log.MainPanelAlpha);
            }

            //绘制纵向渐变屏幕泛光动画
            DrawBloomEffect(spriteBatch, pixel, panelRect, log.MainPanelAlpha);

            //绘制脉冲光效
            float pulse = (float)Math.Sin(pulseTimer * 2f) * 0.5f + 0.5f;
            Color pulseColor = new Color(255, 140, 60) * (0.08f * pulse * log.MainPanelAlpha);
            spriteBatch.Draw(pixel, panelRect, pulseColor);

            //绘制边框
            int border = 3;
            Color edgeColor = Color.Lerp(new Color(255, 120, 40), new Color(255, 180, 100), pulse);
            spriteBatch.Draw(pixel, new Rectangle(panelRect.X, panelRect.Y, panelRect.Width, border), edgeColor * 0.95f * log.MainPanelAlpha);
            spriteBatch.Draw(pixel, new Rectangle(panelRect.X, panelRect.Bottom - border, panelRect.Width, border), edgeColor * 0.75f * log.MainPanelAlpha);
            spriteBatch.Draw(pixel, new Rectangle(panelRect.X, panelRect.Y, border, panelRect.Height), edgeColor * 0.85f * log.MainPanelAlpha);
            spriteBatch.Draw(pixel, new Rectangle(panelRect.Right - border, panelRect.Y, border, panelRect.Height), edgeColor * 0.85f * log.MainPanelAlpha);

            //绘制内边框发光
            Rectangle innerRect = panelRect;
            innerRect.Inflate(-6, -6);
            Color innerGlow = new Color(255, 140, 60) * (0.15f * pulse * log.MainPanelAlpha);
            spriteBatch.Draw(pixel, new Rectangle(innerRect.X, innerRect.Y, innerRect.Width, 1), innerGlow);
            spriteBatch.Draw(pixel, new Rectangle(innerRect.X, innerRect.Bottom - 1, innerRect.Width, 1), innerGlow * 0.7f);
            spriteBatch.Draw(pixel, new Rectangle(innerRect.X, innerRect.Y, 1, innerRect.Height), innerGlow * 0.85f);
            spriteBatch.Draw(pixel, new Rectangle(innerRect.Right - 1, innerRect.Y, 1, innerRect.Height), innerGlow * 0.85f);

            //绘制角落装饰
            DrawCornerMark(spriteBatch, new Vector2(panelRect.X + 12, panelRect.Y + 12), pulse, log.MainPanelAlpha);
            DrawCornerMark(spriteBatch, new Vector2(panelRect.Right - 12, panelRect.Y + 12), pulse, log.MainPanelAlpha);
            DrawCornerMark(spriteBatch, new Vector2(panelRect.X + 12, panelRect.Bottom - 12), pulse * 0.7f, log.MainPanelAlpha);
            DrawCornerMark(spriteBatch, new Vector2(panelRect.Right - 12, panelRect.Bottom - 12), pulse * 0.7f, log.MainPanelAlpha);
        }

        private void DrawBloomEffect(SpriteBatch spriteBatch, Texture2D pixel, Rectangle panelRect, float alphaMult) {
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
                    spriteBatch.Draw(pixel, bloomRect, finalColor * (alpha * layerAlpha * alphaMult));
                }
            }
        }

        public void DrawNode(SpriteBatch spriteBatch, QuestNode node, Vector2 drawPos, float scale, bool isHovered, float alpha) {
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
            spriteBatch.Draw(pixel, shadowRect, Color.Black * 0.5f * alpha);

            //绘制节点背景
            spriteBatch.Draw(pixel, nodeRect, baseColor * 0.7f * alpha);

            //绘制节点发光效果
            if (node.IsUnlocked || node.IsCompleted) {
                float glowPulse = (float)Math.Sin(Main.GameUpdateCount * 0.05f) * 0.5f + 0.5f;
                Color glowColor = node.IsCompleted ? new Color(100, 255, 120) : new Color(255, 180, 100);

                Rectangle glowRect = nodeRect;
                glowRect.Inflate(2, 2);
                spriteBatch.Draw(pixel, glowRect, glowColor * (0.3f * glowPulse * alpha));
            }

            //绘制任务图标
            DrawQuestIcon(spriteBatch, node, drawPos, scale, alpha);

            //绘制节点边框
            int borderWidth = 2;
            Color edgeColor = node.IsCompleted ? new Color(120, 255, 140) :
                             (node.IsUnlocked ? new Color(255, 160, 80) : new Color(120, 120, 130));

            if (isHovered) {
                edgeColor = Color.White;
                borderWidth = 3;
            }

            spriteBatch.Draw(pixel, new Rectangle(nodeRect.X, nodeRect.Y, nodeRect.Width, borderWidth), edgeColor * alpha);
            spriteBatch.Draw(pixel, new Rectangle(nodeRect.X, nodeRect.Bottom - borderWidth, nodeRect.Width, borderWidth), edgeColor * 0.8f * alpha);
            spriteBatch.Draw(pixel, new Rectangle(nodeRect.X, nodeRect.Y, borderWidth, nodeRect.Height), edgeColor * 0.9f * alpha);
            spriteBatch.Draw(pixel, new Rectangle(nodeRect.Right - borderWidth, nodeRect.Y, borderWidth, nodeRect.Height), edgeColor * 0.9f * alpha);

            Vector2 nameSize = FontAssets.MouseText.Value.MeasureString(node.DisplayName?.Value) * 0.75f;
            //绘制节点名称
            Vector2 namePos = new Vector2(drawPos.X, drawPos.Y + size / 2 + 8);

            Color textColor = node.IsCompleted ? new Color(140, 255, 160) :
                             (node.IsUnlocked ? new Color(255, 200, 140) : new Color(140, 140, 150));

            if (isHovered) {
                textColor = Color.White;
            }

            Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value, node.DisplayName?.Value,
                namePos.X, namePos.Y, textColor * alpha, Color.Black * alpha, nameSize / 2, 0.75f);
        }

        public void DrawConnection(SpriteBatch spriteBatch, Vector2 start, Vector2 end, bool isUnlocked, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 diff = end - start;
            float length = diff.Length();
            float rotation = diff.ToRotation();

            //加粗的连接线宽度
            int lineWidth = 8;

            //绘制外层阴影
            Color shadowColor = Color.Black * 0.4f;
            spriteBatch.Draw(pixel, start + new Vector2(2, 2).RotatedBy(rotation),
                new Rectangle(0, 0, (int)length, lineWidth), shadowColor * alpha, rotation,
                new Vector2(0, lineWidth / 2f), 1f, SpriteEffects.None, 0f);

            //绘制基础暗色背景层
            Color lineColor = isUnlocked ? new Color(60, 45, 30) : new Color(40, 40, 45);
            spriteBatch.Draw(pixel, start, new Rectangle(0, 0, (int)length, lineWidth),
                lineColor * 0.9f * alpha, rotation, new Vector2(0, lineWidth / 2f), 1f, SpriteEffects.None, 0f);

            if (isUnlocked) {
                //绘制主动流动的渐变动画
                DrawFlowingGradient(spriteBatch, pixel, start, end, length, rotation, lineWidth, alpha);

                //绘制外发光效果
                Color glowColor = new Color(255, 140, 60) * 0.3f;
                int glowWidth = lineWidth + 6;
                spriteBatch.Draw(pixel, start, new Rectangle(0, 0, (int)length, glowWidth),
                    glowColor * alpha, rotation, new Vector2(0, glowWidth / 2f), 1f, SpriteEffects.None, 0f);
            }
            else {
                //未解锁状态的暗淡虚线效果
                DrawDashedLine(spriteBatch, pixel, start, length, rotation, lineWidth, alpha);
            }
        }

        private void DrawFlowingGradient(SpriteBatch spriteBatch, Texture2D pixel, Vector2 start, Vector2 end, float length, float rotation, int lineWidth, float alpha) {
            //创建持续流动的渐变效果，从起点流向终点
            int segments = Math.Max((int)(length / 12f), 3);

            //流动偏移，确保是从0到1的连续运动
            float flowProgress = (flowTimer * 0.2f) % 1f;

            for (int i = 0; i < segments; i++) {
                float t = (float)i / segments;
                float dist = t * length;
                Vector2 pos = start + new Vector2(dist, 0).RotatedBy(rotation);

                // 计算流动亮度
                float wave = (float)Math.Sin((t - flowProgress) * MathHelper.TwoPi * 2f);
                float brightness = (wave * 0.5f + 0.5f);

                Color color = Color.Lerp(new Color(150, 80, 40), new Color(255, 180, 80), brightness);

                spriteBatch.Draw(pixel, pos, new Rectangle(0, 0, (int)(length / segments) + 1, lineWidth),
                    color * alpha, rotation, new Vector2(0, lineWidth / 2f), 1f, SpriteEffects.None, 0f);
            }

            //添加流动的能量脉冲点
            int pulseCount = Math.Max((int)(length / 60f), 2);
            for (int i = 0; i < pulseCount; i++) {
                float t = ((flowTimer * 0.5f + i * (1f / pulseCount)) % 1f);
                Vector2 pos = Vector2.Lerp(start, end, t);

                float size = 4f + (float)Math.Sin(flowTimer * 5f) * 2f;
                spriteBatch.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), Color.White * alpha, rotation,
                    new Vector2(0.5f, 0.5f), new Vector2(size * 2f, size), SpriteEffects.None, 0f);
            }
        }

        private void DrawDashedLine(SpriteBatch spriteBatch, Texture2D pixel, Vector2 start, float length, float rotation, int lineWidth, float alpha) {
            //绘制虚线效果表示未解锁
            int dashLength = 14;
            int gapLength = 10;
            int totalLength = dashLength + gapLength;
            int dashCount = (int)(length / totalLength);

            for (int i = 0; i < dashCount; i++) {
                float dashStart = i * totalLength;
                Vector2 dashPos = start + new Vector2(dashStart, 0).RotatedBy(rotation);

                Color dashColor = new Color(70, 70, 80) * 0.6f * alpha;
                spriteBatch.Draw(pixel, dashPos, new Rectangle(0, 0, dashLength, lineWidth),
                    dashColor, rotation, new Vector2(0, lineWidth / 2f), 1f, SpriteEffects.None, 0f);
            }
        }

        public Vector4 GetPadding() {
            return new Vector4(15, 35, 15, 15);
        }

        public Rectangle GetCloseButtonRect(Rectangle panelRect) {
            return new Rectangle(
                panelRect.Right - 40,
                panelRect.Y + 10,
                30,
                30
            );
        }

        public Rectangle GetRewardButtonRect(Rectangle panelRect) {
            int padding = 20;
            return new Rectangle(
                panelRect.X + panelRect.Width / 2 - 60,
                panelRect.Bottom - padding - 40,
                120,
                35
            );
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
            Utils.DrawBorderString(spriteBatch, node.DisplayName?.Value, titlePos, titleColor * alpha, 1.2f);
            currentY += (int)(FontAssets.MouseText.Value.MeasureString(node.DisplayName?.Value).Y * 1.2f) + 10;

            //绘制分隔线
            Rectangle divider = new Rectangle(panelRect.X + padding, currentY, panelRect.Width - padding * 2, 2);
            spriteBatch.Draw(pixel, divider, new Color(255, 140, 60) * (alpha * 0.6f));
            currentY += 15;

            //绘制任务描述
            string description = string.IsNullOrEmpty(node.DetailedDescription?.Value) ? node.Description?.Value : node.DetailedDescription?.Value;
            if (!string.IsNullOrEmpty(description)) {
                int maxTextWidth = panelRect.Width - padding * 2;
                string[] lines = Utils.WordwrapString(description, FontAssets.MouseText.Value, (int)(maxTextWidth / 0.85f), 99, out int lineCount);
                foreach (string line in lines) {
                    if (string.IsNullOrEmpty(line)) {
                        continue;
                    }
                    Utils.DrawBorderString(spriteBatch, line.TrimEnd('-', ' '), new Vector2(panelRect.X + padding, currentY), Color.White * alpha, 0.85f);
                    currentY += (int)(FontAssets.MouseText.Value.MeasureString(line).Y * 0.85f) + 4;
                }
                currentY += 10;
            }

            //绘制任务目标
            if (node.Objectives != null && node.Objectives.Count > 0) {
                Utils.DrawBorderString(spriteBatch, QuestLog.ObjectiveText.Value + ":", new Vector2(panelRect.X + padding, currentY),
                    new Color(255, 200, 140) * alpha, 0.9f);
                currentY += 25;

                foreach (var objective in node.Objectives) {
                    string objText = $"• {objective.Description} ({objective.CurrentProgress}/{objective.RequiredProgress})";
                    Color objColor = objective.IsCompleted ? new Color(140, 255, 160) : Color.White;
                    Utils.DrawBorderString(spriteBatch, objText, new Vector2(panelRect.X + padding + 10, currentY),
                        objColor * alpha, 0.8f);

                    //如果存在目标物品，绘制图标
                    if (objective.TargetItemID > 0) {
                        //计算图标位置(文本右侧)
                        Vector2 textSize = FontAssets.MouseText.Value.MeasureString(objText) * 0.8f;
                        Rectangle itemRect = new Rectangle(
                            (int)(panelRect.X + padding + 10 + textSize.X + 10),
                            currentY - 4,
                            24,
                            24
                        );

                        //绘制背景
                        spriteBatch.Draw(pixel, itemRect, new Color(0, 0, 0, 100) * alpha);

                        //绘制物品
                        Main.instance.LoadItem(objective.TargetItemID);
                        Texture2D itemTex = TextureAssets.Item[objective.TargetItemID].Value;
                        if (itemTex != null) {
                            Rectangle frame = Main.itemAnimations[objective.TargetItemID] != null
                                ? Main.itemAnimations[objective.TargetItemID].GetFrame(itemTex)
                                : itemTex.Frame();

                            float scale = 1f;
                            if (frame.Width > 20 || frame.Height > 20) {
                                scale = 20f / Math.Max(frame.Width, frame.Height);
                            }

                            Vector2 origin = frame.Size() / 2f;
                            Vector2 drawPos = itemRect.Center.ToVector2();
                            spriteBatch.Draw(itemTex, drawPos, frame, Color.White * alpha, 0f, origin, scale, SpriteEffects.None, 0f);
                        }

                        //悬停检测
                        if (itemRect.Contains(Main.MouseScreen.ToPoint()) && ContentSamples.ItemsByType.TryGetValue(objective.TargetItemID, out var item)) {
                            Main.HoverItem = item;
                            Main.hoverItemName = item.Name;
                        }
                    }

                    currentY += 22;
                }
                currentY += 10;
            }

            //绘制任务奖励
            if (node.Rewards != null && node.Rewards.Count > 0) {
                Utils.DrawBorderString(spriteBatch, QuestLog.RewardText.Value + ":", new Vector2(panelRect.X + padding, currentY),
                    new Color(255, 200, 140) * alpha, 0.9f);
                currentY += 25;

                int rewardX = panelRect.X + padding + 10;
                foreach (var reward in node.Rewards) {
                    //绘制奖励物品图标
                    Rectangle rewardRect = new Rectangle(rewardX, currentY, 32, 32);
                    Color rewardColor = reward.Claimed ? new Color(100, 100, 110) : new Color(255, 200, 120);

                    if (rewardRect.Contains(Main.MouseScreen.ToPoint()) && ContentSamples.ItemsByType.TryGetValue(reward.ItemType, out var item)) {
                        Main.HoverItem = item;
                        Main.hoverItemName = item.Name;
                    }

                    //绘制背景框
                    spriteBatch.Draw(pixel, rewardRect, rewardColor * (alpha * 0.3f));

                    //绘制真实物品图标
                    Main.instance.LoadItem(reward.ItemType);
                    Texture2D itemTexture = TextureAssets.Item[reward.ItemType].Value;
                    if (itemTexture != null) {
                        Rectangle frame = Main.itemAnimations[reward.ItemType] != null
                            ? Main.itemAnimations[reward.ItemType].GetFrame(itemTexture)
                            : itemTexture.Frame();

                        float scale = 1f;
                        if (frame.Width > 32 || frame.Height > 32) {
                            scale = 32f / Math.Max(frame.Width, frame.Height);
                        }

                        Vector2 itemPos = new Vector2(rewardRect.X + 16, rewardRect.Y + 16);
                        Vector2 origin = frame.Size() / 2f;

                        spriteBatch.Draw(itemTexture, itemPos, frame, Color.White * alpha, 0f, origin, scale, SpriteEffects.None, 0f);
                    }

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
                Rectangle buttonRect = GetRewardButtonRect(panelRect);

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
                string btnText = QuestLog.ReceiveAwardText.Value;
                Vector2 btnTextSize = FontAssets.MouseText.Value.MeasureString(btnText) * 0.85f;
                Vector2 btnTextPos = new Vector2(buttonRect.X + buttonRect.Width / 2, buttonRect.Y + buttonRect.Height / 2);
                Utils.DrawBorderString(spriteBatch, btnText, btnTextPos, Color.White * alpha, 0.85f, 0.5f, 0.5f);
            }
        }

        public void DrawProgressBar(SpriteBatch spriteBatch, QuestLog log, Rectangle panelRect) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            float alpha = log.MainPanelAlpha;

            //计算进度
            int total = 0;
            int completed = 0;
            foreach (var node in QuestNode.AllQuests) {
                total++;
                if (node.IsCompleted) completed++;
            }
            float progress = total > 0 ? (float)completed / total : 0f;

            //进度条区域
            int barHeight = log.ShowProgressBar ? 24 : 8;
            int barWidth = panelRect.Width - 40;
            Rectangle barRect = new Rectangle(panelRect.X + 20, panelRect.Bottom + 10, barWidth, barHeight);

            //绘制背景
            spriteBatch.Draw(pixel, barRect, Color.Black * 0.7f * alpha);

            //绘制边框
            Color borderColor = new Color(255, 140, 60) * alpha;
            int border = 2;
            spriteBatch.Draw(pixel, new Rectangle(barRect.X, barRect.Y, barRect.Width, border), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(barRect.X, barRect.Bottom - border, barRect.Width, border), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(barRect.X, barRect.Y, border, barRect.Height), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(barRect.Right - border, barRect.Y, border, barRect.Height), borderColor);

            //绘制进度填充
            if (total > 0) {
                int fillWidth = (int)((barWidth - border * 2) * progress);
                Rectangle fillRect = new Rectangle(barRect.X + border, barRect.Y + border, fillWidth, barHeight - border * 2);

                //渐变填充
                Color fillColor = new Color(255, 180, 60) * 0.6f * alpha;
                spriteBatch.Draw(pixel, fillRect, fillColor);

                //流光效果
                float flow = (flowTimer * 2f) % 1f;
                int flowX = fillRect.X + (int)(flow * fillRect.Width);
                if (flowX < fillRect.Right) {
                    spriteBatch.Draw(pixel, new Rectangle(flowX, fillRect.Y, 2, fillRect.Height), Color.White * 0.5f * alpha);
                }
            }

            if (log.ShowProgressBar) {
                //绘制文字
                string text = $"{QuestLog.ProgressText.Value}: {completed}/{total} ({(int)(progress * 100)}%)";
                Vector2 textSize = FontAssets.MouseText.Value.MeasureString(text) * 0.8f;
                Vector2 textPos = new Vector2(
                    barRect.X + barRect.Width / 2 - textSize.X / 2,
                    barRect.Y + barRect.Height / 2 - textSize.Y / 2 + 2
                );
                Utils.DrawBorderString(spriteBatch, text, textPos, Color.White * alpha, 0.8f);
            }

            //绘制切换按钮(小箭头)
            Rectangle toggleRect = new Rectangle(barRect.Right + 5, barRect.Y + barHeight / 2 - 10, 20, 20);
            bool hoverToggle = toggleRect.Contains(Main.MouseScreen.ToPoint());
            Color toggleColor = hoverToggle ? new Color(255, 200, 100) : new Color(200, 150, 80);

            Utils.DrawBorderString(spriteBatch, log.ShowProgressBar ? "▲" : "▼", toggleRect.TopLeft(), toggleColor * alpha, 1f);

            //处理点击
            if (hoverToggle) {
                Main.LocalPlayer.mouseInterface = true;
                if (Main.mouseLeft && Main.mouseLeftRelease) {
                    log.ShowProgressBar = !log.ShowProgressBar;
                    SoundEngine.PlaySound(SoundID.MenuTick);
                }
            }
        }

        private void DrawQuestIcon(SpriteBatch spriteBatch, QuestNode node, Vector2 center, float scale, float alpha) {
            Texture2D iconTexture = node.GetIconTexture();
            if (iconTexture == null) return;

            Rectangle? sourceRect = node.GetIconSourceRect(iconTexture);
            if (!sourceRect.HasValue) return;

            //计算图标绘制区域(节点内部，留出边距)
            int iconSize = (int)(40 * scale);
            Rectangle iconDrawRect = new Rectangle(
                (int)(center.X - iconSize / 2),
                (int)(center.Y - iconSize / 2),
                iconSize,
                iconSize
            );

            //计算缩放以适应图标区域
            float iconScale = 1f;
            Rectangle frame = sourceRect.Value;
            if (frame.Width > iconSize || frame.Height > iconSize) {
                iconScale = iconSize / (float)Math.Max(frame.Width, frame.Height);
            }

            //确定颜色(未解锁时变暗)
            Color iconColor = node.IsUnlocked ? Color.White : new Color(100, 100, 110);

            //已完成时添加绿色调
            if (node.IsCompleted) {
                iconColor = new Color(200, 255, 200);
            }

            //绘制图标
            Vector2 iconPos = new Vector2(iconDrawRect.X + iconDrawRect.Width / 2, iconDrawRect.Y + iconDrawRect.Height / 2);
            Vector2 origin = frame.Size() / 2f;

            spriteBatch.Draw(iconTexture, iconPos, frame, iconColor * alpha, 0f, origin, iconScale, SpriteEffects.None, 0f);
        }

        private void DrawCornerMark(SpriteBatch spriteBatch, Vector2 pos, float pulse, float alphaMult) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            float size = 7f;
            Color markColor = new Color(255, 150, 70) * (pulse * alphaMult);

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
