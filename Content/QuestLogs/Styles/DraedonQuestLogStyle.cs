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
    public class DraedonQuestLogStyle : IQuestLogStyle
    {
        private float scanTimer;
        private float pulseTimer;
        private float shaderTime;
        private float dataFlowTimer;
        private const int EdgePad = 16;

        private struct DataParticle
        {
            public Vector2 Pos;
            public Vector2 Vel;
            public float Life;
            public float MaxLife;
            public float Size;
            public int Type; //0=方块 1=短线 2=点
        }
        private readonly List<DataParticle> dataParticles = [];

        public void UpdateStyle() {
            scanTimer += 0.04f;
            pulseTimer += 0.03f;
            shaderTime += 0.004f;
            dataFlowTimer += 0.008f;
            if (scanTimer > MathHelper.TwoPi) scanTimer -= MathHelper.TwoPi;
            if (pulseTimer > MathHelper.TwoPi) pulseTimer -= MathHelper.TwoPi;
            if (shaderTime > 100f) shaderTime -= 100f;
            if (dataFlowTimer > 1000f) dataFlowTimer -= 1000f;

            if (Main.rand.NextBool(6)) {
                dataParticles.Add(new DataParticle {
                    Pos = new Vector2(Main.rand.NextFloat(0, 1f), Main.rand.NextFloat(0, 1f)),
                    Vel = new Vector2(Main.rand.NextFloat(-0.001f, 0.001f), Main.rand.NextFloat(-0.003f, -0.001f)),
                    Life = 1f,
                    MaxLife = Main.rand.NextFloat(80f, 160f),
                    Size = Main.rand.NextFloat(1f, 3f),
                    Type = Main.rand.Next(3)
                });
            }

            for (int i = dataParticles.Count - 1; i >= 0; i--) {
                var p = dataParticles[i];
                p.Life -= 1f;
                p.Pos += p.Vel;
                dataParticles[i] = p;
                if (p.Life <= 0) dataParticles.RemoveAt(i);
            }
        }

        #region 着色器面板背景

        private void DrawShaderPanel(SpriteBatch sb, Rectangle rect, float alpha, bool nightMode) {
            Texture2D px = VaultAsset.placeholder2.Value;

            //DraedonPanel着色器
            if (EffectLoader.DraedonPanel?.Value != null) {
                Effect effect = EffectLoader.DraedonPanel.Value;
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
            Color top = nightMode ? new Color(20, 6, 8) : new Color(6, 12, 22);
            Color mid = nightMode ? new Color(12, 4, 5) : new Color(4, 8, 16);
            Color bot = nightMode ? new Color(6, 2, 3) : new Color(2, 5, 10);

            int segs = 20;
            for (int i = 0; i < segs; i++) {
                float t = i / (float)segs;
                float t2 = (i + 1f) / segs;
                int y1 = rect.Y + (int)(t * rect.Height);
                int y2 = rect.Y + (int)(t2 * rect.Height);
                Color c = t < 0.5f
                    ? Color.Lerp(top, mid, t * 2f)
                    : Color.Lerp(mid, bot, (t - 0.5f) * 2f);
                sb.Draw(px, new Rectangle(rect.X, y1, rect.Width, Math.Max(1, y2 - y1)), c * alpha);
            }

            Color scanC = nightMode ? new Color(35, 12, 15) : new Color(12, 25, 50);
            for (int y = rect.Y; y < rect.Bottom; y += 3)
                sb.Draw(px, new Rectangle(rect.X + 2, y, rect.Width - 4, 1), scanC * (alpha * 0.08f));

            Color gridC = nightMode ? new Color(50, 18, 22) : new Color(18, 45, 80);
            int gridSpacing = 40;
            for (int gx = rect.X + gridSpacing; gx < rect.Right; gx += gridSpacing)
                sb.Draw(px, new Rectangle(gx, rect.Y + 4, 1, rect.Height - 8), gridC * (alpha * 0.06f));
            for (int gy = rect.Y + gridSpacing; gy < rect.Bottom; gy += gridSpacing)
                sb.Draw(px, new Rectangle(rect.X + 4, gy, rect.Width - 8, 1), gridC * (alpha * 0.06f));

            int vigW = 35;
            for (int v = 0; v < vigW; v += 3) {
                float fade = 1f - v / (float)vigW;
                fade *= fade;
                Color vc = Color.Black * (alpha * 0.2f * fade);
                sb.Draw(px, new Rectangle(rect.X + v, rect.Y, 2, rect.Height), vc);
                sb.Draw(px, new Rectangle(rect.Right - v - 2, rect.Y, 2, rect.Height), vc);
            }
            for (int v = 0; v < 20; v += 3) {
                float fade = 1f - v / 20f;
                fade *= fade;
                Color vc = Color.Black * (alpha * 0.25f * fade);
                sb.Draw(px, new Rectangle(rect.X, rect.Y + v, rect.Width, 2), vc);
                sb.Draw(px, new Rectangle(rect.X, rect.Bottom - v - 2, rect.Width, 2), vc);
            }

            float scanY = rect.Y + (shaderTime * 0.06f % 1f) * rect.Height;
            Color sweepC = nightMode ? new Color(80, 25, 30) : new Color(30, 80, 150);
            for (int dy = -4; dy <= 4; dy++) {
                int py = (int)scanY + dy;
                if (py < rect.Y || py >= rect.Bottom) continue;
                float f = 1f - Math.Abs(dy) / 5f;
                sb.Draw(px, new Rectangle(rect.X + 2, py, rect.Width - 4, 1), sweepC * (alpha * 0.1f * f * f));
            }
        }

        #endregion

        public void DrawBackground(SpriteBatch spriteBatch, QuestLog log, Rectangle panelRect) {
            Texture2D px = VaultAsset.placeholder2.Value;
            bool nightMode = log.NightMode;
            float alpha = log.MainPanelAlpha;
            float pulse = MathF.Sin(pulseTimer) * 0.5f + 0.5f;

            Color accentColor = nightMode ? new Color(255, 60, 40) : new Color(40, 200, 255);

            //着色器面板
            DrawShaderPanel(spriteBatch, panelRect, alpha, nightMode);

            foreach (var p in dataParticles) {
                Vector2 drawPos = new Vector2(
                    panelRect.X + p.Pos.X * panelRect.Width,
                    panelRect.Y + p.Pos.Y * panelRect.Height);
                if (drawPos.X < panelRect.X || drawPos.X > panelRect.Right ||
                    drawPos.Y < panelRect.Y || drawPos.Y > panelRect.Bottom) continue;

                float lifeRatio = p.Life / p.MaxLife;
                float fade = MathF.Sin(lifeRatio * MathHelper.Pi);
                Color pColor = accentColor * (fade * 0.5f * alpha);

                if (p.Type == 0) {
                    spriteBatch.Draw(px, drawPos, new Rectangle(0, 0, 1, 1), pColor, 0f,
                        new Vector2(0.5f), new Vector2(p.Size * 2f, p.Size * 2f), SpriteEffects.None, 0f);
                }
                else if (p.Type == 1) {
                    float rot = p.Vel.ToRotation();
                    spriteBatch.Draw(px, drawPos, new Rectangle(0, 0, 1, 1), pColor, rot,
                        new Vector2(0.5f, 0.5f), new Vector2(p.Size * 4f, p.Size * 0.5f), SpriteEffects.None, 0f);
                }
                else {
                    spriteBatch.Draw(px, drawPos, new Rectangle(0, 0, 1, 1), pColor * 1.5f, 0f,
                        new Vector2(0.5f), p.Size, SpriteEffects.None, 0f);
                }
            }

            DrawCircuitNode(spriteBatch, new Vector2(panelRect.X + 14, panelRect.Y + 14), pulse, alpha, accentColor);
            DrawCircuitNode(spriteBatch, new Vector2(panelRect.Right - 14, panelRect.Y + 14), pulse, alpha, accentColor);
            DrawCircuitNode(spriteBatch, new Vector2(panelRect.X + 14, panelRect.Bottom - 14), pulse * 0.6f, alpha, accentColor);
            DrawCircuitNode(spriteBatch, new Vector2(panelRect.Right - 14, panelRect.Bottom - 14), pulse * 0.6f, alpha, accentColor);

            //角落电路走线
            DrawCircuitTrace(spriteBatch, px,
                new Vector2(panelRect.X + 14, panelRect.Y + 14), true, true, alpha, accentColor);
            DrawCircuitTrace(spriteBatch, px,
                new Vector2(panelRect.Right - 14, panelRect.Y + 14), false, true, alpha, accentColor);
        }

        public void DrawNode(SpriteBatch spriteBatch, QuestNode node, Vector2 drawPos, float scale, bool isHovered, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            bool isCompleted = node.IsCompleted;
            bool isUnlocked = node.IsUnlocked;
            bool hasUnclaimed = node.HasUnclaimedRewards;

            //四状态色，未解/进行/待领/已领
            Color coreColor, glowColor, edgeColor;

            if (isCompleted && !hasUnclaimed) {
                coreColor = new Color(50, 200, 80);
                glowColor = new Color(80, 255, 110);
                edgeColor = new Color(140, 255, 170);
            }
            else if (hasUnclaimed) {
                //待领金数据流
                coreColor = new Color(200, 170, 40);
                glowColor = new Color(255, 225, 70);
                edgeColor = new Color(255, 245, 140);
            }
            else if (isUnlocked) {
                coreColor = new Color(35, 170, 220);
                glowColor = new Color(60, 220, 255);
                edgeColor = new Color(120, 240, 255);
            }
            else {
                coreColor = new Color(50, 50, 60);
                glowColor = new Color(65, 65, 78);
                edgeColor = new Color(90, 90, 105);
            }

            if (isHovered) {
                coreColor = Color.Lerp(coreColor, Color.White, 0.3f);
                glowColor = Color.Lerp(glowColor, Color.White, 0.45f);
                edgeColor = Color.White;
            }

            float size = 44f * scale;
            float halfSize = size / 2f;
            float rotation = MathHelper.PiOver4;
            if (isHovered) rotation += MathF.Sin(dataFlowTimer * 5f) * 0.08f;

            //多层全息光晕
            if (isUnlocked || isCompleted) {
                float glowPulse = MathF.Sin(Main.GameUpdateCount * 0.06f) * 0.5f + 0.5f;
                for (int layer = 3; layer >= 1; layer--) {
                    float expand = 1f + layer * 0.12f + (isHovered ? 0.06f : 0f);
                    float layerAlpha = (0.08f + glowPulse * 0.04f) / layer;
                    //待领光晕加强
                    if (hasUnclaimed) layerAlpha *= 1.6f;
                    spriteBatch.Draw(px, drawPos, new Rectangle(0, 0, 1, 1),
                        glowColor * (layerAlpha * alpha), rotation,
                        new Vector2(0.5f), size * expand, SpriteEffects.None, 0f);
                }
            }

            spriteBatch.Draw(px, drawPos + new Vector2(3, 4), new Rectangle(0, 0, 1, 1),
                Color.Black * (0.5f * alpha), rotation,
                new Vector2(0.5f), size, SpriteEffects.None, 0f);

            //菱形金属壳
            spriteBatch.Draw(px, drawPos, new Rectangle(0, 0, 1, 1),
                coreColor * (0.9f * alpha), rotation,
                new Vector2(0.5f), size, SpriteEffects.None, 0f);

            //深色内核
            spriteBatch.Draw(px, drawPos, new Rectangle(0, 0, 1, 1),
                Color.Black * (0.7f * alpha), rotation,
                new Vector2(0.5f), size * 0.78f, SpriteEffects.None, 0f);

            //内核上亮渐变
            Color fillTop = Color.Lerp(coreColor, Color.Black, 0.5f);
            spriteBatch.Draw(px, drawPos + new Vector2(0, -2), new Rectangle(0, 0, 1, 1),
                fillTop * (0.4f * alpha), rotation,
                new Vector2(0.5f), size * 0.72f, SpriteEffects.None, 0f);

            //旋后顶/左高光线
            float edgeSize = size * 0.95f;
            spriteBatch.Draw(px, drawPos + new Vector2(-1, -1), new Rectangle(0, 0, 1, 1),
                edgeColor * (0.25f * alpha), rotation,
                new Vector2(0.5f), edgeSize, SpriteEffects.None, 0f);
            spriteBatch.Draw(px, drawPos, new Rectangle(0, 0, 1, 1),
                coreColor * (0.85f * alpha), rotation,
                new Vector2(0.5f), size * 0.92f, SpriteEffects.None, 0f);
            spriteBatch.Draw(px, drawPos, new Rectangle(0, 0, 1, 1),
                Color.Black * (0.65f * alpha), rotation,
                new Vector2(0.5f), size * 0.76f, SpriteEffects.None, 0f);

            DrawQuestIcon(spriteBatch, node, drawPos, scale, alpha);

            Vector2 nameSize = FontAssets.MouseText.Value.MeasureString(node.DisplayName?.Value) * 0.75f;
            Vector2 namePos = new Vector2(drawPos.X, drawPos.Y + halfSize + 14);
            Color textColor;
            if (isCompleted && !hasUnclaimed) {
                textColor = new Color(130, 255, 155);
            }
            else if (hasUnclaimed) {
                textColor = new Color(255, 235, 110);
            }
            else if (isUnlocked) {
                textColor = new Color(160, 230, 255);
            }
            else {
                textColor = new Color(120, 120, 135);
            }
            if (isHovered) textColor = Color.White;

            Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value, node.DisplayName?.Value,
                namePos.X, namePos.Y, textColor * alpha, Color.Black * alpha, nameSize / 2, 0.75f);
        }

        public void DrawConnection(SpriteBatch spriteBatch, Vector2 start, Vector2 end, bool isUnlocked, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            Vector2 diff = end - start;
            float length = diff.Length();
            float rotation = diff.ToRotation();
            if (length < 2f) return;

            int lineW = 5;

            spriteBatch.Draw(px, start + new Vector2(2, 3).RotatedBy(rotation),
                new Rectangle(0, 0, (int)length, lineW), Color.Black * (0.35f * alpha),
                rotation, new Vector2(0, lineW / 2f), 1f, SpriteEffects.None, 0f);

            if (isUnlocked) {
                Color pipeBase = new Color(15, 45, 65);
                spriteBatch.Draw(px, start,
                    new Rectangle(0, 0, (int)length, lineW), pipeBase * (0.85f * alpha),
                    rotation, new Vector2(0, lineW / 2f), 1f, SpriteEffects.None, 0f);

                int segments = Math.Max((int)(length / 8f), 4);
                float flowProgress = (dataFlowTimer * 1.2f) % 1f;
                for (int i = 0; i < segments; i++) {
                    float t = i / (float)segments;
                    float dist = t * length;
                    Vector2 pos = start + new Vector2(dist, 0).RotatedBy(rotation);
                    float wave = MathF.Sin((t - flowProgress) * MathHelper.TwoPi * 2f);
                    float brightness = wave * 0.5f + 0.5f;

                    Color c = Color.Lerp(new Color(25, 80, 130), new Color(60, 220, 255), brightness);
                    int segLen = (int)(length / segments) + 1;

                    spriteBatch.Draw(px, pos, new Rectangle(0, 0, segLen, lineW),
                        c * (0.55f * alpha), rotation, new Vector2(0, lineW / 2f), 1f, SpriteEffects.None, 0f);
                    spriteBatch.Draw(px, pos, new Rectangle(0, 0, segLen, lineW / 2),
                        c * (0.7f * alpha * brightness), rotation, new Vector2(0, lineW / 4f), 1f, SpriteEffects.None, 0f);
                }

                int glowW = lineW + 8;
                spriteBatch.Draw(px, start, new Rectangle(0, 0, (int)length, glowW),
                    new Color(40, 180, 255) * (0.1f * alpha), rotation,
                    new Vector2(0, glowW / 2f), 1f, SpriteEffects.None, 0f);

                int packetCount = Math.Max((int)(length / 50f), 2);
                for (int i = 0; i < packetCount; i++) {
                    float t = ((dataFlowTimer * 0.8f + i * (1f / packetCount)) % 1f);
                    Vector2 pos = Vector2.Lerp(start, end, t);
                    float sz = 2.5f + MathF.Sin(dataFlowTimer * 3f + i * 2f) * 1f;
                    spriteBatch.Draw(px, pos, new Rectangle(0, 0, 1, 1),
                        new Color(180, 240, 255) * (0.9f * alpha), rotation,
                        new Vector2(0.5f, 0.5f), new Vector2(sz * 3f, sz), SpriteEffects.None, 0f);
                }
            }
            else {
                //未解锁点阵虚线
                int dashLen = 8;
                int gapLen = 10;
                int total = dashLen + gapLen;
                int dashCount = (int)(length / total);
                for (int i = 0; i < dashCount; i++) {
                    float dashStart = i * total;
                    Vector2 pos = start + new Vector2(dashStart, 0).RotatedBy(rotation);
                    spriteBatch.Draw(px, pos, new Rectangle(0, 0, dashLen, lineW - 1),
                        new Color(45, 50, 60) * (0.5f * alpha), rotation,
                        new Vector2(0, (lineW - 1) / 2f), 1f, SpriteEffects.None, 0f);
                }
            }
        }

        public Vector4 GetPadding() => new Vector4(20, 20, 20, 20);

        public Rectangle GetCloseButtonRect(Rectangle panelRect) =>
            new Rectangle(panelRect.Right - 40, panelRect.Y + 10, 30, 30);

        public Rectangle GetRewardButtonRect(Rectangle panelRect) =>
            new Rectangle(panelRect.X + panelRect.Width / 2 - 60, panelRect.Bottom - 60, 120, 35);

        public void DrawQuestDetail(SpriteBatch spriteBatch, QuestNode node, Rectangle panelRect, float alpha) {
            //详情已改为右侧停靠栏，不再是模态框，故不铺全屏压暗
            bool nightMode = QuestLog.Instance?.NightMode ?? false;

            DrawShaderPanel(spriteBatch, panelRect, alpha, nightMode);

            DrawDetailContent(spriteBatch, node, panelRect, alpha);
        }

        private void DrawDetailContent(SpriteBatch spriteBatch, QuestNode node, Rectangle panelRect, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            int padding = 20;
            int currentY = panelRect.Y + padding;
            bool nightMode = QuestLog.Instance?.NightMode ?? false;

            Color accentC = nightMode ? new Color(255, 100, 70) : new Color(60, 210, 255);
            Color labelC = nightMode ? new Color(255, 180, 150) : new Color(180, 230, 255);

            Color titleColor = node.IsCompleted ? new Color(120, 255, 150) : accentC;
            Utils.DrawBorderString(spriteBatch, node.DisplayName?.Value,
                new Vector2(panelRect.X + padding, currentY), titleColor * alpha, 1.2f);
            currentY += (int)(FontAssets.MouseText.Value.MeasureString(node.DisplayName?.Value).Y * 1.2f) + 8;

            //双线凹槽+扫掠点
            int lineW = panelRect.Width - padding * 2;
            spriteBatch.Draw(px, new Rectangle(panelRect.X + padding, currentY, lineW, 1),
                Color.Black * (alpha * 0.8f));
            spriteBatch.Draw(px, new Rectangle(panelRect.X + padding, currentY + 1, lineW, 1),
                accentC * (alpha * 0.5f));
            float sweepX = panelRect.X + padding + (dataFlowTimer * 0.8f % 1f) * lineW;
            for (int dx = -4; dx <= 4; dx++) {
                int px2 = (int)sweepX + dx;
                if (px2 >= panelRect.X + padding && px2 < panelRect.X + padding + lineW) {
                    float f = 1f - Math.Abs(dx) / 5f;
                    spriteBatch.Draw(px, new Rectangle(px2, currentY, 1, 2), accentC * (alpha * 0.6f * f * f));
                }
            }
            currentY += 14;

            string desc = string.IsNullOrEmpty(node.DetailedDescription?.Value)
                ? node.Description?.Value : node.DetailedDescription?.Value;
            if (!string.IsNullOrEmpty(desc)) {
                int maxW = panelRect.Width - padding * 2;
                string[] lines = VaultUtils.WrapTextArray(desc, FontAssets.MouseText.Value, (int)(maxW / 0.85f), 99, out _);
                foreach (string line in lines) {
                    if (string.IsNullOrEmpty(line)) continue;
                    Utils.DrawBorderString(spriteBatch, line.TrimEnd('-', ' '),
                        new Vector2(panelRect.X + padding, currentY), Color.White * alpha, 0.85f);
                    currentY += (int)(FontAssets.MouseText.Value.MeasureString(line).Y * 0.85f) + 4;
                }
                currentY += 10;
            }

            if (node.Objectives != null && node.Objectives.Count > 0) {
                Utils.DrawBorderString(spriteBatch, QuestLog.ObjectiveText.Value + ":",
                    new Vector2(panelRect.X + padding, currentY), labelC * alpha, 0.9f);
                currentY += 25;

                foreach (var obj in node.Objectives) {
                    string objText = $"• {obj.GetDisplayText()} ({obj.CurrentProgress}/{obj.RequiredProgress})";
                    Color objColor = obj.IsCompleted ? new Color(140, 255, 160) : Color.White;
                    Utils.DrawBorderString(spriteBatch, objText,
                        new Vector2(panelRect.X + padding + 10, currentY), objColor * alpha, 0.8f);

                    if (obj.TargetItemID > 0) {
                        Vector2 textSize = FontAssets.MouseText.Value.MeasureString(objText) * 0.8f;
                        Rectangle itemRect = new Rectangle(
                            (int)(panelRect.X + padding + 10 + textSize.X + 10),
                            currentY - 4, 24, 24);

                        spriteBatch.Draw(px, itemRect, new Color(0, 0, 0, 100) * alpha);
                        Main.instance.LoadItem(obj.TargetItemID);
                        Texture2D itemTex = TextureAssets.Item[obj.TargetItemID].Value;
                        if (itemTex != null) {
                            Rectangle frame = Main.itemAnimations[obj.TargetItemID] != null
                                ? Main.itemAnimations[obj.TargetItemID].GetFrame(itemTex)
                                : itemTex.Frame();
                            float sc = 1f;
                            if (frame.Width > 20 || frame.Height > 20)
                                sc = 20f / Math.Max(frame.Width, frame.Height);
                            spriteBatch.Draw(itemTex, itemRect.Center.ToVector2(), frame,
                                Color.White * alpha, 0f, frame.Size() / 2f, sc, SpriteEffects.None, 0f);
                        }
                        if (itemRect.Contains(Main.MouseScreen.ToPoint())
                            && ContentSamples.ItemsByType.TryGetValue(obj.TargetItemID, out var item)) {
                            Main.HoverItem = item.Clone();
                            Main.hoverItemName = item.Name;
                        }
                    }
                    currentY += 22;
                }
                currentY += 10;
            }

            if (node.Rewards != null && node.Rewards.Count > 0) {
                Utils.DrawBorderString(spriteBatch, QuestLog.RewardText.Value + ":",
                    new Vector2(panelRect.X + padding, currentY), labelC * alpha, 0.9f);
                currentY += 25;

                int rewardX = panelRect.X + padding + 10;
                foreach (var reward in node.Rewards) {
                    Rectangle rewardRect = new Rectangle(rewardX, currentY, 32, 32);
                    Color rewardColor = reward.Claimed ? new Color(60, 65, 75) : accentC;

                    if (rewardRect.Contains(Main.MouseScreen.ToPoint())
                        && ContentSamples.ItemsByType.TryGetValue(reward.ItemType, out var item)) {
                        Main.HoverItem = item.Clone();
                        Main.hoverItemName = item.Name;
                    }

                    spriteBatch.Draw(px, rewardRect, rewardColor * (alpha * 0.2f));
                    DrawThinTechBorder(spriteBatch, rewardRect, rewardColor * (alpha * 0.5f));

                    Main.instance.LoadItem(reward.ItemType);
                    Texture2D itemTex = TextureAssets.Item[reward.ItemType].Value;
                    if (itemTex != null) {
                        Rectangle frame = Main.itemAnimations[reward.ItemType] != null
                            ? Main.itemAnimations[reward.ItemType].GetFrame(itemTex)
                            : itemTex.Frame();
                        float sc = 1f;
                        if (frame.Width > 32 || frame.Height > 32)
                            sc = 32f / Math.Max(frame.Width, frame.Height);
                        spriteBatch.Draw(itemTex, new Vector2(rewardRect.X + 16, rewardRect.Y + 16),
                            frame, Color.White * alpha, 0f, frame.Size() / 2f, sc, SpriteEffects.None, 0f);
                    }

                    Utils.DrawBorderString(spriteBatch, $"x{reward.Amount}",
                        new Vector2(rewardX + 36, currentY + 8), Color.White * alpha, 0.75f);

                    rewardX += 100;
                    if (rewardX > panelRect.Right - padding - 100) {
                        rewardX = panelRect.X + padding + 10;
                        currentY += 40;
                    }
                }
                currentY += 50;
            }

            if (node.IsCompleted && node.Rewards != null && node.Rewards.Exists(r => !r.Claimed)) {
                Rectangle btnRect = GetRewardButtonRect(panelRect);
                bool hover = btnRect.Contains(Main.MouseScreen.ToPoint());
                DrawTechButton(spriteBatch, btnRect, QuestLog.ReceiveAwardText.Value, hover, alpha);
            }
        }

        public void DrawProgressBar(SpriteBatch spriteBatch, QuestLog log, Rectangle panelRect) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float alpha = log.MainPanelAlpha;
            bool nightMode = log.NightMode;
            Color barColor = nightMode ? new Color(255, 80, 60) : new Color(60, 220, 255);

            int total = 0, completed = 0;
            foreach (var node in QuestNode.AllQuests) {
                total++;
                if (node.IsCompleted) completed++;
            }
            float progress = total > 0 ? (float)completed / total : 0f;

            int barH = log.ShowProgressBar ? 22 : 8;
            int barW = panelRect.Width - 40;
            Rectangle barRect = new Rectangle(panelRect.X + 20, panelRect.Bottom + 10, barW, barH);

            spriteBatch.Draw(px, barRect, new Color(5, 8, 14) * (0.8f * alpha));

            if (total > 0) {
                int fillW = (int)(barW * progress);
                if (fillW > 0) {
                    Rectangle fillRect = new Rectangle(barRect.X, barRect.Y, fillW, barH);

                    //填充3段渐变
                    int segs = 6;
                    for (int i = 0; i < segs; i++) {
                        float t = i / (float)segs;
                        float t2 = (i + 1f) / segs;
                        int x1 = fillRect.X + (int)(t * fillW);
                        int x2 = fillRect.X + (int)(t2 * fillW);
                        Color c = Color.Lerp(barColor * 0.4f, barColor * 0.7f, t);
                        spriteBatch.Draw(px, new Rectangle(x1, fillRect.Y, Math.Max(1, x2 - x1), barH), c * alpha);
                    }

                    spriteBatch.Draw(px, new Rectangle(fillRect.X, fillRect.Y, fillW, 1),
                        Color.White * (0.2f * alpha));

                    float flow = (dataFlowTimer * 4f) % 1f;
                    int flowX = fillRect.X + (int)(flow * fillW);
                    if (flowX < fillRect.Right)
                        spriteBatch.Draw(px, new Rectangle(flowX, fillRect.Y, 3, barH),
                            Color.White * (0.5f * alpha));
                }
            }

            //科技边框
            DrawThinTechBorder(spriteBatch, barRect, barColor * (0.6f * alpha));

            if (log.ShowProgressBar) {
                string text = $"{QuestLog.ProgressText.Value}: {completed}/{total} ({(int)(progress * 100)}%)";
                Vector2 ts = FontAssets.MouseText.Value.MeasureString(text) * 0.8f;
                Utils.DrawBorderString(spriteBatch, text,
                    new Vector2(barRect.X + barW / 2 - ts.X / 2, barRect.Y + barH / 2 - ts.Y / 2 + 2),
                    Color.White * alpha, 0.8f);
            }

            Rectangle toggleRect = new Rectangle(barRect.Right + 5, barRect.Y + barH / 2 - 10, 20, 20);
            bool hoverToggle = toggleRect.Contains(Main.MouseScreen.ToPoint());
            Color toggleC = hoverToggle ? barColor : barColor * 0.6f;
            Utils.DrawBorderString(spriteBatch, log.ShowProgressBar ? "▲" : "▼",
                toggleRect.TopLeft(), toggleC * alpha, 1f);
            if (hoverToggle) {
                Main.LocalPlayer.mouseInterface = true;
                if (Main.mouseLeft && Main.mouseLeftRelease) {
                    log.ShowProgressBar = !log.ShowProgressBar;
                    SoundEngine.PlaySound(SoundID.MenuTick);
                }
            }
        }

        #region 通用绘制工具

        //科技按钮，渐变/扫描线/边光/角标
        private void DrawTechButton(SpriteBatch sb, Rectangle rect, string text, bool hover, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            bool nightMode = QuestLog.Instance?.NightMode ?? false;
            float pulse = MathF.Sin(pulseTimer * 3f) * 0.5f + 0.5f;
            Color accentC = nightMode ? new Color(255, 80, 55) : new Color(55, 210, 255);

            Rectangle shadowR = rect;
            shadowR.Offset(2, 3);
            sb.Draw(px, shadowR, Color.Black * (0.45f * alpha));

            Color topC = hover ? Color.Lerp(accentC, Color.White, 0.3f) : accentC;
            Color botC = hover ? Color.Lerp(accentC, Color.Black, 0.2f) : Color.Lerp(accentC, Color.Black, 0.5f);

            int steps = 6;
            for (int i = 0; i < steps; i++) {
                float t = i / (float)steps;
                float t2 = (i + 1f) / steps;
                int y1 = rect.Y + (int)(t * rect.Height);
                int y2 = rect.Y + (int)(t2 * rect.Height);
                Color c = Color.Lerp(topC, botC, t);
                sb.Draw(px, new Rectangle(rect.X, y1, rect.Width, Math.Max(1, y2 - y1)), c * (0.35f * alpha));
            }

            for (int y = rect.Y; y < rect.Bottom; y += 3)
                sb.Draw(px, new Rectangle(rect.X + 1, y, rect.Width - 2, 1), accentC * (alpha * 0.06f));

            Color edgeC = hover ? Color.White : Color.Lerp(accentC, Color.White, pulse * 0.3f);
            sb.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 1), edgeC * (0.8f * alpha));
            sb.Draw(px, new Rectangle(rect.X, rect.Y, 1, rect.Height), edgeC * (0.6f * alpha));
            sb.Draw(px, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), edgeC * (0.3f * alpha));
            sb.Draw(px, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), edgeC * (0.3f * alpha));

            int cs = 4;
            sb.Draw(px, new Rectangle(rect.X, rect.Y, cs, 1), edgeC * alpha);
            sb.Draw(px, new Rectangle(rect.X, rect.Y, 1, cs), edgeC * alpha);
            sb.Draw(px, new Rectangle(rect.Right - cs, rect.Y, cs, 1), edgeC * alpha);
            sb.Draw(px, new Rectangle(rect.Right - 1, rect.Y, 1, cs), edgeC * alpha);
            sb.Draw(px, new Rectangle(rect.X, rect.Bottom - 1, cs, 1), edgeC * alpha);
            sb.Draw(px, new Rectangle(rect.X, rect.Bottom - cs, 1, cs), edgeC * alpha);
            sb.Draw(px, new Rectangle(rect.Right - cs, rect.Bottom - 1, cs, 1), edgeC * alpha);
            sb.Draw(px, new Rectangle(rect.Right - 1, rect.Bottom - cs, 1, cs), edgeC * alpha);

            Color textC = hover ? Color.White : new Color(230, 245, 255);
            Utils.DrawBorderString(sb, text,
                new Vector2(rect.X + rect.Width / 2, rect.Y + rect.Height / 2),
                textC * alpha, 0.85f, 0.5f, 0.5f);
        }

        //方形科技小按钮
        private void DrawSmallTechButton(SpriteBatch sb, Rectangle rect, bool hover, float alpha,
            Action<SpriteBatch, Vector2, float> drawIcon) {
            Texture2D px = VaultAsset.placeholder2.Value;
            bool nightMode = QuestLog.Instance?.NightMode ?? false;
            Color accentC = nightMode ? new Color(255, 70, 50) : new Color(50, 195, 255);

            Rectangle shadow = rect;
            shadow.Offset(1, 2);
            sb.Draw(px, shadow, Color.Black * (0.4f * alpha));

            Color bgC = hover
                ? Color.Lerp(accentC, Color.White, 0.15f) * 0.25f
                : accentC * 0.12f;
            sb.Draw(px, rect, bgC * alpha);

            for (int y = rect.Y; y < rect.Bottom; y += 3)
                sb.Draw(px, new Rectangle(rect.X + 1, y, rect.Width - 2, 1), accentC * (alpha * 0.04f));

            Color edgeC = hover ? Color.White : accentC * 0.7f;
            sb.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 1), edgeC * (0.7f * alpha));
            sb.Draw(px, new Rectangle(rect.X, rect.Y, 1, rect.Height), edgeC * (0.5f * alpha));
            sb.Draw(px, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), edgeC * (0.25f * alpha));
            sb.Draw(px, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), edgeC * (0.25f * alpha));

            int cs = 3;
            sb.Draw(px, new Rectangle(rect.X, rect.Y, cs, 1), edgeC * alpha);
            sb.Draw(px, new Rectangle(rect.X, rect.Y, 1, cs), edgeC * alpha);
            sb.Draw(px, new Rectangle(rect.Right - cs, rect.Bottom - 1, cs, 1), edgeC * alpha);
            sb.Draw(px, new Rectangle(rect.Right - 1, rect.Bottom - cs, 1, cs), edgeC * alpha);

            drawIcon?.Invoke(sb, rect.Center.ToVector2(), alpha);
        }

        private void DrawQuestIcon(SpriteBatch spriteBatch, QuestNode node, Vector2 center, float scale, float alpha) {
            Texture2D iconTex = node.GetIconTexture();
            if (iconTex == null) return;
            Rectangle? sourceRect = node.GetIconSourceRect(iconTex);
            if (!sourceRect.HasValue) return;

            int iconSize = (int)(34 * scale);
            Rectangle frame = sourceRect.Value;
            float iconScale = 1f;
            if (frame.Width > iconSize || frame.Height > iconSize)
                iconScale = iconSize / (float)Math.Max(frame.Width, frame.Height);

            Color iconColor = node.IsCompleted ? new Color(200, 255, 210) :
                             (node.IsUnlocked ? Color.White : new Color(90, 95, 110));

            spriteBatch.Draw(iconTex, center, frame, iconColor * alpha, 0f,
                frame.Size() / 2f, iconScale, SpriteEffects.None, 0f);
        }

        //角落电路节点
        private void DrawCircuitNode(SpriteBatch sb, Vector2 pos, float pulse, float alpha, Color color) {
            Texture2D px = VaultAsset.placeholder2.Value;

            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), color * (0.15f * pulse * alpha), 0f,
                new Vector2(0.5f), 14f, SpriteEffects.None, 0f);

            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), color * (0.7f * pulse * alpha), 0f,
                new Vector2(0.5f), new Vector2(12f, 1.5f), SpriteEffects.None, 0f);
            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), color * (0.6f * pulse * alpha), MathHelper.PiOver2,
                new Vector2(0.5f), new Vector2(12f, 1.5f), SpriteEffects.None, 0f);

            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), color * (0.9f * pulse * alpha), MathHelper.PiOver4,
                new Vector2(0.5f), 4f, SpriteEffects.None, 0f);

            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), Color.White * (0.3f * pulse * alpha), 0f,
                new Vector2(0.5f), 1.5f, SpriteEffects.None, 0f);
        }

        //角落电路走线
        private void DrawCircuitTrace(SpriteBatch sb, Texture2D px, Vector2 corner,
            bool goRight, bool goDown, float alpha, Color color) {
            int dirX = goRight ? 1 : -1;
            int dirY = goDown ? 1 : -1;
            Color traceC = color * (alpha * 0.2f);

            sb.Draw(px, new Rectangle((int)corner.X, (int)corner.Y, 30 * dirX, 1), traceC);
            Vector2 turn = corner + new Vector2(30 * dirX, 0);
            sb.Draw(px, new Rectangle((int)turn.X, (int)turn.Y, 1, 20 * dirY), traceC);
            Vector2 end = turn + new Vector2(0, 20 * dirY);
            sb.Draw(px, end, new Rectangle(0, 0, 1, 1), color * (alpha * 0.35f), 0f,
                new Vector2(0.5f), 2f, SpriteEffects.None, 0f);
        }

        //细线科技边框
        private void DrawThinTechBorder(SpriteBatch sb, Rectangle rect, Color color) {
            Texture2D px = VaultAsset.placeholder2.Value;
            sb.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 1), color);
            sb.Draw(px, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), color * 0.6f);
            sb.Draw(px, new Rectangle(rect.X, rect.Y, 1, rect.Height), color * 0.8f);
            sb.Draw(px, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), color * 0.5f);
        }

        #endregion

        #region 按钮区域与绘制

        public Rectangle GetStyleSwitchButtonRect(Rectangle panelRect) =>
            new Rectangle(panelRect.X + 15, panelRect.Bottom - 45, 30, 30);

        public void DrawStyleSwitchButton(SpriteBatch spriteBatch, Rectangle panelRect, bool isHovered, float alpha) {
            Rectangle btnRect = GetStyleSwitchButtonRect(panelRect);
            bool nightMode = QuestLog.Instance?.NightMode ?? false;
            Color ic = nightMode ? new Color(255, 160, 130) : new Color(160, 220, 255);
            if (isHovered) ic = Color.White;

            DrawSmallTechButton(spriteBatch, btnRect, isHovered, alpha, (sb, center, a) => {
                Texture2D px = VaultAsset.placeholder2.Value;
                sb.Draw(px, center + new Vector2(2, -1), new Rectangle(0, 0, 12, 16),
                    ic * (a * 0.35f), 0f, new Vector2(6, 8), 1f, SpriteEffects.None, 0f);
                sb.Draw(px, center + new Vector2(-1, 1), new Rectangle(0, 0, 12, 16),
                    ic * (a * 0.7f), 0f, new Vector2(6, 8), 1f, SpriteEffects.None, 0f);
                for (int i = 0; i < 3; i++)
                    sb.Draw(px, center + new Vector2(-1, 1) + new Vector2(-3, -4 + i * 4),
                        new Rectangle(0, 0, 6, 1), Color.Black * (a * 0.5f), 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
            });
        }

        public Rectangle GetNightModeButtonRect(Rectangle panelRect) =>
            new Rectangle(panelRect.X + 55, panelRect.Bottom - 45, 30, 30);

        public void DrawNightModeButton(SpriteBatch spriteBatch, Rectangle panelRect, bool isHovered, float alpha, bool isNightMode) {
            Rectangle btnRect = GetNightModeButtonRect(panelRect);
            Color ic = isNightMode ? new Color(255, 200, 80) : new Color(100, 200, 255);
            if (isHovered) ic = Color.White;

            DrawSmallTechButton(spriteBatch, btnRect, isHovered, alpha, (sb, center, a) => {
                Texture2D px = VaultAsset.placeholder2.Value;
                if (isNightMode) {
                    sb.Draw(px, center, new Rectangle(0, 0, 14, 14), ic * a, 0f,
                        new Vector2(7, 7), 1f, SpriteEffects.None, 0f);
                    sb.Draw(px, center + new Vector2(4, -2), new Rectangle(0, 0, 12, 12),
                        new Color(10, 5, 5) * a, 0f, new Vector2(6, 6), 1f, SpriteEffects.None, 0f);
                }
                else {
                    sb.Draw(px, center, new Rectangle(0, 0, 8, 8), ic * a, 0f,
                        new Vector2(4, 4), 1f, SpriteEffects.None, 0f);
                    float time = Main.GameUpdateCount * 0.025f;
                    for (int i = 0; i < 8; i++) {
                        float rot = i * MathHelper.PiOver4 + time;
                        Vector2 off = new Vector2(0, -8).RotatedBy(rot);
                        sb.Draw(px, center + off, new Rectangle(0, 0, 2, 3), ic * (a * 0.8f),
                            rot, new Vector2(1, 1.5f), 1f, SpriteEffects.None, 0f);
                    }
                }
            });
        }

        public Rectangle GetClaimAllButtonRect(Rectangle panelRect) =>
            new Rectangle(panelRect.X + panelRect.Width / 2 - 70, panelRect.Bottom + 40, 140, 35);

        public void DrawClaimAllButton(SpriteBatch spriteBatch, Rectangle panelRect, bool isHovered, float alpha) {
            Rectangle btnRect = GetClaimAllButtonRect(panelRect);
            DrawTechButton(spriteBatch, btnRect, QuestLog.QuickReceiveAwardText.Value, isHovered, alpha);
        }

        public Rectangle GetResetViewButtonRect(Rectangle panelRect) =>
            new Rectangle(panelRect.Right - 45, panelRect.Bottom - 48, 36, 36);

        public void DrawResetViewButton(SpriteBatch spriteBatch, Rectangle panelRect, Vector2 directionToCenter, bool isHovered, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            Rectangle btnRect = GetResetViewButtonRect(panelRect);
            Vector2 center = btnRect.Center.ToVector2();
            bool nightMode = QuestLog.Instance?.NightMode ?? false;
            Color accentC = nightMode ? new Color(255, 80, 55) : new Color(55, 200, 255);
            Color arrowC = isHovered ? Color.White : accentC;

            DrawSmallTechButton(spriteBatch, btnRect, isHovered, alpha, null);

            float scanRot = Main.GameUpdateCount * 0.03f;
            for (int i = 0; i < 4; i++) {
                float r = scanRot + i * MathHelper.PiOver2;
                Vector2 tickEnd = center + new Vector2(0, -12).RotatedBy(r);
                spriteBatch.Draw(px, tickEnd, new Rectangle(0, 0, 1, 3), accentC * (alpha * 0.3f),
                    (float)r, new Vector2(0.5f, 1.5f), 1f, SpriteEffects.None, 0f);
            }

            float rot = directionToCenter.ToRotation();
            spriteBatch.Draw(px, center, new Rectangle(0, 0, 14, 2), arrowC * alpha, rot,
                new Vector2(0, 1f), 1f, SpriteEffects.None, 0f);
            float headSz = 7f + MathF.Sin(Main.GameUpdateCount * 0.15f) * 1.5f;
            Vector2 headPos = center + new Vector2(7, 0).RotatedBy(rot);
            spriteBatch.Draw(px, headPos, new Rectangle(0, 0, (int)headSz, 2), arrowC * alpha,
                rot + MathHelper.Pi * 0.75f, new Vector2(0, 1), 1f, SpriteEffects.None, 0f);
            spriteBatch.Draw(px, headPos, new Rectangle(0, 0, (int)headSz, 2), arrowC * alpha,
                rot - MathHelper.Pi * 0.75f, new Vector2(0, 1), 1f, SpriteEffects.None, 0f);

            spriteBatch.Draw(px, center, new Rectangle(0, 0, 3, 3), accentC * alpha,
                0f, new Vector2(1.5f, 1.5f), 1f, SpriteEffects.None, 0f);
        }

        #endregion
    }
}
