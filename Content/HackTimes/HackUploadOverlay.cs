using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.HackTimes
{
    /// <summary>上传进度头顶圆环</summary>
    internal class HackUploadOverlay
    {

        private float pulseTimer;

        public void Update() {
            pulseTimer += 0.016f;
        }

        /// <summary>绘制上传进度圆环</summary>
        public void Draw(SpriteBatch sb, int npcIndex, float progress, bool completed, float alpha) {
            if (npcIndex < 0 || npcIndex >= Main.maxNPCs) return;

            NPC npc = Main.npc[npcIndex];
            if (!npc.active) return;

            Texture2D px = CWRAsset.Placeholder_White?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (px == null) return;

            Vector2 npcScreen = npc.Center - Main.screenPosition;
            float ringRadius = Math.Max(npc.width, npc.height) * 0.6f + 24f;
            float ringY = npcScreen.Y - npc.height * 0.5f - 20f;
            Vector2 ringCenter = new(npcScreen.X, ringY);

            if (completed) {
                DrawCompletedRing(sb, px, glow, ringCenter, ringRadius, alpha);
            }
            else {
                DrawProgressRing(sb, px, glow, ringCenter, ringRadius, progress, alpha);
            }

            //进度百分比文字
            string text = completed ? "BREACH OK" : $"{(int)(progress * 100)}%";
            Color textColor = completed
                ? HackTheme.Accent * alpha
                : HackTheme.Uploading * alpha;
            Vector2 textSize = Terraria.GameContent.FontAssets.MouseText.Value.MeasureString(text) * 0.3f;
            Vector2 textPos = new(ringCenter.X - textSize.X * 0.5f, ringCenter.Y - textSize.Y * 0.5f);
            Utils.DrawBorderString(sb, text, textPos, textColor, 0.3f);
        }

        /// <summary>
        /// 绘制圆弧进度环
        /// </summary>
        private void DrawProgressRing(SpriteBatch sb, Texture2D px, Texture2D glow,
            Vector2 center, float radius, float progress, float alpha) {

            int totalSegments = 48;
            int filledSegments = (int)(totalSegments * progress);
            float startAngle = -MathHelper.PiOver2;

            for (int i = 0; i < totalSegments; i++) {
                float angle = startAngle + i * MathHelper.TwoPi / totalSegments;
                float nextAngle = startAngle + (i + 1) * MathHelper.TwoPi / totalSegments;
                float midAngle = (angle + nextAngle) * 0.5f;

                Vector2 pos = center + new Vector2(MathF.Cos(midAngle), MathF.Sin(midAngle)) * radius;

                bool isFilled = i < filledSegments;

                if (isFilled) {
                    //已上传部分用琥珀色
                    Color segColor = HackTheme.Uploading * (alpha * 0.7f);
                    sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), segColor,
                        midAngle, new Vector2(0.5f, 0.5f), new Vector2(5f, 2f), SpriteEffects.None, 0);

                    //填充前端发光
                    if (i == filledSegments - 1 && glow != null) {
                        Color tipGlow = HackTheme.ProgressGlow * (alpha * 0.35f);
                        tipGlow.A = 0;
                        sb.Draw(glow, pos, null, tipGlow, 0, glow.Size() / 2, 0.08f, SpriteEffects.None, 0);
                    }
                }
                else {
                    //未上传部分用暗色
                    Color dimColor = HackTheme.Border * (alpha * 0.25f);
                    sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), dimColor,
                        midAngle, new Vector2(0.5f, 0.5f), new Vector2(4f, 1.5f), SpriteEffects.None, 0);
                }
            }
        }

        /// <summary>
        /// 绘制完成状态的脉冲环
        /// </summary>
        private void DrawCompletedRing(SpriteBatch sb, Texture2D px, Texture2D glow,
            Vector2 center, float radius, float alpha) {

            float pulse = 0.7f + 0.3f * MathF.Sin(pulseTimer * 6f);
            int segments = 48;

            for (int i = 0; i < segments; i++) {
                float angle = -MathHelper.PiOver2 + i * MathHelper.TwoPi / segments;
                float nextAngle = -MathHelper.PiOver2 + (i + 1) * MathHelper.TwoPi / segments;
                float midAngle = (angle + nextAngle) * 0.5f;

                Vector2 pos = center + new Vector2(MathF.Cos(midAngle), MathF.Sin(midAngle)) * radius;
                Color segColor = HackTheme.Accent * (alpha * 0.8f * pulse);
                sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), segColor,
                    midAngle, new Vector2(0.5f, 0.5f), new Vector2(5f, 2f), SpriteEffects.None, 0);
            }

            //中心扩散光
            if (glow != null) {
                Color centerGlow = HackTheme.Accent * (alpha * 0.15f * pulse);
                centerGlow.A = 0;
                sb.Draw(glow, center, null, centerGlow, 0, glow.Size() / 2, radius / 30f, SpriteEffects.None, 0);
            }
        }
    }
}
