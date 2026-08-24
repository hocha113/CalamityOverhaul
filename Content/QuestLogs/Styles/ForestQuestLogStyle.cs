using CalamityOverhaul.Common;
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
    public class ForestQuestLogStyle : IQuestLogStyle
    {
        private float magicTimer;
        private float leafTimer;
        private float runeTimer;
        private float glowTimer;
        private float shaderTime;
        private const int EdgePad = 16;

        private struct LeafParticle
        {
            public Vector2 Pos;
            public Vector2 Vel;
            public float Life;
            public float MaxLife;
            public float Size;
            public float Rot;
            public int Type; //0=叶片 1=孢子 2=光尘
        }
        private readonly List<LeafParticle> leafParticles = [];

        public void UpdateStyle() {
            magicTimer += 0.02f;
            if (magicTimer > MathHelper.TwoPi) magicTimer -= MathHelper.TwoPi;
            leafTimer += 0.015f;
            runeTimer += 0.03f;
            glowTimer += 0.025f;
            shaderTime += 0.004f;
            if (shaderTime > 100f) shaderTime -= 100f;

            if (Main.rand.NextBool(5)) {
                leafParticles.Add(new LeafParticle {
                    Pos = new Vector2(Main.rand.NextFloat(0, 1f), Main.rand.NextFloat(0, 1f)),
                    Vel = new Vector2(Main.rand.NextFloat(0.0005f, 0.002f), Main.rand.NextFloat(0.001f, 0.003f)),
                    Life = 1f,
                    MaxLife = Main.rand.NextFloat(90f, 180f),
                    Size = Main.rand.NextFloat(1.5f, 3.5f),
                    Rot = Main.rand.NextFloat(0, MathHelper.TwoPi),
                    Type = Main.rand.Next(3)
                });
            }

            for (int i = leafParticles.Count - 1; i >= 0; i--) {
                var p = leafParticles[i];
                p.Life -= 1f;
                p.Pos += p.Vel;
                p.Rot += 0.02f;
                leafParticles[i] = p;
                if (p.Life <= 0) leafParticles.RemoveAt(i);
            }
        }

        #region 着色器面板背景

        private void DrawShaderPanel(SpriteBatch sb, Rectangle rect, float alpha, bool nightMode) {
            Texture2D px = VaultAsset.placeholder2.Value;

            if (EffectLoader.ForestPanel?.Value != null) {
                Effect effect = EffectLoader.ForestPanel.Value;
                Rectangle extRect = rect;
                extRect.Inflate(EdgePad, EdgePad);

                effect.Parameters["uTime"]?.SetValue(shaderTime);
                effect.Parameters["uAlpha"]?.SetValue(alpha * 0.97f);
                effect.Parameters["uResolution"]?.SetValue(new Vector2(extRect.Width, extRect.Height));
                effect.Parameters["uEdgePad"]?.SetValue((float)EdgePad);
                effect.Parameters["uNightMode"]?.SetValue(nightMode ? 1f : 0f);

                sb.End();
                sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                    SamplerState.AnisotropicClamp, DepthStencilState.None,
                    RasterizerState.CullNone, effect, Main.UIScaleMatrix);

                sb.Draw(px, extRect, Color.White);

                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                    SamplerState.AnisotropicClamp, DepthStencilState.None,
                    RasterizerState.CullNone, null, Main.UIScaleMatrix);
            }
            else {
                DrawFallbackBackground(sb, px, rect, alpha, nightMode);
            }
        }

        //降级背景
        private void DrawFallbackBackground(SpriteBatch sb, Texture2D px, Rectangle rect, float alpha, bool nightMode) {
            Color top = nightMode ? new Color(10, 20, 22) : new Color(22, 28, 12);
            Color bot = nightMode ? new Color(4, 10, 14) : new Color(10, 14, 5);

            int segs = 20;
            for (int i = 0; i < segs; i++) {
                float t = i / (float)segs;
                float t2 = (i + 1f) / segs;
                int y1 = rect.Y + (int)(t * rect.Height);
                int y2 = rect.Y + (int)(t2 * rect.Height);
                float noise = (float)Math.Sin(t * 50f + magicTimer * 0.5f) * 0.15f + 0.85f;
                Color c = Color.Lerp(top, bot, t) * noise;
                sb.Draw(px, new Rectangle(rect.X, y1, rect.Width, Math.Max(1, y2 - y1)), c * alpha * 0.9f);
            }

            for (int i = 0; i < 40; i++) {
                float t = i / 40f;
                int y = rect.Y + (int)(t * rect.Height);
                float scan = (float)Math.Sin(t * 80f + magicTimer) * 0.5f + 0.5f;
                sb.Draw(px, new Rectangle(rect.X, y, rect.Width, 1), Color.Black * alpha * scan * 0.08f);
            }

            int vSize = rect.Width / 3;
            sb.Draw(px, new Rectangle(rect.X, rect.Y, vSize, rect.Height), Color.Black * alpha * 0.15f);
            sb.Draw(px, new Rectangle(rect.Right - vSize, rect.Y, vSize, rect.Height), Color.Black * alpha * 0.15f);
        }

        #endregion

        public void DrawBackground(SpriteBatch spriteBatch, QuestLog log, Rectangle panelRect) {
            float alpha = log.MainPanelAlpha;
            bool nightMode = log.NightMode;
            Texture2D pixel = VaultAsset.placeholder2.Value;

            DrawShaderPanel(spriteBatch, panelRect, alpha, nightMode);

            foreach (var leaf in leafParticles) {
                float x = panelRect.X + leaf.Pos.X * panelRect.Width;
                float y = panelRect.Y + leaf.Pos.Y * panelRect.Height;
                if (!panelRect.Contains((int)x, (int)y)) continue;

                float fadeIn = Math.Min(1f, (leaf.MaxLife - leaf.Life) / 20f);
                float fadeOut = Math.Min(1f, leaf.Life / 20f);
                float a = fadeIn * fadeOut * alpha;

                Color c;
                if (leaf.Type == 0) c = nightMode ? new Color(80, 180, 140) : new Color(120, 200, 80);
                else if (leaf.Type == 1) c = nightMode ? new Color(100, 160, 200) : new Color(200, 220, 100);
                else c = nightMode ? new Color(140, 220, 180) : new Color(180, 255, 150);

                spriteBatch.Draw(pixel, new Vector2(x, y), new Rectangle(0, 0, 1, 1),
                    c * a * 0.5f, leaf.Rot, new Vector2(0.5f), leaf.Size, SpriteEffects.None, 0f);
            }

            float runePulse = (float)Math.Sin(runeTimer) * 0.5f + 0.5f;
            Color runeColor = nightMode ? new Color(80, 200, 160) : new Color(120, 200, 80);
            runeColor *= (0.4f + runePulse * 0.3f) * alpha;
            int runeOff = 22;

            DrawRuneNode(spriteBatch, pixel, new Vector2(panelRect.X + runeOff, panelRect.Y + runeOff), 14, runeColor, runeTimer);
            DrawRuneNode(spriteBatch, pixel, new Vector2(panelRect.Right - runeOff, panelRect.Y + runeOff), 14, runeColor, runeTimer + MathHelper.PiOver2);
            DrawRuneNode(spriteBatch, pixel, new Vector2(panelRect.X + runeOff, panelRect.Bottom - runeOff), 14, runeColor, runeTimer + MathHelper.Pi);
            DrawRuneNode(spriteBatch, pixel, new Vector2(panelRect.Right - runeOff, panelRect.Bottom - runeOff), 14, runeColor, runeTimer + MathHelper.Pi * 1.5f);

            Color vineColor = nightMode ? new Color(60, 140, 100) : new Color(80, 160, 80);
            vineColor *= alpha * 0.35f;
            int traceLen = 50;
            DrawVineTrace(spriteBatch, pixel, new Vector2(panelRect.X + runeOff, panelRect.Y + runeOff), traceLen, 0f, vineColor);
            DrawVineTrace(spriteBatch, pixel, new Vector2(panelRect.X + runeOff, panelRect.Y + runeOff), traceLen, MathHelper.PiOver2, vineColor);
            DrawVineTrace(spriteBatch, pixel, new Vector2(panelRect.Right - runeOff, panelRect.Y + runeOff), traceLen, MathHelper.PiOver2, vineColor);
            DrawVineTrace(spriteBatch, pixel, new Vector2(panelRect.Right - runeOff, panelRect.Y + runeOff), traceLen, MathHelper.Pi, vineColor);
            DrawVineTrace(spriteBatch, pixel, new Vector2(panelRect.X + runeOff, panelRect.Bottom - runeOff), traceLen, 0f, vineColor);
            DrawVineTrace(spriteBatch, pixel, new Vector2(panelRect.X + runeOff, panelRect.Bottom - runeOff), traceLen, -MathHelper.PiOver2, vineColor);
            DrawVineTrace(spriteBatch, pixel, new Vector2(panelRect.Right - runeOff, panelRect.Bottom - runeOff), traceLen, MathHelper.Pi, vineColor);
            DrawVineTrace(spriteBatch, pixel, new Vector2(panelRect.Right - runeOff, panelRect.Bottom - runeOff), traceLen, -MathHelper.PiOver2, vineColor);
        }

        public void DrawNode(SpriteBatch spriteBatch, QuestNode node, Vector2 drawPos, float scale, bool isHovered, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            int size = (int)(50 * scale);
            float radius = size * 0.5f;
            bool hasUnclaimed = node.HasUnclaimedRewards;

            Color baseColor;
            if (node.IsCompleted && !hasUnclaimed) {
                baseColor = new Color(100, 200, 120);
            }
            else if (hasUnclaimed) {
                baseColor = new Color(220, 195, 70);
            }
            else if (node.IsUnlocked) {
                baseColor = new Color(200, 180, 100);
            }
            else {
                baseColor = new Color(80, 80, 90);
            }

            if (isHovered) baseColor = Color.Lerp(baseColor, Color.White, 0.4f);

            if (node.IsUnlocked || node.IsCompleted) {
                float gp = (float)Math.Sin(glowTimer * 2f) * 0.5f + 0.5f;
                Color gc;
                if (node.IsCompleted && !hasUnclaimed) {
                    gc = new Color(120, 255, 150);
                }
                else if (hasUnclaimed) {
                    gc = new Color(255, 235, 90);
                    gp = gp * 0.6f + 0.4f; //待领取时辉光更强且更持续
                }
                else {
                    gc = new Color(255, 220, 120);
                }
                DrawHexagon(spriteBatch, pixel, drawPos, radius + 6, gc * (0.15f * gp * alpha));
                DrawHexagon(spriteBatch, pixel, drawPos, radius + 3, gc * (0.25f * gp * alpha));
            }

            DrawHexagon(spriteBatch, pixel, drawPos + new Vector2(3, 3), radius, Color.Black * 0.35f * alpha);

            DrawHexagon(spriteBatch, pixel, drawPos, radius, baseColor * 0.85f * alpha);

            Color highlight = Color.Lerp(baseColor, Color.White, 0.3f);
            DrawHexagon(spriteBatch, pixel, drawPos - new Vector2(1, 2), radius * 0.7f, highlight * 0.2f * alpha);

            DrawQuestIcon(spriteBatch, node, drawPos, scale, alpha);

            DrawHexagonBorder(spriteBatch, pixel, drawPos, radius, baseColor * 1.3f * alpha, isHovered ? 3 : 2);

            Color textColor;
            if (node.IsCompleted && !hasUnclaimed) {
                textColor = new Color(150, 255, 180);
            }
            else if (hasUnclaimed) {
                textColor = new Color(255, 235, 120);
            }
            else if (node.IsUnlocked) {
                textColor = new Color(255, 220, 150);
            }
            else {
                textColor = new Color(120, 120, 130);
            }
            if (isHovered) textColor = Color.White;

            Vector2 nameSize = FontAssets.MouseText.Value.MeasureString(node.DisplayName?.Value) * 0.7f;
            Vector2 namePos = new Vector2(drawPos.X, drawPos.Y + radius + 10);
            Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value, node.DisplayName?.Value,
                namePos.X, namePos.Y, textColor * alpha, Color.Black * alpha, nameSize / 2, 0.7f);
        }

        public void DrawConnection(SpriteBatch spriteBatch, Vector2 start, Vector2 end, bool isUnlocked, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 diff = end - start;
            float length = diff.Length();
            float rotation = diff.ToRotation();
            int lineWidth = 6;

            spriteBatch.Draw(pixel, start + new Vector2(2, 2).RotatedBy(rotation),
                new Rectangle(0, 0, (int)length, lineWidth), Color.Black * 0.3f * alpha,
                rotation, new Vector2(0, lineWidth / 2f), 1f, SpriteEffects.None, 0f);

            if (isUnlocked) {
                Color vineBase = new Color(60, 100, 55);
                spriteBatch.Draw(pixel, start, new Rectangle(0, 0, (int)length, lineWidth),
                    vineBase * 0.8f * alpha, rotation, new Vector2(0, lineWidth / 2f), 1f, SpriteEffects.None, 0f);

                int segs = Math.Max((int)(length / 12f), 3);
                float flow = (magicTimer * 0.35f) % 1f;
                for (int i = 0; i < segs; i++) {
                    float t = (float)i / segs;
                    float wave = (float)Math.Sin((t - flow) * MathHelper.TwoPi * 2f);
                    float bright = wave * 0.5f + 0.5f;
                    Color c = Color.Lerp(new Color(80, 160, 80), new Color(160, 255, 140), bright);

                    float segLen = length / segs + 1;
                    Vector2 segPos = start + new Vector2(t * length, 0).RotatedBy(rotation);
                    spriteBatch.Draw(pixel, segPos, new Rectangle(0, 0, (int)segLen, lineWidth),
                        c * alpha * 0.65f, rotation, new Vector2(0, lineWidth / 2f), 1f, SpriteEffects.None, 0f);
                }

                spriteBatch.Draw(pixel, start, new Rectangle(0, 0, (int)length, lineWidth + 4),
                    new Color(120, 255, 140) * 0.08f * alpha, rotation,
                    new Vector2(0, (lineWidth + 4) / 2f), 1f, SpriteEffects.None, 0f);

                int particleCount = Math.Max((int)(length / 45f), 2);
                for (int i = 0; i < particleCount; i++) {
                    float t = ((magicTimer * 0.4f + i * (1f / particleCount)) % 1f);
                    Vector2 pos = Vector2.Lerp(start, end, t);
                    float sz = 3f + (float)Math.Sin(magicTimer * 4f + i) * 1.5f;
                    spriteBatch.Draw(pixel, pos, new Rectangle(0, 0, 1, 1),
                        new Color(180, 255, 150) * alpha * 0.7f, 0f,
                        new Vector2(0.5f), new Vector2(sz), SpriteEffects.None, 0f);
                }
            }
            else {
                //未解锁虚线
                int dotLen = 10;
                int gapLen = 8;
                int total = dotLen + gapLen;
                int count = (int)(length / total);
                Color dotColor = new Color(60, 60, 70) * 0.5f * alpha;

                for (int i = 0; i < count; i++) {
                    Vector2 dotPos = start + new Vector2(i * total, 0).RotatedBy(rotation);
                    spriteBatch.Draw(pixel, dotPos, new Rectangle(0, 0, dotLen, lineWidth),
                        dotColor, rotation, new Vector2(0, lineWidth / 2f), 1f, SpriteEffects.None, 0f);
                }
            }
        }

        public Vector4 GetPadding() => new Vector4(20, 40, 20, 20);

        public Rectangle GetCloseButtonRect(Rectangle panelRect) {
            return new Rectangle(panelRect.Right - 45, panelRect.Y + 12, 33, 33);
        }

        public Rectangle GetRewardButtonRect(Rectangle panelRect) {
            return new Rectangle(panelRect.X + panelRect.Width / 2 - 70, panelRect.Bottom - 65, 140, 38);
        }

        public void DrawQuestDetail(SpriteBatch spriteBatch, QuestNode node, Rectangle panelRect, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //详情已改为右侧停靠栏，不再是模态框，故不铺全屏压暗
            Rectangle shadowRect = panelRect;
            shadowRect.Offset(6, 6);
            spriteBatch.Draw(pixel, shadowRect, Color.Black * (0.6f * alpha));

            DrawShaderPanel(spriteBatch, panelRect, alpha, false);

            DrawNatureBorder(spriteBatch, pixel, panelRect, alpha);

            int ro = 18;
            Color rc = new Color(120, 200, 130) * alpha;
            DrawRuneNode(spriteBatch, pixel, new Vector2(panelRect.X + ro, panelRect.Y + ro), 12, rc, runeTimer);
            DrawRuneNode(spriteBatch, pixel, new Vector2(panelRect.Right - ro, panelRect.Y + ro), 12, rc, runeTimer + MathHelper.PiOver2);
            DrawRuneNode(spriteBatch, pixel, new Vector2(panelRect.X + ro, panelRect.Bottom - ro), 12, rc, runeTimer + MathHelper.Pi);
            DrawRuneNode(spriteBatch, pixel, new Vector2(panelRect.Right - ro, panelRect.Bottom - ro), 12, rc, runeTimer + MathHelper.Pi * 1.5f);

            DrawDetailContent(spriteBatch, node, panelRect, alpha);
        }

        private void DrawDetailContent(SpriteBatch spriteBatch, QuestNode node, Rectangle panelRect, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            int padding = 25;
            int currentY = panelRect.Y + padding;

            Color titleColor = node.IsCompleted ? new Color(150, 255, 180) : new Color(255, 220, 150);
            Utils.DrawBorderString(spriteBatch, node.DisplayName?.Value, new Vector2(panelRect.X + padding, currentY), titleColor * alpha, 1.3f);
            currentY += (int)(FontAssets.MouseText.Value.MeasureString(node.DisplayName?.Value).Y * 1.3f) + 12;

            Color divColor = new Color(100, 150, 100) * alpha;
            spriteBatch.Draw(pixel, new Rectangle(panelRect.X + padding, currentY, panelRect.Width - padding * 2, 2), divColor * 0.6f);
            int cx = panelRect.X + panelRect.Width / 2;
            DrawRuneNode(spriteBatch, pixel, new Vector2(cx, currentY), 8, divColor * 1.2f, runeTimer);
            currentY += 18;

            string description = string.IsNullOrEmpty(node.DetailedDescription?.Value) ? node.Description?.Value : node.DetailedDescription?.Value;
            if (!string.IsNullOrEmpty(description)) {
                int maxW = panelRect.Width - padding * 2;
                string[] lines = VaultUtils.WrapTextArray(description, FontAssets.MouseText.Value, (int)(maxW / 0.9f), 99, out _);
                foreach (string line in lines) {
                    if (string.IsNullOrEmpty(line)) continue;
                    Utils.DrawBorderString(spriteBatch, line.TrimEnd('-', ' '), new Vector2(panelRect.X + padding, currentY), Color.White * alpha, 0.9f);
                    currentY += (int)(FontAssets.MouseText.Value.MeasureString(line).Y * 0.9f) + 5;
                }
                currentY += 12;
            }

            if (node.Objectives != null && node.Objectives.Count > 0) {
                Utils.DrawBorderString(spriteBatch, QuestLog.ObjectiveText.Value + ":", new Vector2(panelRect.X + padding, currentY),
                    new Color(255, 220, 150) * alpha, 1f);
                currentY += 28;

                foreach (var obj in node.Objectives) {
                    string objText = $"• {obj.GetDisplayText()} ({obj.CurrentProgress}/{obj.RequiredProgress})";
                    Color objColor = obj.IsCompleted ? new Color(150, 255, 180) : Color.White;
                    Utils.DrawBorderString(spriteBatch, objText, new Vector2(panelRect.X + padding + 12, currentY), objColor * alpha, 0.85f);

                    if (obj.TargetItemID > 0) {
                        Vector2 textSize = FontAssets.MouseText.Value.MeasureString(objText) * 0.85f;
                        Rectangle itemRect = new Rectangle((int)(panelRect.X + padding + 12 + textSize.X + 10), currentY - 4, 26, 26);
                        spriteBatch.Draw(pixel, itemRect, new Color(0, 0, 0, 120) * alpha);

                        Main.instance.LoadItem(obj.TargetItemID);
                        Texture2D itemTex = TextureAssets.Item[obj.TargetItemID].Value;
                        if (itemTex != null) {
                            Rectangle frame = Main.itemAnimations[obj.TargetItemID] != null
                                ? Main.itemAnimations[obj.TargetItemID].GetFrame(itemTex) : itemTex.Frame();
                            float sc = 1f;
                            if (frame.Width > 22 || frame.Height > 22) sc = 22f / Math.Max(frame.Width, frame.Height);
                            spriteBatch.Draw(itemTex, itemRect.Center.ToVector2(), frame, Color.White * alpha, 0f, frame.Size() / 2f, sc, SpriteEffects.None, 0f);
                        }

                        if (itemRect.Contains(Main.MouseScreen.ToPoint()) && ContentSamples.ItemsByType.TryGetValue(obj.TargetItemID, out var item)) {
                            Main.HoverItem = item.Clone();
                            Main.hoverItemName = item.Name;
                        }
                    }

                    currentY += 24;
                }
                currentY += 12;
            }

            if (node.Rewards != null && node.Rewards.Count > 0) {
                Utils.DrawBorderString(spriteBatch, QuestLog.RewardText.Value + ":", new Vector2(panelRect.X + padding, currentY),
                    new Color(255, 220, 150) * alpha, 1f);
                currentY += 28;

                int rewardX = panelRect.X + padding + 12;
                foreach (var reward in node.Rewards) {
                    Rectangle rewardRect = new Rectangle(rewardX, currentY, 36, 36);
                    Color rewardColor = reward.Claimed ? new Color(80, 80, 90) : new Color(200, 255, 180);

                    if (rewardRect.Contains(Main.MouseScreen.ToPoint()) && ContentSamples.ItemsByType.TryGetValue(reward.ItemType, out var item)) {
                        Main.HoverItem = item.Clone();
                        Main.hoverItemName = item.Name;
                    }

                    spriteBatch.Draw(pixel, rewardRect, rewardColor * (alpha * 0.35f));

                    Main.instance.LoadItem(reward.ItemType);
                    Texture2D itemTexture = TextureAssets.Item[reward.ItemType].Value;
                    if (itemTexture != null) {
                        Rectangle frame = Main.itemAnimations[reward.ItemType] != null
                            ? Main.itemAnimations[reward.ItemType].GetFrame(itemTexture) : itemTexture.Frame();
                        float sc = 1f;
                        if (frame.Width > 34 || frame.Height > 34) sc = 34f / Math.Max(frame.Width, frame.Height);
                        spriteBatch.Draw(itemTexture, new Vector2(rewardRect.X + 18, rewardRect.Y + 18), frame, Color.White * alpha, 0f, frame.Size() / 2f, sc, SpriteEffects.None, 0f);
                    }

                    Utils.DrawBorderString(spriteBatch, $"x{reward.Amount}", new Vector2(rewardX + 40, currentY + 10), Color.White * alpha, 0.8f);

                    rewardX += 105;
                    if (rewardX > panelRect.Right - padding - 105) {
                        rewardX = panelRect.X + padding + 12;
                        currentY += 44;
                    }
                }
                currentY += 55;
            }

            if (node.IsCompleted && node.Rewards != null && node.Rewards.Exists(r => !r.Claimed)) {
                Rectangle buttonRect = GetRewardButtonRect(panelRect);
                bool hover = buttonRect.Contains(Main.MouseScreen.ToPoint());
                DrawMagicButton(spriteBatch, pixel, buttonRect, hover, alpha, QuestLog.ReceiveAwardText.Value);
            }
        }

        public void DrawProgressBar(SpriteBatch spriteBatch, QuestLog log, Rectangle panelRect) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            float alpha = log.MainPanelAlpha;
            bool nightMode = log.NightMode;

            int total = 0, completed = 0;
            foreach (var node in QuestNode.AllQuests) {
                if (node.IsHiddenNow) continue; //未现身的隐藏任务不进分母
                total++;
                if (node.IsCompleted) completed++;
            }
            float progress = total > 0 ? (float)completed / total : 0f;

            //宿主是页脚带：总控键统一在右簇，条从页脚左缘起
            int barHeight = log.ShowProgressBar ? 26 : 10;
            int barWidth = Math.Clamp(panelRect.Width / 2 - 290, 80, 340);
            Rectangle barRect = new Rectangle(panelRect.X + 16,
                panelRect.Y + (panelRect.Height - barHeight) / 2, barWidth, barHeight);

            spriteBatch.Draw(pixel, barRect, new Color(20, 15, 10) * 0.8f * alpha);

            Color borderColor = (nightMode ? new Color(80, 160, 120) : new Color(100, 180, 120)) * alpha;
            int border = 2;
            spriteBatch.Draw(pixel, new Rectangle(barRect.X, barRect.Y, barRect.Width, border), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(barRect.X, barRect.Bottom - border, barRect.Width, border), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(barRect.X, barRect.Y, border, barRect.Height), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(barRect.Right - border, barRect.Y, border, barRect.Height), borderColor);

            if (total > 0) {
                int fillW = (int)((barWidth - border * 2) * progress);
                Rectangle fillRect = new Rectangle(barRect.X + border, barRect.Y + border, fillW, barHeight - border * 2);

                Color fill1 = nightMode ? new Color(100, 180, 140) : new Color(120, 200, 140);
                Color fill2 = nightMode ? new Color(140, 220, 180) : new Color(160, 240, 180);
                int steps = 8;
                for (int i = 0; i < steps; i++) {
                    float t = i / (float)steps;
                    float t2 = (i + 1f) / steps;
                    int y1 = fillRect.Y + (int)(t * fillRect.Height);
                    int y2 = fillRect.Y + (int)(t2 * fillRect.Height);
                    spriteBatch.Draw(pixel, new Rectangle(fillRect.X, y1, fillRect.Width, Math.Max(1, y2 - y1)), Color.Lerp(fill1, fill2, t) * alpha * 0.7f);
                }

                float flow = (magicTimer * 1.5f) % 1f;
                int flowX = fillRect.X + (int)(flow * fillRect.Width);
                if (flowX < fillRect.Right) {
                    spriteBatch.Draw(pixel, new Rectangle(flowX - 1, fillRect.Y, 3, fillRect.Height), new Color(200, 255, 220) * 0.6f * alpha);
                }
            }

            if (log.ShowProgressBar) {
                string text = $"{QuestLog.ProgressText.Value}: {completed}/{total} ({(int)(progress * 100)}%)";
                Vector2 ts = FontAssets.MouseText.Value.MeasureString(text) * 0.85f;
                Utils.DrawBorderString(spriteBatch, text, new Vector2(barRect.X + barRect.Width / 2 - ts.X / 2, barRect.Y + barRect.Height / 2 - ts.Y / 2 + 2), Color.White * alpha, 0.85f);
            }

            Rectangle toggleRect = new Rectangle(barRect.Right + 6, barRect.Y + barHeight / 2 - 12, 24, 24);
            bool hoverToggle = toggleRect.Contains(Main.MouseScreen.ToPoint());
            Color toggleColor = hoverToggle ? new Color(220, 255, 200) : new Color(180, 220, 160);
            Utils.DrawBorderString(spriteBatch, log.ShowProgressBar ? "▲" : "▼", toggleRect.TopLeft(), toggleColor * alpha, 1.1f);

            if (hoverToggle) {
                Main.LocalPlayer.mouseInterface = true;
                if (Main.mouseLeft && Main.mouseLeftRelease) {
                    log.ShowProgressBar = !log.ShowProgressBar;
                    SoundEngine.PlaySound(SoundID.MenuTick);
                }
            }
        }

        #region 按钮区域与绘制

        public Rectangle GetStyleSwitchButtonRect(Rectangle panelRect) {
            return QuestLogTheme.FooterStyleButton(panelRect);
        }

        public void DrawStyleSwitchButton(SpriteBatch spriteBatch, Rectangle panelRect, bool isHovered, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle buttonRect = GetStyleSwitchButtonRect(panelRect);
            DrawSmallNatureButton(spriteBatch, pixel, buttonRect, isHovered, alpha, "[i:149]");
        }

        public Rectangle GetNightModeButtonRect(Rectangle panelRect) {
            return QuestLogTheme.FooterNightButton(panelRect);
        }

        public void DrawNightModeButton(SpriteBatch spriteBatch, Rectangle panelRect, bool isHovered, float alpha, bool isNightMode) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle buttonRect = GetNightModeButtonRect(panelRect);
            Vector2 center = buttonRect.Center.ToVector2();

            DrawSmallNatureButton(spriteBatch, pixel, buttonRect, isHovered, alpha, null);

            Color iconColor = isHovered ? Color.White : new Color(255, 255, 220);
            if (isNightMode) {
                spriteBatch.Draw(pixel, center, new Rectangle(0, 0, 14, 14), iconColor * alpha, 0f, new Vector2(7, 7), 1f, SpriteEffects.None, 0f);
                spriteBatch.Draw(pixel, center + new Vector2(5, -2), new Rectangle(0, 0, 12, 12), new Color(40, 60, 50) * alpha, 0f, new Vector2(6, 6), 1f, SpriteEffects.None, 0f);
            }
            else {
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
            return QuestLogTheme.FooterClaimButton(panelRect);
        }

        public void DrawClaimAllButton(SpriteBatch spriteBatch, Rectangle panelRect, bool isHovered, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle buttonRect = GetClaimAllButtonRect(panelRect);
            DrawMagicButton(spriteBatch, pixel, buttonRect, isHovered, alpha, QuestLog.QuickReceiveAwardText.Value);
        }

        public Rectangle GetResetViewButtonRect(Rectangle panelRect) {
            return QuestLogTheme.FooterResetButton(panelRect);
        }

        public void DrawResetViewButton(SpriteBatch spriteBatch, Rectangle panelRect, Vector2 directionToCenter, bool isHovered, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle buttonRect = GetResetViewButtonRect(panelRect);
            Vector2 center = buttonRect.Center.ToVector2();

            DrawSmallNatureButton(spriteBatch, pixel, buttonRect, isHovered, alpha, null);

            float time = Main.GameUpdateCount * 0.015f;
            for (int i = 0; i < 6; i++) {
                float rot = i * MathHelper.PiOver2 * 0.666f + time;
                Vector2 offset = new Vector2(0, -15).RotatedBy(rot);
                spriteBatch.Draw(pixel, center + offset, new Rectangle(0, 0, 2, 4), new Color(180, 220, 180) * 0.4f * alpha, rot, new Vector2(1, 2), 1f, SpriteEffects.None, 0f);
            }

            float rotation = directionToCenter.ToRotation();
            float arrowPulse = (float)Math.Sin(Main.GameUpdateCount * 0.12f) * 0.2f + 1f;
            Color arrowColor = isHovered ? Color.White : new Color(220, 255, 220);

            spriteBatch.Draw(pixel, center, new Rectangle(0, 0, 14, 3), arrowColor * alpha, rotation, new Vector2(0, 1.5f), 1f, SpriteEffects.None, 0f);

            float headSize = 7f * arrowPulse;
            Vector2 headPos = center + new Vector2(7, 0).RotatedBy(rotation);
            spriteBatch.Draw(pixel, headPos, new Rectangle(0, 0, (int)headSize, 2), arrowColor * alpha, rotation + MathHelper.Pi * 0.75f, new Vector2(0, 1), 1f, SpriteEffects.None, 0f);
            spriteBatch.Draw(pixel, headPos, new Rectangle(0, 0, (int)headSize, 2), arrowColor * alpha, rotation - MathHelper.Pi * 0.75f, new Vector2(0, 1), 1f, SpriteEffects.None, 0f);

            spriteBatch.Draw(pixel, center, new Rectangle(0, 0, 4, 4), new Color(255, 100, 100) * alpha, 0f, new Vector2(2, 2), 1f, SpriteEffects.None, 0f);
        }

        #endregion

        #region 绘制工具方法

        private void DrawMagicButton(SpriteBatch spriteBatch, Texture2D pixel, Rectangle buttonRect, bool isHovered, float alpha, string text) {
            float pulse = (float)Math.Sin(glowTimer * 2.5f) * 0.5f + 0.5f;

            Color bg1 = isHovered ? Color.Lerp(new Color(80, 140, 90), Color.White, 0.3f) : new Color(80, 140, 90);
            Color bg2 = isHovered ? Color.Lerp(new Color(120, 180, 130), Color.White, 0.3f) : new Color(120, 180, 130);

            int steps = 8;
            for (int i = 0; i < steps; i++) {
                float t = i / (float)steps;
                float t2 = (i + 1f) / steps;
                int y1 = buttonRect.Y + (int)(t * buttonRect.Height);
                int y2 = buttonRect.Y + (int)(t2 * buttonRect.Height);
                spriteBatch.Draw(pixel, new Rectangle(buttonRect.X, y1, buttonRect.Width, Math.Max(1, y2 - y1)), Color.Lerp(bg1, bg2, t) * alpha * 0.85f);
            }

            Color glowColor = Color.Lerp(new Color(120, 200, 140), new Color(180, 255, 200), pulse);
            if (isHovered) glowColor = Color.White;
            glowColor *= alpha;

            int border = 3;
            spriteBatch.Draw(pixel, new Rectangle(buttonRect.X, buttonRect.Y, buttonRect.Width, border), glowColor);
            spriteBatch.Draw(pixel, new Rectangle(buttonRect.X, buttonRect.Bottom - border, buttonRect.Width, border), glowColor * 0.8f);
            spriteBatch.Draw(pixel, new Rectangle(buttonRect.X, buttonRect.Y, border, buttonRect.Height), glowColor * 0.9f);
            spriteBatch.Draw(pixel, new Rectangle(buttonRect.Right - border, buttonRect.Y, border, buttonRect.Height), glowColor * 0.9f);

            Color textColor = isHovered ? new Color(220, 255, 230) : Color.White;
            Utils.DrawBorderString(spriteBatch, text, new Vector2(buttonRect.X + buttonRect.Width / 2, buttonRect.Y + buttonRect.Height / 2), textColor * alpha, 0.9f, 0.5f, 0.5f);
        }

        private void DrawSmallNatureButton(SpriteBatch spriteBatch, Texture2D pixel, Rectangle buttonRect, bool isHovered, float alpha, string icon) {
            Vector2 center = buttonRect.Center.ToVector2();
            float radius = buttonRect.Width * 0.4f;

            DrawHexagon(spriteBatch, pixel, center + new Vector2(2, 2), radius, Color.Black * 0.4f * alpha);

            Color bgColor = isHovered ? new Color(120, 180, 140) : new Color(80, 120, 90);
            DrawHexagon(spriteBatch, pixel, center, radius, bgColor * alpha);

            Color borderColor = isHovered ? Color.White : new Color(180, 220, 180);
            DrawHexagonBorder(spriteBatch, pixel, center, radius, borderColor * alpha, 2);

            if (!string.IsNullOrEmpty(icon)) {
                Utils.DrawBorderString(spriteBatch, icon, center - new Vector2(12, 12), Color.White * alpha, 1f, 0.5f, 0.5f);
            }
        }

        private void DrawQuestIcon(SpriteBatch spriteBatch, QuestNode node, Vector2 center, float scale, float alpha) {
            Texture2D iconTexture = node.GetIconTexture();
            if (iconTexture == null) return;

            Rectangle? sourceRect = node.GetIconSourceRect(iconTexture);
            if (!sourceRect.HasValue) return;

            int iconSize = (int)(42 * scale);
            Rectangle frame = sourceRect.Value;
            float iconScale = 1f;
            if (frame.Width > iconSize || frame.Height > iconSize) iconScale = iconSize / (float)Math.Max(frame.Width, frame.Height);

            Color iconColor = node.IsUnlocked ? Color.White : new Color(80, 80, 90);
            if (node.IsCompleted) iconColor = new Color(220, 255, 220);

            spriteBatch.Draw(iconTexture, center, frame, iconColor * alpha, 0f, frame.Size() / 2f, iconScale, SpriteEffects.None, 0f);
        }

        private void DrawRuneNode(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, float size, Color color, float rotation) {
            int points = 6;
            for (int i = 0; i < points; i++) {
                float a1 = (i / (float)points) * MathHelper.TwoPi + rotation;
                float a2 = ((i + 2) / (float)points) * MathHelper.TwoPi + rotation;

                Vector2 p1 = center + new Vector2((float)Math.Cos(a1), (float)Math.Sin(a1)) * size;
                Vector2 p2 = center + new Vector2((float)Math.Cos(a2), (float)Math.Sin(a2)) * size;

                Vector2 diff = p2 - p1;
                float len = diff.Length();
                float rot = diff.ToRotation();
                spriteBatch.Draw(pixel, p1, new Rectangle(0, 0, (int)len, 2), color, rot, Vector2.Zero, 1f, SpriteEffects.None, 0f);
            }

            spriteBatch.Draw(pixel, center, new Rectangle(0, 0, 6, 6), color * 1.2f, 0f, new Vector2(3, 3), 1f, SpriteEffects.None, 0f);
        }

        private void DrawVineTrace(SpriteBatch spriteBatch, Texture2D pixel, Vector2 start, int length, float angle, Color color) {
            //从起点沿方向绘制波浪藤蔓
            Vector2 dir = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
            Vector2 perp = new Vector2(-dir.Y, dir.X);

            for (int i = 0; i < length; i += 3) {
                float t = i / (float)length;
                float wave = (float)Math.Sin(t * MathHelper.TwoPi * 2f + leafTimer) * 3f;
                Vector2 pos = start + dir * i + perp * wave;
                float fade = 1f - t;
                spriteBatch.Draw(pixel, pos, new Rectangle(0, 0, 3, 2), color * fade, angle, Vector2.One, 1f, SpriteEffects.None, 0f);
            }
        }

        private void DrawNatureBorder(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, float alpha) {
            float pulse = (float)Math.Sin(glowTimer * 2f) * 0.5f + 0.5f;
            Color outer = new Color(80, 140, 90);
            Color inner = new Color(120, 200, 130);
            Color edgeColor = Color.Lerp(outer, inner, pulse) * alpha;

            int b = 4;
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, b), edgeColor);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - b, rect.Width, b), edgeColor * 0.8f);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, b, rect.Height), edgeColor * 0.9f);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - b, rect.Y, b, rect.Height), edgeColor * 0.9f);
        }

        private void DrawHexagon(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, float radius, Color color) {
            int sides = 6;
            for (int i = 0; i < sides; i++) {
                float a1 = (i / (float)sides) * MathHelper.TwoPi - MathHelper.PiOver2;
                float a2 = ((i + 1) / (float)sides) * MathHelper.TwoPi - MathHelper.PiOver2;

                Vector2 p1 = center + new Vector2((float)Math.Cos(a1), (float)Math.Sin(a1)) * radius;
                Vector2 p2 = center + new Vector2((float)Math.Cos(a2), (float)Math.Sin(a2)) * radius;

                Vector2 diff = p2 - p1;
                float len = diff.Length();
                float rot = diff.ToRotation();
                spriteBatch.Draw(pixel, p1, new Rectangle(0, 0, (int)len, (int)radius), color, rot, Vector2.Zero, 1f, SpriteEffects.None, 0f);
            }
        }

        private void DrawHexagonBorder(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, float radius, Color color, int thickness) {
            int sides = 6;
            for (int i = 0; i < sides; i++) {
                float a1 = (i / (float)sides) * MathHelper.TwoPi - MathHelper.PiOver2;
                float a2 = ((i + 1) / (float)sides) * MathHelper.TwoPi - MathHelper.PiOver2;

                Vector2 p1 = center + new Vector2((float)Math.Cos(a1), (float)Math.Sin(a1)) * radius;
                Vector2 p2 = center + new Vector2((float)Math.Cos(a2), (float)Math.Sin(a2)) * radius;

                Vector2 diff = p2 - p1;
                float len = diff.Length();
                float rot = diff.ToRotation();
                spriteBatch.Draw(pixel, p1, new Rectangle(0, 0, (int)len, thickness), color, rot, new Vector2(0, thickness / 2f), 1f, SpriteEffects.None, 0f);
            }
        }

        #endregion
    }
}