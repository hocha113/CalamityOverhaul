using CalamityOverhaul.Content.QuestLogs.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.QuestLogs.Styles
{
    public class ForestQuestLogStyle : IQuestLogStyle
    {
        //动画计时器
        private float magicTimer;
        private float leafTimer;
        private float runeTimer;
        private float glowTimer;

        public void UpdateStyle() {
            magicTimer += 0.02f;
            if (magicTimer > MathHelper.TwoPi) magicTimer -= MathHelper.TwoPi;
            leafTimer += 0.015f;
            runeTimer += 0.03f;
            glowTimer += 0.025f;
        }

        public void DrawBackground(SpriteBatch spriteBatch, QuestLog log, Rectangle panelRect) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            bool nightMode = log.NightMode;

            //绘制深色阴影
            Rectangle shadowRect = panelRect;
            shadowRect.Offset(5, 5);
            spriteBatch.Draw(pixel, shadowRect, Color.Black * 0.5f * log.MainPanelAlpha);

            //绘制主背景(羊皮纸质感)
            Color bgColor = nightMode ? new Color(15, 20, 15) : new Color(45, 40, 30);
            spriteBatch.Draw(pixel, panelRect, bgColor * 0.9f * log.MainPanelAlpha);

            //绘制纸张纹理效果
            DrawPaperTexture(spriteBatch, pixel, panelRect, log.MainPanelAlpha, nightMode);

            //绘制魔法光晕效果
            DrawMagicAura(spriteBatch, pixel, panelRect, log.MainPanelAlpha, nightMode);

            //绘制装饰性边框
            DrawDecorativeBorder(spriteBatch, pixel, panelRect, log.MainPanelAlpha, nightMode);

            //绘制四个角落的魔法符文
            DrawCornerRunes(spriteBatch, pixel, panelRect, log.MainPanelAlpha, nightMode);

            //绘制飘落的魔法粒子
            DrawFloatingParticles(spriteBatch, pixel, panelRect, log.MainPanelAlpha, nightMode);
        }

        private void DrawPaperTexture(SpriteBatch spriteBatch, Texture2D pixel, Rectangle panelRect, float alphaMult, bool nightMode) {
            //创建羊皮纸的褶皱纹理效果
            Color baseColor = nightMode ? new Color(25, 35, 25) : new Color(60, 55, 40);
            Color darkColor = nightMode ? new Color(15, 20, 15) : new Color(40, 35, 25);

            int stripeCount = 30;
            for (int i = 0; i < stripeCount; i++) {
                float t = i / (float)stripeCount;
                float noise = (float)Math.Sin(t * 50f + magicTimer * 0.5f) * 0.3f + 0.7f;
                int y = panelRect.Y + (int)(t * panelRect.Height);
                int h = Math.Max(1, panelRect.Height / stripeCount);

                Color color = Color.Lerp(darkColor, baseColor, noise);
                spriteBatch.Draw(pixel, new Rectangle(panelRect.X, y, panelRect.Width, h), color * 0.3f * alphaMult);
            }
        }

        private void DrawMagicAura(SpriteBatch spriteBatch, Texture2D pixel, Rectangle panelRect, float alphaMult, bool nightMode) {
            //绘制魔法光环从中心向外扩散
            Vector2 center = panelRect.Center.ToVector2();
            float pulse = (float)Math.Sin(glowTimer * 1.5f) * 0.5f + 0.5f;

            Color auraColor = nightMode ? new Color(100, 200, 150) : new Color(150, 200, 100);
            
            //绘制多层光环
            for (int layer = 0; layer < 3; layer++) {
                float layerOffset = layer * 0.33f;
                float layerPulse = (float)Math.Sin(glowTimer * 1.5f + layerOffset * MathHelper.TwoPi) * 0.5f + 0.5f;
                
                int radius = 50 + layer * 30;
                int segments = 32;
                
                for (int i = 0; i < segments; i++) {
                    float angle = (i / (float)segments) * MathHelper.TwoPi + magicTimer * (0.3f + layer * 0.1f);
                    float nextAngle = ((i + 1) / (float)segments) * MathHelper.TwoPi + magicTimer * (0.3f + layer * 0.1f);
                    
                    Vector2 pos1 = center + new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * radius;
                    Vector2 pos2 = center + new Vector2((float)Math.Cos(nextAngle), (float)Math.Sin(nextAngle)) * radius;
                    
                    //确保在面板内
                    if (panelRect.Contains(pos1.ToPoint()) || panelRect.Contains(pos2.ToPoint())) {
                        float alpha = layerPulse * 0.1f * alphaMult;
                        spriteBatch.Draw(pixel, pos1, new Rectangle(0, 0, 2, 2), auraColor * alpha, 0f, Vector2.One, 1f, SpriteEffects.None, 0f);
                    }
                }
            }
        }

        private void DrawDecorativeBorder(SpriteBatch spriteBatch, Texture2D pixel, Rectangle panelRect, float alphaMult, bool nightMode) {
            //绘制森林主题的装饰边框
            float pulse = (float)Math.Sin(glowTimer * 2f) * 0.5f + 0.5f;
            
            Color outerColor = nightMode ? new Color(60, 120, 80) : new Color(100, 150, 80);
            Color innerColor = nightMode ? new Color(100, 180, 120) : new Color(150, 200, 100);
            Color edgeColor = Color.Lerp(outerColor, innerColor, pulse) * alphaMult;

            //外边框(较粗)
            int outerBorder = 5;
            spriteBatch.Draw(pixel, new Rectangle(panelRect.X, panelRect.Y, panelRect.Width, outerBorder), edgeColor * 0.9f);
            spriteBatch.Draw(pixel, new Rectangle(panelRect.X, panelRect.Bottom - outerBorder, panelRect.Width, outerBorder), edgeColor * 0.7f);
            spriteBatch.Draw(pixel, new Rectangle(panelRect.X, panelRect.Y, outerBorder, panelRect.Height), edgeColor * 0.8f);
            spriteBatch.Draw(pixel, new Rectangle(panelRect.Right - outerBorder, panelRect.Y, outerBorder, panelRect.Height), edgeColor * 0.8f);

            //内边框(细线，发光)
            Rectangle innerRect = panelRect;
            innerRect.Inflate(-8, -8);
            Color glowColor = nightMode ? new Color(150, 255, 180) : new Color(180, 255, 150);
            glowColor *= pulse * alphaMult;

            spriteBatch.Draw(pixel, new Rectangle(innerRect.X, innerRect.Y, innerRect.Width, 2), glowColor);
            spriteBatch.Draw(pixel, new Rectangle(innerRect.X, innerRect.Bottom - 2, innerRect.Width, 2), glowColor * 0.8f);
            spriteBatch.Draw(pixel, new Rectangle(innerRect.X, innerRect.Y, 2, innerRect.Height), glowColor * 0.9f);
            spriteBatch.Draw(pixel, new Rectangle(innerRect.Right - 2, innerRect.Y, 2, innerRect.Height), glowColor * 0.9f);

            //装饰性藤蔓纹样
            DrawVinePattern(spriteBatch, pixel, panelRect, alphaMult, nightMode);
        }

        private void DrawVinePattern(SpriteBatch spriteBatch, Texture2D pixel, Rectangle panelRect, float alphaMult, bool nightMode) {
            //在边框上绘制藤蔓图案
            Color vineColor = nightMode ? new Color(80, 140, 100) : new Color(100, 160, 80);
            vineColor *= alphaMult;

            //顶部藤蔓
            int vineCount = 15;
            for (int i = 0; i < vineCount; i++) {
                float t = i / (float)vineCount;
                int x = panelRect.X + (int)(t * panelRect.Width);
                float wave = (float)Math.Sin(t * MathHelper.TwoPi * 3f + leafTimer) * 3f;
                
                for (int j = 0; j < 5; j++) {
                    int y = panelRect.Y + 2 + j * 2;
                    int offsetX = (int)(wave * (j / 5f));
                    spriteBatch.Draw(pixel, new Rectangle(x + offsetX, y, 2, 2), vineColor * (1f - j / 5f));
                }
            }
        }

        private void DrawCornerRunes(SpriteBatch spriteBatch, Texture2D pixel, Rectangle panelRect, float alphaMult, bool nightMode) {
            //在四个角落绘制神秘符文
            float pulse = (float)Math.Sin(runeTimer) * 0.5f + 0.5f;
            Color runeColor = nightMode ? new Color(120, 220, 160) : new Color(160, 240, 140);
            runeColor *= (0.6f + pulse * 0.4f) * alphaMult;

            int runeSize = 20;
            int offset = 15;

            //左上角
            DrawRune(spriteBatch, pixel, new Vector2(panelRect.X + offset, panelRect.Y + offset), runeSize, runeColor, runeTimer);
            //右上角
            DrawRune(spriteBatch, pixel, new Vector2(panelRect.Right - offset, panelRect.Y + offset), runeSize, runeColor, runeTimer + MathHelper.PiOver2);
            //左下角
            DrawRune(spriteBatch, pixel, new Vector2(panelRect.X + offset, panelRect.Bottom - offset), runeSize, runeColor, runeTimer + MathHelper.Pi);
            //右下角
            DrawRune(spriteBatch, pixel, new Vector2(panelRect.Right - offset, panelRect.Bottom - offset), runeSize, runeColor, runeTimer + MathHelper.Pi * 1.5f);
        }

        private void DrawRune(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, float size, Color color, float rotation) {
            //绘制六芒星符文
            int points = 6;
            for (int i = 0; i < points; i++) {
                float angle1 = (i / (float)points) * MathHelper.TwoPi + rotation;
                float angle2 = ((i + 2) / (float)points) * MathHelper.TwoPi + rotation;
                
                Vector2 p1 = center + new Vector2((float)Math.Cos(angle1), (float)Math.Sin(angle1)) * size;
                Vector2 p2 = center + new Vector2((float)Math.Cos(angle2), (float)Math.Sin(angle2)) * size;
                
                //绘制连接线
                Vector2 diff = p2 - p1;
                float length = diff.Length();
                float lineRotation = diff.ToRotation();
                
                spriteBatch.Draw(pixel, p1, new Rectangle(0, 0, (int)length, 2), color, lineRotation, Vector2.Zero, 1f, SpriteEffects.None, 0f);
            }

            //中心点
            spriteBatch.Draw(pixel, center, new Rectangle(0, 0, 6, 6), color * 1.2f, 0f, new Vector2(3, 3), 1f, SpriteEffects.None, 0f);
        }

        private void DrawFloatingParticles(SpriteBatch spriteBatch, Texture2D pixel, Rectangle panelRect, float alphaMult, bool nightMode) {
            //绘制漂浮的魔法粒子
            Color particleColor = nightMode ? new Color(150, 255, 200) : new Color(180, 255, 150);
            
            int particleCount = 20;
            for (int i = 0; i < particleCount; i++) {
                float offset = i * 0.314f;
                float t = (magicTimer + offset) % MathHelper.TwoPi;
                
                float x = panelRect.X + 20 + (float)Math.Sin(t * 2f + i) * (panelRect.Width - 40) * 0.5f + (panelRect.Width - 40) * 0.5f;
                float y = panelRect.Y + ((t / MathHelper.TwoPi) * (panelRect.Height - 40)) + 20;
                
                float alpha = (float)Math.Sin(t) * 0.5f + 0.5f;
                float size = 2f + (float)Math.Sin(t * 3f) * 1f;
                
                spriteBatch.Draw(pixel, new Vector2(x, y), new Rectangle(0, 0, 1, 1), particleColor * alpha * alphaMult * 0.4f, 0f, Vector2.Zero, size, SpriteEffects.None, 0f);
            }
        }

        public void DrawNode(SpriteBatch spriteBatch, QuestNode node, Vector2 drawPos, float scale, bool isHovered, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            int size = (int)(50 * scale);

            //根据任务状态确定颜色
            Color baseColor = node.IsCompleted ? new Color(100, 200, 120) :
                             (node.IsUnlocked ? new Color(200, 180, 100) : new Color(80, 80, 90));

            if (isHovered) {
                baseColor = Color.Lerp(baseColor, Color.White, 0.5f);
            }

            //绘制六边形节点背景
            DrawHexagon(spriteBatch, pixel, drawPos, size * 0.5f, baseColor * 0.8f * alpha);

            //绘制节点发光效果
            if (node.IsUnlocked || node.IsCompleted) {
                float glowPulse = (float)Math.Sin(Main.GameUpdateCount * 0.04f) * 0.5f + 0.5f;
                Color glowColor = node.IsCompleted ? new Color(120, 255, 150) : new Color(255, 220, 120);
                
                DrawHexagon(spriteBatch, pixel, drawPos, size * 0.55f, glowColor * (0.4f * glowPulse * alpha));
            }

            //绘制任务图标
            DrawQuestIcon(spriteBatch, node, drawPos, scale, alpha);

            //绘制六边形边框
            DrawHexagonBorder(spriteBatch, pixel, drawPos, size * 0.5f, baseColor * 1.2f * alpha, isHovered ? 3 : 2);

            //绘制节点名称
            Vector2 nameSize = FontAssets.MouseText.Value.MeasureString(node.DisplayName?.Value) * 0.7f;
            Vector2 namePos = new Vector2(drawPos.X, drawPos.Y + size / 2 + 10);

            Color textColor = node.IsCompleted ? new Color(150, 255, 180) :
                             (node.IsUnlocked ? new Color(255, 220, 150) : new Color(120, 120, 130));

            if (isHovered) {
                textColor = Color.White;
            }

            Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value, node.DisplayName?.Value,
                namePos.X, namePos.Y, textColor * alpha, Color.Black * alpha, nameSize / 2, 0.7f);
        }

        private void DrawHexagon(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, float radius, Color color) {
            //绘制填充的六边形
            int sides = 6;
            for (int i = 0; i < sides; i++) {
                float angle1 = (i / (float)sides) * MathHelper.TwoPi - MathHelper.PiOver2;
                float angle2 = ((i + 1) / (float)sides) * MathHelper.TwoPi - MathHelper.PiOver2;
                
                Vector2 p1 = center + new Vector2((float)Math.Cos(angle1), (float)Math.Sin(angle1)) * radius;
                Vector2 p2 = center + new Vector2((float)Math.Cos(angle2), (float)Math.Sin(angle2)) * radius;
                
                //绘制从中心到边缘的三角形
                Vector2 diff = p2 - p1;
                float length = diff.Length();
                float rotation = diff.ToRotation();
                
                Rectangle rect = new Rectangle(0, 0, (int)length, (int)radius);
                spriteBatch.Draw(pixel, p1, rect, color, rotation, Vector2.Zero, 1f, SpriteEffects.None, 0f);
            }
        }

        private void DrawHexagonBorder(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, float radius, Color color, int thickness) {
            //绘制六边形边框
            int sides = 6;
            for (int i = 0; i < sides; i++) {
                float angle1 = (i / (float)sides) * MathHelper.TwoPi - MathHelper.PiOver2;
                float angle2 = ((i + 1) / (float)sides) * MathHelper.TwoPi - MathHelper.PiOver2;
                
                Vector2 p1 = center + new Vector2((float)Math.Cos(angle1), (float)Math.Sin(angle1)) * radius;
                Vector2 p2 = center + new Vector2((float)Math.Cos(angle2), (float)Math.Sin(angle2)) * radius;
                
                Vector2 diff = p2 - p1;
                float length = diff.Length();
                float rotation = diff.ToRotation();
                
                spriteBatch.Draw(pixel, p1, new Rectangle(0, 0, (int)length, thickness), color, rotation, new Vector2(0, thickness / 2f), 1f, SpriteEffects.None, 0f);
            }
        }

        public void DrawConnection(SpriteBatch spriteBatch, Vector2 start, Vector2 end, bool isUnlocked, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 diff = end - start;
            float length = diff.Length();
            float rotation = diff.ToRotation();

            int lineWidth = 6;

            //绘制阴影
            spriteBatch.Draw(pixel, start + new Vector2(2, 2).RotatedBy(rotation),
                new Rectangle(0, 0, (int)length, lineWidth), Color.Black * 0.3f * alpha, rotation,
                new Vector2(0, lineWidth / 2f), 1f, SpriteEffects.None, 0f);

            if (isUnlocked) {
                //解锁状态绘制魔法藤蔓
                DrawMagicVine(spriteBatch, pixel, start, end, length, rotation, lineWidth, alpha);
            }
            else {
                //未解锁状态绘制虚线
                DrawDottedLine(spriteBatch, pixel, start, length, rotation, lineWidth, alpha);
            }
        }

        private void DrawMagicVine(SpriteBatch spriteBatch, Texture2D pixel, Vector2 start, Vector2 end, float length, float rotation, int lineWidth, float alpha) {
            //绘制基础藤蔓
            Color vineColor = new Color(80, 120, 70);
            spriteBatch.Draw(pixel, start, new Rectangle(0, 0, (int)length, lineWidth),
                vineColor * 0.8f * alpha, rotation, new Vector2(0, lineWidth / 2f), 1f, SpriteEffects.None, 0f);

            //绘制流动的魔法能量
            int segments = Math.Max((int)(length / 15f), 3);
            float flowProgress = (magicTimer * 0.3f) % 1f;

            for (int i = 0; i < segments; i++) {
                float t = (float)i / segments;
                float dist = t * length;
                Vector2 pos = start + new Vector2(dist, 0).RotatedBy(rotation);

                float wave = (float)Math.Sin((t - flowProgress) * MathHelper.TwoPi * 2f);
                float brightness = (wave * 0.5f + 0.5f);

                Color color = Color.Lerp(new Color(100, 180, 100), new Color(180, 255, 150), brightness);

                spriteBatch.Draw(pixel, pos, new Rectangle(0, 0, (int)(length / segments) + 1, lineWidth),
                    color * alpha * 0.7f, rotation, new Vector2(0, lineWidth / 2f), 1f, SpriteEffects.None, 0f);
            }

            //绘制魔法光点
            int particleCount = Math.Max((int)(length / 50f), 2);
            for (int i = 0; i < particleCount; i++) {
                float t = ((magicTimer * 0.4f + i * (1f / particleCount)) % 1f);
                Vector2 pos = Vector2.Lerp(start, end, t);

                float size = 3f + (float)Math.Sin(magicTimer * 4f) * 1.5f;
                Color particleColor = new Color(180, 255, 150);
                spriteBatch.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), particleColor * alpha, rotation,
                    new Vector2(0.5f, 0.5f), new Vector2(size, size), SpriteEffects.None, 0f);
            }
        }

        private void DrawDottedLine(SpriteBatch spriteBatch, Texture2D pixel, Vector2 start, float length, float rotation, int lineWidth, float alpha) {
            //绘制虚线
            int dotLength = 10;
            int gapLength = 8;
            int totalLength = dotLength + gapLength;
            int dotCount = (int)(length / totalLength);

            Color dotColor = new Color(60, 60, 70) * 0.5f * alpha;

            for (int i = 0; i < dotCount; i++) {
                float dotStart = i * totalLength;
                Vector2 dotPos = start + new Vector2(dotStart, 0).RotatedBy(rotation);

                spriteBatch.Draw(pixel, dotPos, new Rectangle(0, 0, dotLength, lineWidth),
                    dotColor, rotation, new Vector2(0, lineWidth / 2f), 1f, SpriteEffects.None, 0f);
            }
        }

        public Vector4 GetPadding() {
            return new Vector4(20, 40, 20, 20);
        }

        public Rectangle GetCloseButtonRect(Rectangle panelRect) {
            return new Rectangle(
                panelRect.Right - 45,
                panelRect.Y + 12,
                33,
                33
            );
        }

        public Rectangle GetRewardButtonRect(Rectangle panelRect) {
            int padding = 20;
            return new Rectangle(
                panelRect.X + panelRect.Width / 2 - 70,
                panelRect.Bottom - padding - 45,
                140,
                38
            );
        }

        public void DrawQuestDetail(SpriteBatch spriteBatch, QuestNode node, Rectangle panelRect, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //绘制半透明背景遮罩
            Rectangle fullScreen = new Rectangle(0, 0, Main.screenWidth, Main.screenHeight);
            spriteBatch.Draw(pixel, fullScreen, Color.Black * (0.65f * alpha));

            //绘制详情面板阴影
            Rectangle shadowRect = panelRect;
            shadowRect.Offset(6, 6);
            spriteBatch.Draw(pixel, shadowRect, Color.Black * (0.6f * alpha));

            //绘制面板背景
            spriteBatch.Draw(pixel, panelRect, new Color(35, 30, 20) * alpha);

            //绘制羊皮纸纹理
            DrawPaperTexture(spriteBatch, pixel, panelRect, alpha, false);

            //绘制魔法边框
            DrawDetailPanelBorder(spriteBatch, pixel, panelRect, alpha);

            //绘制内容
            DrawDetailContent(spriteBatch, node, panelRect, alpha);
        }

        private void DrawDetailPanelBorder(SpriteBatch spriteBatch, Texture2D pixel, Rectangle panelRect, float alpha) {
            float pulse = (float)Math.Sin(glowTimer * 2f) * 0.5f + 0.5f;
            
            Color outerColor = new Color(80, 140, 90);
            Color innerColor = new Color(120, 200, 130);
            Color edgeColor = Color.Lerp(outerColor, innerColor, pulse) * alpha;

            int border = 5;
            spriteBatch.Draw(pixel, new Rectangle(panelRect.X, panelRect.Y, panelRect.Width, border), edgeColor);
            spriteBatch.Draw(pixel, new Rectangle(panelRect.X, panelRect.Bottom - border, panelRect.Width, border), edgeColor * 0.8f);
            spriteBatch.Draw(pixel, new Rectangle(panelRect.X, panelRect.Y, border, panelRect.Height), edgeColor * 0.9f);
            spriteBatch.Draw(pixel, new Rectangle(panelRect.Right - border, panelRect.Y, border, panelRect.Height), edgeColor * 0.9f);

            //角落符文
            int runeOffset = 18;
            DrawRune(spriteBatch, pixel, new Vector2(panelRect.X + runeOffset, panelRect.Y + runeOffset), 15, edgeColor, runeTimer);
            DrawRune(spriteBatch, pixel, new Vector2(panelRect.Right - runeOffset, panelRect.Y + runeOffset), 15, edgeColor, runeTimer + MathHelper.PiOver2);
            DrawRune(spriteBatch, pixel, new Vector2(panelRect.X + runeOffset, panelRect.Bottom - runeOffset), 15, edgeColor, runeTimer + MathHelper.Pi);
            DrawRune(spriteBatch, pixel, new Vector2(panelRect.Right - runeOffset, panelRect.Bottom - runeOffset), 15, edgeColor, runeTimer + MathHelper.Pi * 1.5f);
        }

        private void DrawDetailContent(SpriteBatch spriteBatch, QuestNode node, Rectangle panelRect, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            int padding = 25;
            int currentY = panelRect.Y + padding;

            //绘制任务标题
            Vector2 titlePos = new Vector2(panelRect.X + padding, currentY);
            Color titleColor = node.IsCompleted ? new Color(150, 255, 180) : new Color(255, 220, 150);
            Utils.DrawBorderString(spriteBatch, node.DisplayName?.Value, titlePos, titleColor * alpha, 1.3f);
            currentY += (int)(FontAssets.MouseText.Value.MeasureString(node.DisplayName?.Value).Y * 1.3f) + 12;

            //绘制装饰性分隔线
            DrawDecorativeDivider(spriteBatch, pixel, panelRect.X + padding, currentY, panelRect.Width - padding * 2, alpha);
            currentY += 18;

            //绘制任务描述
            string description = string.IsNullOrEmpty(node.DetailedDescription?.Value) ? node.Description?.Value : node.DetailedDescription?.Value;
            if (!string.IsNullOrEmpty(description)) {
                int maxTextWidth = panelRect.Width - padding * 2;
                string[] lines = Utils.WordwrapString(description, FontAssets.MouseText.Value, (int)(maxTextWidth / 0.9f), 99, out int lineCount);
                foreach (string line in lines) {
                    if (string.IsNullOrEmpty(line)) {
                        continue;
                    }
                    Utils.DrawBorderString(spriteBatch, line.TrimEnd('-', ' '), new Vector2(panelRect.X + padding, currentY), Color.White * alpha, 0.9f);
                    currentY += (int)(FontAssets.MouseText.Value.MeasureString(line).Y * 0.9f) + 5;
                }
                currentY += 12;
            }

            //绘制任务目标
            if (node.Objectives != null && node.Objectives.Count > 0) {
                Utils.DrawBorderString(spriteBatch, QuestLog.ObjectiveText.Value + ":", new Vector2(panelRect.X + padding, currentY),
                    new Color(255, 220, 150) * alpha, 1f);
                currentY += 28;

                foreach (var objective in node.Objectives) {
                    string objText = $"• {objective.Description} ({objective.CurrentProgress}/{objective.RequiredProgress})";
                    Color objColor = objective.IsCompleted ? new Color(150, 255, 180) : Color.White;
                    Utils.DrawBorderString(spriteBatch, objText, new Vector2(panelRect.X + padding + 12, currentY),
                        objColor * alpha, 0.85f);

                    //如果存在目标物品绘制图标
                    if (objective.TargetItemID > 0) {
                        Vector2 textSize = FontAssets.MouseText.Value.MeasureString(objText) * 0.85f;
                        Rectangle itemRect = new Rectangle(
                            (int)(panelRect.X + padding + 12 + textSize.X + 10),
                            currentY - 4,
                            26,
                            26
                        );

                        spriteBatch.Draw(pixel, itemRect, new Color(0, 0, 0, 120) * alpha);

                        Main.instance.LoadItem(objective.TargetItemID);
                        Texture2D itemTex = TextureAssets.Item[objective.TargetItemID].Value;
                        if (itemTex != null) {
                            Rectangle frame = Main.itemAnimations[objective.TargetItemID] != null
                                ? Main.itemAnimations[objective.TargetItemID].GetFrame(itemTex)
                                : itemTex.Frame();

                            float scale = 1f;
                            if (frame.Width > 22 || frame.Height > 22) {
                                scale = 22f / Math.Max(frame.Width, frame.Height);
                            }

                            Vector2 origin = frame.Size() / 2f;
                            Vector2 drawPos = itemRect.Center.ToVector2();
                            spriteBatch.Draw(itemTex, drawPos, frame, Color.White * alpha, 0f, origin, scale, SpriteEffects.None, 0f);
                        }

                        if (itemRect.Contains(Main.MouseScreen.ToPoint()) && ContentSamples.ItemsByType.TryGetValue(objective.TargetItemID, out var item)) {
                            Main.HoverItem = item;
                            Main.hoverItemName = item.Name;
                        }
                    }

                    currentY += 24;
                }
                currentY += 12;
            }

            //绘制任务奖励
            if (node.Rewards != null && node.Rewards.Count > 0) {
                Utils.DrawBorderString(spriteBatch, QuestLog.RewardText.Value + ":", new Vector2(panelRect.X + padding, currentY),
                    new Color(255, 220, 150) * alpha, 1f);
                currentY += 28;

                int rewardX = panelRect.X + padding + 12;
                foreach (var reward in node.Rewards) {
                    Rectangle rewardRect = new Rectangle(rewardX, currentY, 36, 36);
                    Color rewardColor = reward.Claimed ? new Color(80, 80, 90) : new Color(200, 255, 180);

                    if (rewardRect.Contains(Main.MouseScreen.ToPoint()) && ContentSamples.ItemsByType.TryGetValue(reward.ItemType, out var item)) {
                        Main.HoverItem = item;
                        Main.hoverItemName = item.Name;
                    }

                    spriteBatch.Draw(pixel, rewardRect, rewardColor * (alpha * 0.35f));

                    Main.instance.LoadItem(reward.ItemType);
                    Texture2D itemTexture = TextureAssets.Item[reward.ItemType].Value;
                    if (itemTexture != null) {
                        Rectangle frame = Main.itemAnimations[reward.ItemType] != null
                            ? Main.itemAnimations[reward.ItemType].GetFrame(itemTexture)
                            : itemTexture.Frame();

                        float scale = 1f;
                        if (frame.Width > 34 || frame.Height > 34) {
                            scale = 34f / Math.Max(frame.Width, frame.Height);
                        }

                        Vector2 itemPos = new Vector2(rewardRect.X + 18, rewardRect.Y + 18);
                        Vector2 origin = frame.Size() / 2f;

                        spriteBatch.Draw(itemTexture, itemPos, frame, Color.White * alpha, 0f, origin, scale, SpriteEffects.None, 0f);
                    }

                    string amountText = $"x{reward.Amount}";
                    Vector2 amountPos = new Vector2(rewardX + 40, currentY + 10);
                    Utils.DrawBorderString(spriteBatch, amountText, amountPos, Color.White * alpha, 0.8f);

                    rewardX += 105;
                    if (rewardX > panelRect.Right - padding - 105) {
                        rewardX = panelRect.X + padding + 12;
                        currentY += 44;
                    }
                }
                currentY += 55;
            }

            //绘制领取按钮
            if (node.IsCompleted && node.Rewards != null && node.Rewards.Exists(r => !r.Claimed)) {
                Rectangle buttonRect = GetRewardButtonRect(panelRect);

                bool hoverButton = buttonRect.Contains(Main.MouseScreen.ToPoint());
                DrawMagicButton(spriteBatch, pixel, buttonRect, hoverButton, alpha, QuestLog.ReceiveAwardText.Value);
            }
        }

        private void DrawDecorativeDivider(SpriteBatch spriteBatch, Texture2D pixel, int x, int y, int width, float alpha) {
            //绘制装饰性分隔线
            Color lineColor = new Color(100, 150, 100) * alpha;
            
            spriteBatch.Draw(pixel, new Rectangle(x, y, width, 2), lineColor * 0.6f);
            
            //中间装饰
            int centerX = x + width / 2;
            DrawRune(spriteBatch, pixel, new Vector2(centerX, y), 8, lineColor * 1.2f, runeTimer);
        }

        private void DrawMagicButton(SpriteBatch spriteBatch, Texture2D pixel, Rectangle buttonRect, bool isHovered, float alpha, string text) {
            float pulse = (float)Math.Sin(glowTimer * 2.5f) * 0.5f + 0.5f;
            
            Color bgColor1 = new Color(80, 140, 90);
            Color bgColor2 = new Color(120, 180, 130);
            
            if (isHovered) {
                bgColor1 = Color.Lerp(bgColor1, Color.White, 0.3f);
                bgColor2 = Color.Lerp(bgColor2, Color.White, 0.3f);
            }

            //渐变背景
            int steps = 8;
            for (int i = 0; i < steps; i++) {
                float t = i / (float)steps;
                int y = buttonRect.Y + (int)(t * buttonRect.Height);
                int h = Math.Max(1, buttonRect.Height / steps);
                Color c = Color.Lerp(bgColor1, bgColor2, t);
                spriteBatch.Draw(pixel, new Rectangle(buttonRect.X, y, buttonRect.Width, h), c * alpha * 0.85f);
            }

            //发光边框
            Color glowColor = Color.Lerp(new Color(120, 200, 140), new Color(180, 255, 200), pulse);
            if (isHovered) glowColor = Color.White;
            glowColor *= alpha;

            int border = 3;
            spriteBatch.Draw(pixel, new Rectangle(buttonRect.X, buttonRect.Y, buttonRect.Width, border), glowColor);
            spriteBatch.Draw(pixel, new Rectangle(buttonRect.X, buttonRect.Bottom - border, buttonRect.Width, border), glowColor * 0.8f);
            spriteBatch.Draw(pixel, new Rectangle(buttonRect.X, buttonRect.Y, border, buttonRect.Height), glowColor * 0.9f);
            spriteBatch.Draw(pixel, new Rectangle(buttonRect.Right - border, buttonRect.Y, border, buttonRect.Height), glowColor * 0.9f);

            //文字
            Vector2 textSize = FontAssets.MouseText.Value.MeasureString(text) * 0.9f;
            Vector2 textPos = new Vector2(buttonRect.X + buttonRect.Width / 2, buttonRect.Y + buttonRect.Height / 2);
            
            Color textColor = isHovered ? new Color(220, 255, 230) : Color.White;
            Utils.DrawBorderString(spriteBatch, text, textPos, textColor * alpha, 0.9f, 0.5f, 0.5f);
        }

        public void DrawProgressBar(SpriteBatch spriteBatch, QuestLog log, Rectangle panelRect) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            float alpha = log.MainPanelAlpha;
            bool nightMode = log.NightMode;

            //计算进度
            int total = 0;
            int completed = 0;
            foreach (var node in QuestNode.AllQuests) {
                total++;
                if (node.IsCompleted) completed++;
            }
            float progress = total > 0 ? (float)completed / total : 0f;

            //进度条区域
            int barHeight = log.ShowProgressBar ? 26 : 10;
            int barWidth = panelRect.Width - 40;
            Rectangle barRect = new Rectangle(panelRect.X + 20, panelRect.Bottom + 12, barWidth, barHeight);

            //绘制背景
            spriteBatch.Draw(pixel, barRect, new Color(20, 15, 10) * 0.8f * alpha);

            //绘制边框
            Color borderColor = nightMode ? new Color(80, 160, 120) : new Color(100, 180, 120);
            borderColor *= alpha;
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
                Color fillColor1 = nightMode ? new Color(100, 180, 140) : new Color(120, 200, 140);
                Color fillColor2 = nightMode ? new Color(140, 220, 180) : new Color(160, 240, 180);
                
                int gradSteps = 68;
                for (int i = 0; i < gradSteps; i++) {
                    float t = i / (float)gradSteps;
                    int y = fillRect.Y + (int)(t * fillRect.Height);
                    int h = Math.Max(1, fillRect.Height / gradSteps);
                    Color c = Color.Lerp(fillColor1, fillColor2, t);
                    spriteBatch.Draw(pixel, new Rectangle(fillRect.X, y, fillRect.Width, h), c * alpha * 0.7f);
                }

                //魔法流光效果
                float flow = (magicTimer * 1.5f) % 1f;
                int flowX = fillRect.X + (int)(flow * fillRect.Width);
                if (flowX < fillRect.Right) {
                    Color flowColor = new Color(200, 255, 220);
                    spriteBatch.Draw(pixel, new Rectangle(flowX - 1, fillRect.Y, 3, fillRect.Height), flowColor * 0.6f * alpha);
                }
            }

            if (log.ShowProgressBar) {
                //绘制文字
                string text = $"{QuestLog.ProgressText.Value}: {completed}/{total} ({(int)(progress * 100)}%)";
                Vector2 textSize = FontAssets.MouseText.Value.MeasureString(text) * 0.85f;
                Vector2 textPos = new Vector2(
                    barRect.X + barRect.Width / 2 - textSize.X / 2,
                    barRect.Y + barRect.Height / 2 - textSize.Y / 2 + 2
                );
                Utils.DrawBorderString(spriteBatch, text, textPos, Color.White * alpha, 0.85f);
            }

            //绘制切换按钮
            Rectangle toggleRect = new Rectangle(barRect.Right + 6, barRect.Y + barHeight / 2 - 12, 24, 24);
            bool hoverToggle = toggleRect.Contains(Main.MouseScreen.ToPoint());
            Color toggleColor = hoverToggle ? new Color(220, 255, 200) : new Color(180, 220, 160);

            Utils.DrawBorderString(spriteBatch, log.ShowProgressBar ? "▲" : "▼", toggleRect.TopLeft(), toggleColor * alpha, 1.1f);

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

            int iconSize = (int)(42 * scale);
            Rectangle iconDrawRect = new Rectangle(
                (int)(center.X - iconSize / 2),
                (int)(center.Y - iconSize / 2),
                iconSize,
                iconSize
            );

            float iconScale = 1f;
            Rectangle frame = sourceRect.Value;
            if (frame.Width > iconSize || frame.Height > iconSize) {
                iconScale = iconSize / (float)Math.Max(frame.Width, frame.Height);
            }

            Color iconColor = node.IsUnlocked ? Color.White : new Color(80, 80, 90);

            if (node.IsCompleted) {
                iconColor = new Color(220, 255, 220);
            }

            Vector2 iconPos = new Vector2(iconDrawRect.X + iconDrawRect.Width / 2, iconDrawRect.Y + iconDrawRect.Height / 2);
            Vector2 origin = frame.Size() / 2f;

            spriteBatch.Draw(iconTexture, iconPos, frame, iconColor * alpha, 0f, origin, iconScale, SpriteEffects.None, 0f);
        }

        public Rectangle GetStyleSwitchButtonRect(Rectangle panelRect) {
            return new Rectangle(
                panelRect.X + 18,
                panelRect.Bottom - 50,
                34,
                34
            );
        }

        public void DrawStyleSwitchButton(SpriteBatch spriteBatch, Rectangle panelRect, bool isHovered, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle buttonRect = GetStyleSwitchButtonRect(panelRect);

            DrawHexagonalButton(spriteBatch, pixel, buttonRect, isHovered, alpha, "[i:149]");
        }

        public Rectangle GetNightModeButtonRect(Rectangle panelRect) {
            return new Rectangle(
                panelRect.X + 62,
                panelRect.Bottom - 50,
                34,
                34
            );
        }

        public void DrawNightModeButton(SpriteBatch spriteBatch, Rectangle panelRect, bool isHovered, float alpha, bool isNightMode) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle buttonRect = GetNightModeButtonRect(panelRect);
            Vector2 center = buttonRect.Center.ToVector2();

            DrawHexagonalButton(spriteBatch, pixel, buttonRect, isHovered, alpha, null);

            //绘制图标
            Color iconColor = isHovered ? Color.White : new Color(255, 255, 220);

            if (isNightMode) {
                //月亮
                spriteBatch.Draw(pixel, center, new Rectangle(0, 0, 14, 14), iconColor * alpha, 0f, new Vector2(7, 7), 1f, SpriteEffects.None, 0f);
                spriteBatch.Draw(pixel, center + new Vector2(5, -2), new Rectangle(0, 0, 12, 12), new Color(40, 60, 50) * alpha, 0f, new Vector2(6, 6), 1f, SpriteEffects.None, 0f);
            }
            else {
                //太阳
                spriteBatch.Draw(pixel, center, new Rectangle(0, 0, 10, 10), iconColor * alpha, 0f, new Vector2(5, 5), 1f, SpriteEffects.None, 0f);
                float time = Main.GameUpdateCount * 0.02f;
                for (int i = 0; i < 8; i++) {
                    float rot = i * MathHelper.PiOver4 + time;
                    Vector2 offset = new Vector2(0, -10).RotatedBy(rot);
                    spriteBatch.Draw(pixel, center + offset, new Rectangle(0, 0, 2, 5), iconColor * alpha, rot, new Vector2(1, 2.5f), 1f, SpriteEffects.None, 0f);
                }
            }
        }

        public Rectangle GetClaimAllButtonRect(Rectangle panelRect) {
            return new Rectangle(
                panelRect.X + panelRect.Width / 2 - 75,
                panelRect.Bottom + 45,
                150,
                40
            );
        }

        public void DrawClaimAllButton(SpriteBatch spriteBatch, Rectangle panelRect, bool isHovered, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle buttonRect = GetClaimAllButtonRect(panelRect);
            bool nightMode = QuestLog.Instance?.NightMode ?? false;

            float pulse = (float)Math.Sin(Main.GameUpdateCount * 0.08f) * 0.5f + 0.5f;

            //背景渐变
            Color colorTop, colorBottom;
            if (nightMode) {
                colorTop = isHovered ? new Color(100, 180, 140) : new Color(80, 160, 120);
                colorBottom = isHovered ? new Color(140, 220, 180) : new Color(120, 200, 160);
            }
            else {
                colorTop = isHovered ? new Color(120, 200, 140) : new Color(100, 180, 120);
                colorBottom = isHovered ? new Color(160, 240, 180) : new Color(140, 220, 160);
            }

            //渐变背景
            int steps = 10;
            for (int i = 0; i < steps; i++) {
                float t = i / (float)steps;
                int y = buttonRect.Y + (int)(t * buttonRect.Height);
                int h = Math.Max(1, buttonRect.Height / steps);
                Color c = Color.Lerp(colorTop, colorBottom, t);
                spriteBatch.Draw(pixel, new Rectangle(buttonRect.X, y, buttonRect.Width, h), c * alpha * 0.85f);
            }

            //发光边框
            Color glowColor;
            if (nightMode) {
                glowColor = Color.Lerp(new Color(140, 220, 180), new Color(180, 255, 220), pulse);
            }
            else {
                glowColor = Color.Lerp(new Color(160, 240, 180), new Color(200, 255, 220), pulse);
            }
            if (isHovered) glowColor = Color.White;
            glowColor *= alpha;

            int border = 3;
            spriteBatch.Draw(pixel, new Rectangle(buttonRect.X, buttonRect.Y, buttonRect.Width, border), glowColor);
            spriteBatch.Draw(pixel, new Rectangle(buttonRect.X, buttonRect.Bottom - border, buttonRect.Width, border), glowColor);
            spriteBatch.Draw(pixel, new Rectangle(buttonRect.X, buttonRect.Y, border, buttonRect.Height), glowColor);
            spriteBatch.Draw(pixel, new Rectangle(buttonRect.Right - border, buttonRect.Y, border, buttonRect.Height), glowColor);

            //角落装饰
            int cornerSize = 8;
            spriteBatch.Draw(pixel, new Rectangle(buttonRect.X, buttonRect.Y, cornerSize, border), glowColor);
            spriteBatch.Draw(pixel, new Rectangle(buttonRect.X, buttonRect.Y, border, cornerSize), glowColor);
            spriteBatch.Draw(pixel, new Rectangle(buttonRect.Right - cornerSize, buttonRect.Y, cornerSize, border), glowColor);
            spriteBatch.Draw(pixel, new Rectangle(buttonRect.Right - border, buttonRect.Y, border, cornerSize), glowColor);
            spriteBatch.Draw(pixel, new Rectangle(buttonRect.X, buttonRect.Bottom - border, cornerSize, border), glowColor);
            spriteBatch.Draw(pixel, new Rectangle(buttonRect.X, buttonRect.Bottom - cornerSize, border, cornerSize), glowColor);
            spriteBatch.Draw(pixel, new Rectangle(buttonRect.Right - cornerSize, buttonRect.Bottom - border, cornerSize, border), glowColor);
            spriteBatch.Draw(pixel, new Rectangle(buttonRect.Right - border, buttonRect.Bottom - cornerSize, border, cornerSize), glowColor);

            //文字
            string text = QuestLog.QuickReceiveAwardText.Value;
            Vector2 textPos = new Vector2(buttonRect.X + buttonRect.Width / 2, buttonRect.Y + buttonRect.Height / 2);

            Color textColor;
            if (nightMode) {
                textColor = isHovered ? new Color(220, 255, 230) : Color.White;
            }
            else {
                textColor = isHovered ? new Color(230, 255, 220) : Color.White;
            }

            Utils.DrawBorderString(spriteBatch, text, textPos, textColor * alpha, 0.9f, 0.5f, 0.5f);
        }

        public Rectangle GetResetViewButtonRect(Rectangle panelRect) {
            return new Rectangle(
                panelRect.Right - 50,
                panelRect.Bottom - 52,
                38,
                38
            );
        }

        public void DrawResetViewButton(SpriteBatch spriteBatch, Rectangle panelRect, Vector2 directionToCenter, bool isHovered, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle buttonRect = GetResetViewButtonRect(panelRect);
            Vector2 center = buttonRect.Center.ToVector2();

            DrawHexagonalButton(spriteBatch, pixel, buttonRect, isHovered, alpha, null);

            //绘制指南针装饰
            float time = Main.GameUpdateCount * 0.015f;
            for (int i = 0; i < 6; i++) {
                float rot = i * MathHelper.PiOver2 * 0.666f + time;
                Vector2 offset = new Vector2(0, -15).RotatedBy(rot);
                spriteBatch.Draw(pixel, center + offset, new Rectangle(0, 0, 2, 4), new Color(180, 220, 180) * 0.4f * alpha, rot, new Vector2(1, 2), 1f, SpriteEffects.None, 0f);
            }

            //箭头
            float rotation = directionToCenter.ToRotation();
            float arrowPulse = (float)Math.Sin(Main.GameUpdateCount * 0.12f) * 0.2f + 1f;

            Color arrowColor = isHovered ? Color.White : new Color(220, 255, 220);

            //绘制箭头杆
            spriteBatch.Draw(pixel, center, new Rectangle(0, 0, 14, 3), arrowColor * alpha, rotation, new Vector2(0, 1.5f), 1f, SpriteEffects.None, 0f);

            //绘制箭头头部
            float headSize = 7f * arrowPulse;
            Vector2 headPos = center + new Vector2(7, 0).RotatedBy(rotation);

            spriteBatch.Draw(pixel, headPos, new Rectangle(0, 0, (int)headSize, 2), arrowColor * alpha, rotation + MathHelper.Pi * 0.75f, new Vector2(0, 1), 1f, SpriteEffects.None, 0f);
            spriteBatch.Draw(pixel, headPos, new Rectangle(0, 0, (int)headSize, 2), arrowColor * alpha, rotation - MathHelper.Pi * 0.75f, new Vector2(0, 1), 1f, SpriteEffects.None, 0f);

            //中心点
            spriteBatch.Draw(pixel, center, new Rectangle(0, 0, 4, 4), new Color(255, 100, 100) * alpha, 0f, new Vector2(2, 2), 1f, SpriteEffects.None, 0f);
        }

        private void DrawHexagonalButton(SpriteBatch spriteBatch, Texture2D pixel, Rectangle buttonRect, bool isHovered, float alpha, string icon) {
            Vector2 center = buttonRect.Center.ToVector2();
            float radius = buttonRect.Width * 0.4f;

            //绘制阴影
            DrawHexagon(spriteBatch, pixel, center + new Vector2(2, 2), radius, Color.Black * 0.4f * alpha);

            //绘制主体
            Color bgColor = isHovered ? new Color(120, 180, 140) : new Color(80, 120, 90);
            DrawHexagon(spriteBatch, pixel, center, radius, bgColor * alpha);

            //绘制边框
            Color borderColor = isHovered ? Color.White : new Color(180, 220, 180);
            DrawHexagonBorder(spriteBatch, pixel, center, radius, borderColor * alpha, 2);

            //绘制图标文字
            if (!string.IsNullOrEmpty(icon)) {
                Vector2 iconPos = center - new Vector2(12, 12);
                Utils.DrawBorderString(spriteBatch, icon, iconPos, Color.White * alpha, 1f, 0.5f, 0.5f);
            }
        }
    }
}
