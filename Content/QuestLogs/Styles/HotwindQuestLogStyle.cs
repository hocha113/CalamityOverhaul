using CalamityOverhaul.Common;
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
        private float flowTimer;
        private float pulseTimer;
        private float shaderTime;
        private const int EdgePad = 16;

        public void UpdateStyle() {
            flowTimer += 0.025f;
            if (flowTimer > MathHelper.TwoPi) flowTimer -= MathHelper.TwoPi;
            pulseTimer += 0.025f;
            shaderTime += 0.004f;
            if (shaderTime > 100f) shaderTime -= 100f;
        }

        #region 面板着色器背景

        //着色器面板，降级程序化
        private void DrawShaderPanel(SpriteBatch sb, Rectangle rect, float alpha, bool nightMode) {
            Texture2D px = VaultAsset.placeholder2.Value;

            if (EffectLoader.HotwindPanel?.Value != null) {
                Effect effect = EffectLoader.HotwindPanel.Value;
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

        //降级背景，噪声/扫描线/暗角/脉冲
        private void DrawFallbackBackground(SpriteBatch sb, Texture2D px, Rectangle rect, float alpha, bool nightMode) {
            Color top = nightMode ? new Color(14, 20, 38) : new Color(28, 18, 10);
            Color mid = nightMode ? new Color(8, 14, 28) : new Color(18, 10, 6);
            Color bot = nightMode ? new Color(5, 8, 18) : new Color(10, 6, 4);

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

            Color scanC = nightMode ? new Color(12, 20, 40) : new Color(30, 18, 8);
            for (int y = rect.Y; y < rect.Bottom; y += 3)
                sb.Draw(px, new Rectangle(rect.X + 2, y, rect.Width - 4, 1), scanC * (alpha * 0.08f));

            int vigW = 30;
            for (int v = 0; v < vigW; v += 3) {
                float fade = (1f - v / (float)vigW);
                fade *= fade;
                Color vc = Color.Black * (alpha * 0.18f * fade);
                sb.Draw(px, new Rectangle(rect.X + v, rect.Y, 2, rect.Height), vc);
                sb.Draw(px, new Rectangle(rect.Right - v - 2, rect.Y, 2, rect.Height), vc);
            }
            for (int v = 0; v < 20; v += 3) {
                float fade = (1f - v / 20f);
                fade *= fade;
                Color vc = Color.Black * (alpha * 0.22f * fade);
                sb.Draw(px, new Rectangle(rect.X, rect.Y + v, rect.Width, 2), vc);
                sb.Draw(px, new Rectangle(rect.X, rect.Bottom - v - 2, rect.Width, 2), vc);
            }

            float pulse = MathF.Sin(pulseTimer * 2f) * 0.5f + 0.5f;
            Color pulseC = nightMode ? new Color(30, 70, 160) : new Color(160, 70, 30);
            sb.Draw(px, rect, pulseC * (0.03f * pulse * alpha));

            float scanY = rect.Y + (shaderTime * 0.055f % 1f) * rect.Height;
            for (int dy = -5; dy <= 5; dy++) {
                int py = (int)scanY + dy;
                if (py < rect.Y || py >= rect.Bottom) continue;
                float f = 1f - Math.Abs(dy) / 6f;
                Color sc = nightMode ? new Color(20, 50, 120) : new Color(120, 55, 18);
                sb.Draw(px, new Rectangle(rect.X + 2, py, rect.Width - 4, 1), sc * (alpha * 0.08f * f * f));
            }
        }

        #endregion

        public void DrawBackground(SpriteBatch spriteBatch, QuestLog log, Rectangle panelRect) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            bool nightMode = log.NightMode;
            float alpha = log.MainPanelAlpha;

            //着色器面板背景
            DrawShaderPanel(spriteBatch, panelRect, alpha, nightMode);

            float pulse = MathF.Sin(pulseTimer * 2f) * 0.5f + 0.5f;
            DrawCornerRivet(spriteBatch, new Vector2(panelRect.X + 12, panelRect.Y + 12), pulse, alpha, nightMode);
            DrawCornerRivet(spriteBatch, new Vector2(panelRect.Right - 12, panelRect.Y + 12), pulse, alpha, nightMode);
            DrawCornerRivet(spriteBatch, new Vector2(panelRect.X + 12, panelRect.Bottom - 12), pulse * 0.7f, alpha, nightMode);
            DrawCornerRivet(spriteBatch, new Vector2(panelRect.Right - 12, panelRect.Bottom - 12), pulse * 0.7f, alpha, nightMode);
        }

        public void DrawNode(SpriteBatch spriteBatch, QuestNode node, Vector2 drawPos, float scale, bool isHovered, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            int size = (int)(48 * scale);
            int halfSize = size / 2;
            float glowPulse = MathF.Sin(Main.GameUpdateCount * 0.05f) * 0.5f + 0.5f;

            //四状态色，未解/进行/待领/已领
            bool hasUnclaimed = node.HasUnclaimedRewards;
            Color coreColor, glowColor, edgeLight;

            if (node.IsCompleted && !hasUnclaimed) {
                //已领沉绿
                coreColor = new Color(45, 130, 65);
                glowColor = new Color(70, 200, 90);
                edgeLight = new Color(130, 255, 155);
            }
            else if (hasUnclaimed) {
                //待领金
                coreColor = new Color(160, 130, 30);
                glowColor = new Color(245, 210, 60);
                edgeLight = new Color(255, 240, 120);
            }
            else if (node.IsUnlocked) {
                coreColor = new Color(155, 85, 35);
                glowColor = new Color(220, 140, 55);
                edgeLight = new Color(255, 180, 90);
            }
            else {
                coreColor = new Color(45, 45, 55);
                glowColor = new Color(70, 70, 85);
                edgeLight = new Color(110, 110, 130);
            }

            if (isHovered) {
                coreColor = Color.Lerp(coreColor, Color.White, 0.25f);
                glowColor = Color.Lerp(glowColor, Color.White, 0.4f);
                edgeLight = Color.White;
            }

            //多层辐射光晕
            if (node.IsUnlocked || node.IsCompleted) {
                for (int layer = 3; layer >= 1; layer--) {
                    int expand = layer * 5 + (isHovered ? 3 : 0);
                    float layerAlpha = (0.06f + glowPulse * 0.04f) / layer;
                    //待领光晕加强
                    if (hasUnclaimed) layerAlpha *= 1.6f;
                    Rectangle glowRect = new Rectangle(
                        (int)drawPos.X - halfSize - expand,
                        (int)drawPos.Y - halfSize - expand,
                        size + expand * 2, size + expand * 2);
                    spriteBatch.Draw(px, glowRect, glowColor * (layerAlpha * alpha));
                }
            }

            //投影
            Rectangle shadowR = new Rectangle(
                (int)drawPos.X - halfSize + 3,
                (int)drawPos.Y - halfSize + 4,
                size, size);
            spriteBatch.Draw(px, shadowR, Color.Black * (0.45f * alpha));

            //节点纵渐变
            Rectangle nodeRect = new Rectangle(
                (int)drawPos.X - halfSize,
                (int)drawPos.Y - halfSize,
                size, size);

            int gradSteps = 6;
            for (int g = 0; g < gradSteps; g++) {
                float gt = g / (float)gradSteps;
                float gt2 = (g + 1f) / gradSteps;
                int gy1 = nodeRect.Y + (int)(gt * nodeRect.Height);
                int gy2 = nodeRect.Y + (int)(gt2 * nodeRect.Height);

                //上亮下暗
                float lightFactor = 1f - gt * 0.6f;
                Color gc = new Color(
                    (int)(coreColor.R * lightFactor),
                    (int)(coreColor.G * lightFactor),
                    (int)(coreColor.B * lightFactor));
                spriteBatch.Draw(px, new Rectangle(nodeRect.X, gy1, nodeRect.Width, Math.Max(1, gy2 - gy1)),
                    gc * alpha);
            }

            //顶高光条
            spriteBatch.Draw(px, new Rectangle(nodeRect.X + 3, nodeRect.Y + 1, nodeRect.Width - 6, 1),
                edgeLight * (0.3f * alpha));
            spriteBatch.Draw(px, new Rectangle(nodeRect.X + 5, nodeRect.Y + 2, nodeRect.Width - 10, 1),
                edgeLight * (0.15f * alpha));

            //左亮右下阴影
            Color hlEdge = edgeLight * (0.4f * alpha);
            Color shEdge = Color.Black * (0.5f * alpha);
            int bw = isHovered ? 2 : 1;
            spriteBatch.Draw(px, new Rectangle(nodeRect.X, nodeRect.Y, bw, nodeRect.Height), hlEdge);
            spriteBatch.Draw(px, new Rectangle(nodeRect.X, nodeRect.Y, nodeRect.Width, bw), hlEdge);
            spriteBatch.Draw(px, new Rectangle(nodeRect.Right - bw, nodeRect.Y, bw, nodeRect.Height), shEdge);
            spriteBatch.Draw(px, new Rectangle(nodeRect.X, nodeRect.Bottom - bw, nodeRect.Width, bw), shEdge);

            DrawQuestIcon(spriteBatch, node, drawPos, scale, alpha);

            Vector2 nameSize = FontAssets.MouseText.Value.MeasureString(node.DisplayName?.Value) * 0.75f;
            Vector2 namePos = new Vector2(drawPos.X, drawPos.Y + halfSize + 8);
            Color textColor;
            if (node.IsCompleted && !node.HasUnclaimedRewards) {
                textColor = new Color(120, 230, 145);
            }
            else if (node.HasUnclaimedRewards) {
                textColor = new Color(255, 230, 100);
            }
            else if (node.IsUnlocked) {
                textColor = new Color(235, 185, 125);
            }
            else {
                textColor = new Color(125, 125, 140);
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

            int lineW = 6;

            spriteBatch.Draw(px, start + new Vector2(2, 3).RotatedBy(rotation),
                new Rectangle(0, 0, (int)length, lineW), Color.Black * (0.35f * alpha),
                rotation, new Vector2(0, lineW / 2f), 1f, SpriteEffects.None, 0f);

            if (isUnlocked) {
                spriteBatch.Draw(px, start,
                    new Rectangle(0, 0, (int)length, lineW), new Color(55, 35, 18) * (0.85f * alpha),
                    rotation, new Vector2(0, lineW / 2f), 1f, SpriteEffects.None, 0f);

                int segments = Math.Max((int)(length / 10f), 3);
                float flowProgress = (flowTimer * 0.2f) % 1f;
                for (int i = 0; i < segments; i++) {
                    float t = i / (float)segments;
                    float dist = t * length;
                    Vector2 pos = start + new Vector2(dist, 0).RotatedBy(rotation);
                    float wave = MathF.Sin((t - flowProgress) * MathHelper.TwoPi * 2f);
                    float brightness = wave * 0.5f + 0.5f;

                    Color c = Color.Lerp(new Color(130, 65, 25), new Color(255, 170, 65), brightness);
                    int segLen = (int)(length / segments) + 1;

                    spriteBatch.Draw(px, pos, new Rectangle(0, 0, segLen, lineW),
                        c * (0.5f * alpha), rotation, new Vector2(0, lineW / 2f), 1f, SpriteEffects.None, 0f);
                    spriteBatch.Draw(px, pos, new Rectangle(0, 0, segLen, lineW / 2),
                        c * (0.7f * alpha * brightness), rotation, new Vector2(0, lineW / 4f), 1f, SpriteEffects.None, 0f);
                }

                int glowW = lineW + 8;
                spriteBatch.Draw(px, start, new Rectangle(0, 0, (int)length, glowW),
                    new Color(255, 130, 50) * (0.12f * alpha), rotation,
                    new Vector2(0, glowW / 2f), 1f, SpriteEffects.None, 0f);

                int pulseCount = Math.Max((int)(length / 55f), 2);
                for (int i = 0; i < pulseCount; i++) {
                    float t = ((flowTimer * 0.5f + i * (1f / pulseCount)) % 1f);
                    Vector2 pos = Vector2.Lerp(start, end, t);
                    float sz = 3f + MathF.Sin(flowTimer * 5f + i) * 1.5f;
                    spriteBatch.Draw(px, pos, new Rectangle(0, 0, 1, 1),
                        new Color(255, 220, 160) * (0.8f * alpha), 0f,
                        new Vector2(0.5f, 0.5f), new Vector2(sz * 2f, sz), SpriteEffects.None, 0f);
                }
            }
            else {
                int dashLen = 12;
                int gapLen = 8;
                int total = dashLen + gapLen;
                int dashCount = (int)(length / total);
                for (int i = 0; i < dashCount; i++) {
                    float dashStart = i * total;
                    Vector2 pos = start + new Vector2(dashStart, 0).RotatedBy(rotation);
                    spriteBatch.Draw(px, pos, new Rectangle(0, 0, dashLen, lineW - 2),
                        new Color(60, 60, 72) * (0.5f * alpha), rotation,
                        new Vector2(0, (lineW - 2) / 2f), 1f, SpriteEffects.None, 0f);
                }
            }
        }

        public Vector4 GetPadding() => new Vector4(15, 35, 15, 15);

        public Rectangle GetCloseButtonRect(Rectangle panelRect) =>
            new Rectangle(panelRect.Right - 40, panelRect.Y + 10, 30, 30);

        public Rectangle GetRewardButtonRect(Rectangle panelRect) =>
            new Rectangle(panelRect.X + panelRect.Width / 2 - 60, panelRect.Bottom - 60, 120, 35);

        public void DrawQuestDetail(SpriteBatch spriteBatch, QuestNode node, Rectangle panelRect, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;

            spriteBatch.Draw(px, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight),
                Color.Black * (0.6f * alpha));

            bool nightMode = QuestLog.Instance?.NightMode ?? false;

            DrawShaderPanel(spriteBatch, panelRect, alpha, nightMode);

            DrawDetailContent(spriteBatch, node, panelRect, alpha);
        }

        private void DrawDetailContent(SpriteBatch spriteBatch, QuestNode node, Rectangle panelRect, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            int padding = 20;
            int currentY = panelRect.Y + padding;

            Color titleColor = node.IsCompleted ? new Color(115, 220, 135) : new Color(225, 175, 115);
            Utils.DrawBorderString(spriteBatch, node.DisplayName?.Value,
                new Vector2(panelRect.X + padding, currentY), titleColor * alpha, 1.2f);
            currentY += (int)(FontAssets.MouseText.Value.MeasureString(node.DisplayName?.Value).Y * 1.2f) + 8;

            //凹槽分隔线
            int lineW = panelRect.Width - padding * 2;
            spriteBatch.Draw(px, new Rectangle(panelRect.X + padding, currentY, lineW, 1),
                new Color(8, 5, 2) * (alpha * 0.8f));
            spriteBatch.Draw(px, new Rectangle(panelRect.X + padding, currentY + 1, lineW, 1),
                new Color(170, 105, 45) * (alpha * 0.35f));
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
                    new Vector2(panelRect.X + padding, currentY), new Color(215, 165, 105) * alpha, 0.9f);
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
                    new Vector2(panelRect.X + padding, currentY), new Color(215, 165, 105) * alpha, 0.9f);
                currentY += 25;

                int rewardX = panelRect.X + padding + 10;
                foreach (var reward in node.Rewards) {
                    Rectangle rewardRect = new Rectangle(rewardX, currentY, 32, 32);
                    Color rewardColor = reward.Claimed ? new Color(100, 100, 110) : new Color(255, 200, 120);

                    if (rewardRect.Contains(Main.MouseScreen.ToPoint())
                        && ContentSamples.ItemsByType.TryGetValue(reward.ItemType, out var item)) {
                        Main.HoverItem = item.Clone();
                        Main.hoverItemName = item.Name;
                    }

                    spriteBatch.Draw(px, rewardRect, rewardColor * (alpha * 0.3f));
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
                DrawMetallicButton(spriteBatch, btnRect, QuestLog.ReceiveAwardText.Value, hover, alpha);
            }
        }

        public void DrawProgressBar(SpriteBatch spriteBatch, QuestLog log, Rectangle panelRect) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float alpha = log.MainPanelAlpha;
            bool nightMode = log.NightMode;

            int total = 0, completed = 0;
            foreach (var node in QuestNode.AllQuests) {
                total++;
                if (node.IsCompleted) completed++;
            }
            float progress = total > 0 ? (float)completed / total : 0f;

            int barH = log.ShowProgressBar ? 22 : 8;
            int barW = panelRect.Width - 40;
            Rectangle barRect = new Rectangle(panelRect.X + 20, panelRect.Bottom + 10, barW, barH);

            spriteBatch.Draw(px, barRect, new Color(8, 6, 4) * (0.75f * alpha));

            if (total > 0) {
                int fillW = (int)(barW * progress);
                if (fillW > 0) {
                    Rectangle fillRect = new Rectangle(barRect.X, barRect.Y, fillW, barH);
                    Color fillC = nightMode ? new Color(50, 110, 200) : new Color(200, 120, 40);
                    spriteBatch.Draw(px, fillRect, fillC * (0.55f * alpha));

                    spriteBatch.Draw(px, new Rectangle(fillRect.X, fillRect.Y, fillW, 1),
                        Color.White * (0.15f * alpha));

                    float flow = (flowTimer * 2f) % 1f;
                    int flowX = fillRect.X + (int)(flow * fillW);
                    if (flowX < fillRect.Right)
                        spriteBatch.Draw(px, new Rectangle(flowX, fillRect.Y, 2, barH),
                            Color.White * (0.4f * alpha));
                }
            }

            //边光上亮下暗
            Color edgeHL = nightMode ? new Color(60, 130, 220) : new Color(220, 130, 55);
            spriteBatch.Draw(px, new Rectangle(barRect.X, barRect.Y, barRect.Width, 1), edgeHL * (0.5f * alpha));
            spriteBatch.Draw(px, new Rectangle(barRect.X, barRect.Bottom - 1, barRect.Width, 1),
                Color.Black * (0.4f * alpha));

            if (log.ShowProgressBar) {
                string text = $"{QuestLog.ProgressText.Value}: {completed}/{total} ({(int)(progress * 100)}%)";
                Vector2 ts = FontAssets.MouseText.Value.MeasureString(text) * 0.8f;
                Utils.DrawBorderString(spriteBatch, text,
                    new Vector2(barRect.X + barW / 2 - ts.X / 2, barRect.Y + barH / 2 - ts.Y / 2 + 2),
                    Color.White * alpha, 0.8f);
            }

            Rectangle toggleRect = new Rectangle(barRect.Right + 5, barRect.Y + barH / 2 - 10, 20, 20);
            bool hoverToggle = toggleRect.Contains(Main.MouseScreen.ToPoint());
            Color toggleC = hoverToggle ? new Color(255, 200, 100) : new Color(200, 150, 80);
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

        //金属按钮，纵渐变/顶反光/边光
        private void DrawMetallicButton(SpriteBatch sb, Rectangle rect, string text, bool hover, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            bool nightMode = QuestLog.Instance?.NightMode ?? false;
            float pulse = MathF.Sin(pulseTimer * 3f) * 0.5f + 0.5f;

            Rectangle shadowR = rect;
            shadowR.Offset(2, 3);
            sb.Draw(px, shadowR, Color.Black * (0.4f * alpha));

            //纵向金属渐变
            Color topC, botC;
            if (nightMode) {
                topC = hover ? new Color(70, 140, 220) : new Color(50, 100, 180);
                botC = hover ? new Color(40, 80, 150) : new Color(30, 60, 120);
            }
            else {
                topC = hover ? new Color(220, 145, 65) : new Color(185, 110, 45);
                botC = hover ? new Color(150, 85, 30) : new Color(120, 65, 22);
            }

            int steps = 8;
            for (int i = 0; i < steps; i++) {
                float t = i / (float)steps;
                float t2 = (i + 1f) / steps;
                int y1 = rect.Y + (int)(t * rect.Height);
                int y2 = rect.Y + (int)(t2 * rect.Height);
                Color c = Color.Lerp(topC, botC, t);
                sb.Draw(px, new Rectangle(rect.X, y1, rect.Width, Math.Max(1, y2 - y1)), c * alpha);
            }

            //顶部反光条
            sb.Draw(px, new Rectangle(rect.X + 3, rect.Y + 1, rect.Width - 6, 1),
                Color.White * (0.25f * alpha));

            Color hlC = nightMode
                ? Color.Lerp(new Color(100, 180, 255), new Color(150, 220, 255), pulse)
                : Color.Lerp(new Color(255, 200, 120), new Color(255, 230, 170), pulse);
            if (hover) hlC = Color.White;

            sb.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 1), hlC * (0.6f * alpha));
            sb.Draw(px, new Rectangle(rect.X, rect.Y, 1, rect.Height), hlC * (0.4f * alpha));
            sb.Draw(px, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), Color.Black * (0.3f * alpha));
            sb.Draw(px, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), Color.Black * (0.4f * alpha));

            Color textC = hover ? Color.White : new Color(255, 245, 230);
            Utils.DrawBorderString(sb, text,
                new Vector2(rect.X + rect.Width / 2, rect.Y + rect.Height / 2),
                textC * alpha, 0.85f, 0.5f, 0.5f);
        }

        private void DrawQuestIcon(SpriteBatch spriteBatch, QuestNode node, Vector2 center, float scale, float alpha) {
            Texture2D iconTex = node.GetIconTexture();
            if (iconTex == null) return;
            Rectangle? sourceRect = node.GetIconSourceRect(iconTex);
            if (!sourceRect.HasValue) return;

            int iconSize = (int)(40 * scale);
            Rectangle frame = sourceRect.Value;
            float iconScale = 1f;
            if (frame.Width > iconSize || frame.Height > iconSize)
                iconScale = iconSize / (float)Math.Max(frame.Width, frame.Height);

            Color iconColor = node.IsCompleted ? new Color(200, 255, 200) :
                             (node.IsUnlocked ? Color.White : new Color(100, 100, 110));

            spriteBatch.Draw(iconTex, center, frame, iconColor * alpha, 0f,
                frame.Size() / 2f, iconScale, SpriteEffects.None, 0f);
        }

        private void DrawCornerRivet(SpriteBatch sb, Vector2 pos, float pulse, float alpha, bool nightMode) {
            Texture2D px = VaultAsset.placeholder2.Value;
            Color baseC = nightMode ? new Color(50, 110, 190) : new Color(190, 110, 50);
            Color glowC = nightMode ? new Color(35, 80, 160) : new Color(160, 80, 35);

            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), glowC * (0.2f * pulse * alpha), 0f,
                new Vector2(0.5f, 0.5f), new Vector2(14f, 14f), SpriteEffects.None, 0f);

            //铆钉中心圆
            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), baseC * (0.8f * pulse * alpha), 0f,
                new Vector2(0.5f, 0.5f), new Vector2(5f, 5f), SpriteEffects.None, 0f);

            sb.Draw(px, pos + new Vector2(-1, -1), new Rectangle(0, 0, 1, 1),
                Color.White * (0.3f * pulse * alpha), 0f,
                new Vector2(0.5f, 0.5f), new Vector2(2f, 2f), SpriteEffects.None, 0f);
        }

        #endregion

        #region 按钮区域与绘制

        public Rectangle GetStyleSwitchButtonRect(Rectangle panelRect) =>
            new Rectangle(panelRect.X + 15, panelRect.Bottom - 45, 30, 30);

        public void DrawStyleSwitchButton(SpriteBatch spriteBatch, Rectangle panelRect, bool isHovered, float alpha) {
            Rectangle btnRect = GetStyleSwitchButtonRect(panelRect);
            DrawSmallIconButton(spriteBatch, btnRect, isHovered, alpha, DrawPageIcon);
        }

        public Rectangle GetNightModeButtonRect(Rectangle panelRect) =>
            new Rectangle(panelRect.X + 55, panelRect.Bottom - 45, 30, 30);

        public void DrawNightModeButton(SpriteBatch spriteBatch, Rectangle panelRect, bool isHovered, float alpha, bool isNightMode) {
            Rectangle btnRect = GetNightModeButtonRect(panelRect);
            DrawSmallIconButton(spriteBatch, btnRect, isHovered, alpha,
                (sb, center, a) => DrawDayNightIcon(sb, center, a, isNightMode, btnRect));
        }

        public Rectangle GetClaimAllButtonRect(Rectangle panelRect) =>
            new Rectangle(panelRect.X + panelRect.Width / 2 - 70, panelRect.Bottom + 40, 140, 35);

        public void DrawClaimAllButton(SpriteBatch spriteBatch, Rectangle panelRect, bool isHovered, float alpha) {
            Rectangle btnRect = GetClaimAllButtonRect(panelRect);
            DrawMetallicButton(spriteBatch, btnRect, QuestLog.QuickReceiveAwardText.Value, isHovered, alpha);
        }

        public Rectangle GetResetViewButtonRect(Rectangle panelRect) =>
            new Rectangle(panelRect.Right - 45, panelRect.Bottom - 48, 36, 36);

        public void DrawResetViewButton(SpriteBatch spriteBatch, Rectangle panelRect, Vector2 directionToCenter, bool isHovered, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            Rectangle btnRect = GetResetViewButtonRect(panelRect);
            Vector2 center = btnRect.Center.ToVector2();

            DrawSmallIconButton(spriteBatch, btnRect, isHovered, alpha, null);

            float rot = directionToCenter.ToRotation();
            Color arrowC = isHovered ? Color.White : new Color(255, 240, 210);

            spriteBatch.Draw(px, center, new Rectangle(0, 0, 14, 2), arrowC * alpha, rot,
                new Vector2(0, 1f), 1f, SpriteEffects.None, 0f);

            float headSz = 7f + MathF.Sin(Main.GameUpdateCount * 0.15f) * 1.5f;
            Vector2 headPos = center + new Vector2(7, 0).RotatedBy(rot);
            spriteBatch.Draw(px, headPos, new Rectangle(0, 0, (int)headSz, 2), arrowC * alpha,
                rot + MathHelper.Pi * 0.75f, new Vector2(0, 1), 1f, SpriteEffects.None, 0f);
            spriteBatch.Draw(px, headPos, new Rectangle(0, 0, (int)headSz, 2), arrowC * alpha,
                rot - MathHelper.Pi * 0.75f, new Vector2(0, 1), 1f, SpriteEffects.None, 0f);

            spriteBatch.Draw(px, center, new Rectangle(0, 0, 3, 3), new Color(255, 100, 60) * alpha,
                0f, new Vector2(1.5f, 1.5f), 1f, SpriteEffects.None, 0f);
        }

        private void DrawSmallIconButton(SpriteBatch sb, Rectangle rect, bool hover, float alpha,
            Action<SpriteBatch, Vector2, float> drawIcon) {
            Texture2D px = VaultAsset.placeholder2.Value;
            bool nightMode = QuestLog.Instance?.NightMode ?? false;

            Rectangle shadow = rect;
            shadow.Offset(1, 2);
            sb.Draw(px, shadow, Color.Black * (0.35f * alpha));

            Color topC = nightMode
                ? (hover ? new Color(55, 90, 145) : new Color(35, 55, 90))
                : (hover ? new Color(130, 90, 50) : new Color(90, 60, 35));
            Color botC = nightMode
                ? (hover ? new Color(30, 50, 90) : new Color(20, 30, 55))
                : (hover ? new Color(80, 50, 25) : new Color(55, 35, 18));

            for (int i = 0; i < 4; i++) {
                float t = i / 4f;
                float t2 = (i + 1f) / 4f;
                int y1 = rect.Y + (int)(t * rect.Height);
                int y2 = rect.Y + (int)(t2 * rect.Height);
                sb.Draw(px, new Rectangle(rect.X, y1, rect.Width, Math.Max(1, y2 - y1)),
                    Color.Lerp(topC, botC, t) * alpha);
            }

            sb.Draw(px, new Rectangle(rect.X + 2, rect.Y, rect.Width - 4, 1),
                Color.White * (0.18f * alpha));

            Color edgeC = hover ? Color.White : (nightMode ? new Color(80, 140, 210) : new Color(210, 140, 70));
            sb.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 1), edgeC * (0.5f * alpha));
            sb.Draw(px, new Rectangle(rect.X, rect.Y, 1, rect.Height), edgeC * (0.3f * alpha));
            sb.Draw(px, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), Color.Black * (0.25f * alpha));
            sb.Draw(px, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), Color.Black * (0.35f * alpha));

            drawIcon?.Invoke(sb, rect.Center.ToVector2(), alpha);
        }

        private void DrawPageIcon(SpriteBatch sb, Vector2 center, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            Color ic = new Color(230, 225, 210) * alpha;
            sb.Draw(px, center + new Vector2(2, -2), new Rectangle(0, 0, 14, 18),
                ic * 0.4f, 0f, new Vector2(7, 9), 1f, SpriteEffects.None, 0f);
            sb.Draw(px, center + new Vector2(-1, 1), new Rectangle(0, 0, 14, 18),
                ic * 0.8f, 0f, new Vector2(7, 9), 1f, SpriteEffects.None, 0f);
            sb.Draw(px, center + new Vector2(-1, 1) + new Vector2(-3, -4),
                new Rectangle(0, 0, 7, 1), Color.Black * (0.4f * alpha), 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
            sb.Draw(px, center + new Vector2(-1, 1) + new Vector2(-3, 0),
                new Rectangle(0, 0, 7, 1), Color.Black * (0.4f * alpha), 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
            sb.Draw(px, center + new Vector2(-1, 1) + new Vector2(-3, 4),
                new Rectangle(0, 0, 5, 1), Color.Black * (0.4f * alpha), 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
        }

        private void DrawDayNightIcon(SpriteBatch sb, Vector2 center, float alpha, bool isNight, Rectangle btnBg) {
            Texture2D px = VaultAsset.placeholder2.Value;
            Color ic = new Color(255, 245, 200) * alpha;

            if (isNight) {
                sb.Draw(px, center, new Rectangle(0, 0, 14, 14), ic, 0f,
                    new Vector2(7, 7), 1f, SpriteEffects.None, 0f);
                Color bgC = new Color(20, 30, 55);
                sb.Draw(px, center + new Vector2(4, -2), new Rectangle(0, 0, 12, 12),
                    bgC * alpha, 0f, new Vector2(6, 6), 1f, SpriteEffects.None, 0f);
            }
            else {
                sb.Draw(px, center, new Rectangle(0, 0, 8, 8), ic, 0f,
                    new Vector2(4, 4), 1f, SpriteEffects.None, 0f);
                float time = Main.GameUpdateCount * 0.02f;
                for (int i = 0; i < 8; i++) {
                    float rot = i * MathHelper.PiOver4 + time;
                    Vector2 off = new Vector2(0, -8).RotatedBy(rot);
                    sb.Draw(px, center + off, new Rectangle(0, 0, 2, 3), ic * 0.8f,
                        rot, new Vector2(1, 1.5f), 1f, SpriteEffects.None, 0f);
                }
            }
        }

        #endregion
    }
}
