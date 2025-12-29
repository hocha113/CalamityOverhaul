using CalamityOverhaul.Content.QuestLogs.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.QuestLogs.Styles
{
    public class DraedonQuestLogStyle : IQuestLogStyle
    {
        //动画计时器
        private float scanLineTimer;
        private float hologramFlicker;
        private float circuitPulseTimer;
        private float hexGridPhase;
        private float globalTime;

        //简单的粒子系统
        private struct TechParticle
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Life;
            public float MaxLife;
            public float Scale;
        }
        private readonly List<TechParticle> particles = [];

        public void UpdateStyle() {
            //更新计时器
            scanLineTimer += 0.04f;
            hologramFlicker += 0.1f;
            circuitPulseTimer += 0.03f;
            hexGridPhase += 0.01f;
            globalTime += 0.016f;

            if (scanLineTimer > MathHelper.TwoPi) scanLineTimer -= MathHelper.TwoPi;
            if (hologramFlicker > MathHelper.TwoPi) hologramFlicker -= MathHelper.TwoPi;
            if (circuitPulseTimer > MathHelper.TwoPi) circuitPulseTimer -= MathHelper.TwoPi;
            if (hexGridPhase > MathHelper.TwoPi) hexGridPhase -= MathHelper.TwoPi;

            //更新粒子
            if (Main.rand.NextBool(10)) {
                particles.Add(new TechParticle {
                    Position = new Vector2(Main.rand.NextFloat(0, 100), Main.rand.NextFloat(0, 100)), //位置在绘制时相对计算
                    Velocity = new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(-0.5f, 0.5f)),
                    Life = 1f,
                    MaxLife = Main.rand.NextFloat(60f, 120f),
                    Scale = Main.rand.NextFloat(0.5f, 1.5f)
                });
            }

            for (int i = particles.Count - 1; i >= 0; i--) {
                var p = particles[i];
                p.Life -= 1f;
                p.Position += p.Velocity;
                particles[i] = p;
                if (p.Life <= 0) {
                    particles.RemoveAt(i);
                }
            }
        }

        public void DrawBackground(SpriteBatch spriteBatch, QuestLog log, Rectangle panelRect) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            bool nightMode = log.NightMode;
            float alpha = log.MainPanelAlpha;

            //基础色调定义
            Color baseDark = nightMode ? new Color(15, 5, 5) : new Color(5, 10, 15);
            Color baseMid = nightMode ? new Color(35, 10, 10) : new Color(10, 20, 35);
            Color accentColor = nightMode ? new Color(255, 60, 40) : new Color(40, 200, 255);
            Color gridColor = nightMode ? new Color(180, 50, 30) : new Color(30, 140, 180);

            //绘制背景底色
            spriteBatch.Draw(pixel, panelRect, baseDark * 0.9f * alpha);

            //绘制六角网格
            DrawHexGrid(spriteBatch, panelRect, alpha, gridColor);

            //绘制扫描线
            DrawScanLines(spriteBatch, panelRect, alpha, accentColor);

            //绘制科技边框
            float pulse = (float)Math.Sin(circuitPulseTimer) * 0.5f + 0.5f;
            DrawTechFrame(spriteBatch, panelRect, alpha, pulse, accentColor);

            //绘制装饰性粒子
            foreach (var p in particles) {
                //将相对坐标映射到面板区域
                Vector2 drawPos = new Vector2(
                    panelRect.X + (Math.Abs(p.Position.X * 13.5f) % panelRect.Width),
                    panelRect.Y + (Math.Abs(p.Position.Y * 13.5f) % panelRect.Height)
                );

                float lifeRatio = p.Life / p.MaxLife;
                float fade = (float)Math.Sin(lifeRatio * MathHelper.Pi);
                spriteBatch.Draw(pixel, drawPos, new Rectangle(0, 0, 1, 1), accentColor * fade * 0.6f * alpha, 0f, Vector2.Zero, p.Scale * 2f, SpriteEffects.None, 0f);
            }
        }

        public void DrawNode(SpriteBatch spriteBatch, QuestNode node, Vector2 drawPos, float scale, bool isHovered, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            bool isCompleted = node.IsCompleted;
            bool isUnlocked = node.IsUnlocked;

            //节点颜色
            Color nodeColor = isCompleted ? new Color(100, 255, 100) : (isUnlocked ? new Color(40, 200, 255) : new Color(80, 80, 90));
            if (isHovered) nodeColor = Color.Lerp(nodeColor, Color.White, 0.5f);

            float size = 40f * scale;
            Rectangle nodeRect = new Rectangle((int)(drawPos.X - size / 2), (int)(drawPos.Y - size / 2), (int)size, (int)size);

            //绘制菱形节点背景
            float rotation = MathHelper.PiOver4;
            if (isHovered) rotation += (float)Math.Sin(globalTime * 5f) * 0.1f;

            //外发光
            if (isUnlocked) {
                for (int i = 0; i < 4; i++) {
                    Vector2 offset = (rotation + i * MathHelper.PiOver2).ToRotationVector2() * 2f;
                    spriteBatch.Draw(pixel, drawPos + offset, new Rectangle(0, 0, 1, 1), nodeColor * 0.3f * alpha, rotation, new Vector2(0.5f, 0.5f), size * 1.2f, SpriteEffects.None, 0f);
                }
            }

            //主体
            spriteBatch.Draw(pixel, drawPos, new Rectangle(0, 0, 1, 1), nodeColor * alpha, rotation, new Vector2(0.5f, 0.5f), size, SpriteEffects.None, 0f);

            //内部黑色
            spriteBatch.Draw(pixel, drawPos, new Rectangle(0, 0, 1, 1), Color.Black * alpha, rotation, new Vector2(0.5f, 0.5f), size * 0.8f, SpriteEffects.None, 0f);

            //绘制任务图标
            DrawQuestIcon(spriteBatch, node, drawPos, scale, alpha);

            //绘制节点名称
            Vector2 nameSize = FontAssets.MouseText.Value.MeasureString(node.DisplayName?.Value) * 0.75f;
            Vector2 namePos = new Vector2(drawPos.X, drawPos.Y + size / 2 + 12);

            Color textColor = node.IsCompleted ? new Color(140, 255, 160) :
                             (node.IsUnlocked ? new Color(255, 200, 140) : new Color(140, 140, 150));

            if (isHovered) {
                textColor = Color.White;
            }

            Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value, node.DisplayName?.Value,
                namePos.X, namePos.Y, textColor * alpha, Color.Black * alpha, nameSize / 2, 0.75f);
        }

        private void DrawQuestIcon(SpriteBatch spriteBatch, QuestNode node, Vector2 center, float scale, float alpha) {
            Texture2D iconTexture = node.GetIconTexture();
            if (iconTexture == null) return;

            Rectangle? sourceRect = node.GetIconSourceRect(iconTexture);
            if (!sourceRect.HasValue) return;

            //计算图标绘制区域(节点内部，留出边距)
            int iconSize = (int)(32 * scale);
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

            //为了适应菱形背景，稍微旋转一点或者保持原样
            spriteBatch.Draw(iconTexture, iconPos, frame, iconColor * alpha, 0f, origin, iconScale, SpriteEffects.None, 0f);
        }

        public void DrawConnection(SpriteBatch spriteBatch, Vector2 start, Vector2 end, bool isUnlocked, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Color lineColor = isUnlocked ? new Color(40, 200, 255) : new Color(60, 70, 80);
            float width = isUnlocked ? 3f : 2f;

            //绘制简单的直线连接，带一点脉冲效果
            Vector2 edge = end - start;
            float length = edge.Length();
            float rotation = (float)Math.Atan2(edge.Y, edge.X);

            spriteBatch.Draw(pixel, start, new Rectangle(0, 0, 1, 1), lineColor * alpha, rotation, new Vector2(0, 0.5f), new Vector2(length, width), SpriteEffects.None, 0f);

            //如果是解锁状态，绘制流动的光点
            if (isUnlocked) {
                float flow = (globalTime * 2f) % 1f;
                Vector2 flowPos = Vector2.Lerp(start, end, flow);
                spriteBatch.Draw(pixel, flowPos, new Rectangle(0, 0, 1, 1), Color.White * alpha, rotation, new Vector2(0.5f, 0.5f), new Vector2(10f, width * 2f), SpriteEffects.None, 0f);
            }
        }

        public Vector4 GetPadding() {
            return new Vector4(20, 20, 20, 20);
        }

        public void DrawQuestDetail(SpriteBatch spriteBatch, QuestNode node, Rectangle panelRect, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //详情面板背景
            spriteBatch.Draw(pixel, panelRect, new Color(10, 15, 25) * 0.95f * alpha);

            //详情面板边框
            Color borderColor = new Color(40, 200, 255) * alpha;
            int border = 2;
            spriteBatch.Draw(pixel, new Rectangle(panelRect.X, panelRect.Y, panelRect.Width, border), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(panelRect.X, panelRect.Bottom - border, panelRect.Width, border), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(panelRect.X, panelRect.Y, border, panelRect.Height), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(panelRect.Right - border, panelRect.Y, border, panelRect.Height), borderColor);

            //标题背景条
            Rectangle titleRect = new Rectangle(panelRect.X, panelRect.Y, panelRect.Width, 40);
            spriteBatch.Draw(pixel, titleRect, borderColor * 0.2f);

            //绘制详细内容
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
            spriteBatch.Draw(pixel, divider, new Color(40, 200, 255) * (alpha * 0.6f));
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

        public Rectangle GetCloseButtonRect(Rectangle panelRect) {
            return new Rectangle(panelRect.Right - 30, panelRect.Y + 5, 25, 25);
        }

        public Rectangle GetRewardButtonRect(Rectangle panelRect) {
            return new Rectangle(panelRect.Right - 120, panelRect.Bottom - 40, 100, 30);
        }

        public void DrawProgressBar(SpriteBatch spriteBatch, QuestLog log, Rectangle panelRect) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            bool nightMode = log.NightMode;
            Color barColor = nightMode ? new Color(255, 80, 60) : new Color(60, 220, 255);

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

            //背景槽
            spriteBatch.Draw(pixel, barRect, Color.Black * 0.6f * log.MainPanelAlpha);

            //进度条
            Rectangle fillRect = new Rectangle(barRect.X, barRect.Y, (int)(barRect.Width * progress), barRect.Height);
            spriteBatch.Draw(pixel, fillRect, barColor * 0.8f * log.MainPanelAlpha);

            //高光流动
            float shinePos = (globalTime * 300f) % barRect.Width;
            if (shinePos < fillRect.Width) {
                spriteBatch.Draw(pixel, new Rectangle(barRect.X + (int)shinePos, barRect.Y, 5, barRect.Height), Color.White * 0.5f * log.MainPanelAlpha);
            }

            //边框
            DrawTechBorder(spriteBatch, barRect, barColor * log.MainPanelAlpha, 2);

            if (log.ShowProgressBar) {
                //绘制文字
                string text = $"{QuestLog.ProgressText.Value}: {completed}/{total} ({(int)(progress * 100)}%)";
                Vector2 textSize = FontAssets.MouseText.Value.MeasureString(text) * 0.8f;
                Vector2 textPos = new Vector2(
                    barRect.X + barRect.Width / 2 - textSize.X / 2,
                    barRect.Y + barRect.Height / 2 - textSize.Y / 2 + 2
                );
                Utils.DrawBorderString(spriteBatch, text, textPos, Color.White * log.MainPanelAlpha, 0.8f);
            }

            //绘制切换按钮(小箭头)
            Rectangle toggleRect = new Rectangle(barRect.Right + 5, barRect.Y + barHeight / 2 - 10, 20, 20);
            bool hoverToggle = toggleRect.Contains(Main.MouseScreen.ToPoint());
            Color toggleColor = hoverToggle ? new Color(255, 200, 100) : new Color(200, 150, 80);

            Utils.DrawBorderString(spriteBatch, log.ShowProgressBar ? "▲" : "▼", toggleRect.TopLeft(), toggleColor * log.MainPanelAlpha, 1f);

            //处理点击
            if (hoverToggle) {
                Main.LocalPlayer.mouseInterface = true;
                if (Main.mouseLeft && Main.mouseLeftRelease) {
                    log.ShowProgressBar = !log.ShowProgressBar;
                    SoundEngine.PlaySound(SoundID.MenuTick);
                }
            }
        }

        public Rectangle GetClaimAllButtonRect(Rectangle panelRect) {
            return new Rectangle(
                panelRect.X + panelRect.Width / 2 - 70,
                panelRect.Bottom + 40,
                140,
                35
            );
        }

        public void DrawClaimAllButton(SpriteBatch spriteBatch, Rectangle panelRect, bool isHovered, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle buttonRect = GetClaimAllButtonRect(panelRect);
            Color btnColor = isHovered ? new Color(80, 255, 100) : new Color(40, 200, 60);

            //按钮背景
            spriteBatch.Draw(pixel, buttonRect, btnColor * 0.2f * alpha);
            DrawTechBorder(spriteBatch, buttonRect, btnColor * alpha, 2);

            //文字
            string text = QuestLog.QuickReceiveAwardText.Value;
            Vector2 size = FontAssets.MouseText.Value.MeasureString(text) * 0.85f;
            Vector2 pos = new Vector2(buttonRect.X + (buttonRect.Width - size.X) / 2, buttonRect.Y + (buttonRect.Height - size.Y) / 2);
            Utils.DrawBorderString(spriteBatch, text, pos, btnColor * alpha, 0.85f);
        }

        public Rectangle GetResetViewButtonRect(Rectangle panelRect) {
            return new Rectangle(
                panelRect.Right - 45,
                panelRect.Bottom - 48,
                36,
                36
            );
        }

        public void DrawResetViewButton(SpriteBatch spriteBatch, Rectangle panelRect, Vector2 directionToCenter, bool isHovered, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle buttonRect = GetResetViewButtonRect(panelRect);
            Color btnColor = isHovered ? Color.White : new Color(100, 200, 255);

            //圆形背景
            spriteBatch.Draw(pixel, buttonRect, Color.Black * 0.5f * alpha);
            DrawTechBorder(spriteBatch, buttonRect, btnColor * alpha, 2);

            //画雷达指针
            Vector2 center = buttonRect.Center.ToVector2();
            float rotation = directionToCenter.ToRotation();

            //绘制雷达扫描线
            spriteBatch.Draw(pixel, center, new Rectangle(0, 0, 1, 1), btnColor * alpha, rotation, new Vector2(0, 0.5f), new Vector2(12, 2), SpriteEffects.None, 0f);
            //绘制中心点
            spriteBatch.Draw(pixel, center, new Rectangle(0, 0, 1, 1), btnColor * alpha, 0f, new Vector2(0.5f, 0.5f), 4f, SpriteEffects.None, 0f);
        }

        public Rectangle GetStyleSwitchButtonRect(Rectangle panelRect) {
            return new Rectangle(
                panelRect.X + 15,
                panelRect.Bottom - 45,
                30,
                30
            );
        }

        public void DrawStyleSwitchButton(SpriteBatch spriteBatch, Rectangle panelRect, bool isHovered, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle buttonRect = GetStyleSwitchButtonRect(panelRect);
            Color btnColor = isHovered ? Color.White : new Color(200, 100, 255);

            spriteBatch.Draw(pixel, buttonRect, Color.Black * 0.5f * alpha);
            DrawTechBorder(spriteBatch, buttonRect, btnColor * alpha, 2);

            //画个数据板图标
            Rectangle iconRect = buttonRect;
            iconRect.Inflate(-6, -6);

            //绘制数据板外框
            spriteBatch.Draw(pixel, iconRect, btnColor * 0.3f * alpha);
            //绘制几行数据线
            for (int i = 0; i < 3; i++) {
                spriteBatch.Draw(pixel, new Rectangle(iconRect.X + 2, iconRect.Y + 4 + i * 5, iconRect.Width - 4, 2), btnColor * 0.8f * alpha);
            }
        }

        public Rectangle GetNightModeButtonRect(Rectangle panelRect) {
            return new Rectangle(
                panelRect.X + 55,
                panelRect.Bottom - 45,
                30,
                30
            );
        }

        public void DrawNightModeButton(SpriteBatch spriteBatch, Rectangle panelRect, bool isHovered, float alpha, bool isNightMode) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle buttonRect = GetNightModeButtonRect(panelRect);
            Color btnColor = isHovered ? Color.White : (isNightMode ? new Color(255, 200, 50) : new Color(100, 100, 100));

            spriteBatch.Draw(pixel, buttonRect, Color.Black * 0.5f * alpha);
            DrawTechBorder(spriteBatch, buttonRect, btnColor * alpha, 2);

            //画个太阳/月亮示意
            Rectangle iconRect = buttonRect;
            iconRect.Inflate(-8, -8);

            if (isNightMode) {
                //月亮 (简单的C形)
                spriteBatch.Draw(pixel, iconRect, btnColor * alpha);
                spriteBatch.Draw(pixel, new Rectangle(iconRect.X + 4, iconRect.Y - 2, iconRect.Width, iconRect.Height), Color.Black * 0.8f * alpha);
            }
            else {
                //太阳 (简单的圆形)
                spriteBatch.Draw(pixel, iconRect, btnColor * alpha);
                //太阳光芒
                if (isHovered) {
                    spriteBatch.Draw(pixel, iconRect, btnColor * 0.3f * alpha);
                }
            }
        }

        #region 辅助绘制方法

        private void DrawHexGrid(SpriteBatch sb, Rectangle rect, float alpha, Color color) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            int hexRows = 10;
            float hexHeight = rect.Height / (float)hexRows;

            for (int row = 0; row < hexRows; row++) {
                float t = row / (float)hexRows;
                float y = rect.Y + row * hexHeight;
                float phase = hexGridPhase + t * MathHelper.Pi;
                float brightness = (float)Math.Sin(phase) * 0.5f + 0.5f;

                Color gridColor = color * (alpha * 0.1f * brightness);
                sb.Draw(pixel, new Rectangle(rect.X + 10, (int)y, rect.Width - 20, 1), gridColor);
            }
        }

        private void DrawScanLines(SpriteBatch sb, Rectangle rect, float alpha, Color color) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            float scanY = rect.Y + (float)Math.Sin(scanLineTimer) * 0.5f * rect.Height + rect.Height * 0.5f;

            for (int i = -2; i <= 2; i++) {
                float offsetY = scanY + i * 3f;
                if (offsetY < rect.Y || offsetY > rect.Bottom) continue;

                float intensity = 1f - Math.Abs(i) * 0.3f;
                Color scanColor = color * (alpha * 0.2f * intensity);
                sb.Draw(pixel, new Rectangle(rect.X + 8, (int)offsetY, rect.Width - 16, 2), scanColor);
            }
        }

        private void DrawTechFrame(SpriteBatch sb, Rectangle rect, float alpha, float pulse, Color color) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Color techEdge = color * (alpha * 0.8f);

            //外框
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 3), techEdge);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 3, rect.Width, 3), techEdge * 0.75f);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, 3, rect.Height), techEdge * 0.9f);
            sb.Draw(pixel, new Rectangle(rect.Right - 3, rect.Y, 3, rect.Height), techEdge * 0.9f);

            //内框
            Rectangle inner = rect;
            inner.Inflate(-6, -6);
            Color innerC = color * (alpha * 0.3f * pulse);
            sb.Draw(pixel, new Rectangle(inner.X, inner.Y, inner.Width, 1), innerC);
            sb.Draw(pixel, new Rectangle(inner.X, inner.Bottom - 1, inner.Width, 1), innerC * 0.7f);
            sb.Draw(pixel, new Rectangle(inner.X, inner.Y, 1, inner.Height), innerC * 0.9f);
            sb.Draw(pixel, new Rectangle(inner.Right - 1, inner.Y, 1, inner.Height), innerC * 0.9f);

            //角落装饰
            DrawCornerCircuit(sb, new Vector2(rect.X + 12, rect.Y + 12), alpha, color);
            DrawCornerCircuit(sb, new Vector2(rect.Right - 12, rect.Y + 12), alpha, color);
            DrawCornerCircuit(sb, new Vector2(rect.X + 12, rect.Bottom - 12), alpha * 0.7f, color);
            DrawCornerCircuit(sb, new Vector2(rect.Right - 12, rect.Bottom - 12), alpha * 0.7f, color);
        }

        private void DrawCornerCircuit(SpriteBatch sb, Vector2 pos, float alpha, Color color) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            float size = 8f;
            Color c = color * alpha;

            sb.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), c, 0f, new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.2f), SpriteEffects.None, 0f);
            sb.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), c * 0.85f, MathHelper.PiOver2, new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.2f), SpriteEffects.None, 0f);
        }

        private void DrawTechBorder(SpriteBatch sb, Rectangle rect, Color color, int thickness) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
            sb.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
        }

        #endregion
    }
}
