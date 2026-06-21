using CalamityOverhaul.Content.EntrustManager;
using CalamityOverhaul.Content.UIs.UIEffect;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.Narrative.Scenarios.Helen.Quest
{
    /// <summary>海洋追踪窗口样式</summary>
    internal class OceanTrackerWidgetStyle : IEntrustTrackerWidgetStyle
    {
        #region 色板

        private static readonly Color AbyssBg = new(3, 12, 22);
        private static readonly Color MidDepth = new(8, 32, 52);
        private static readonly Color CausticBright = new(60, 180, 220);
        private static readonly Color CausticDim = new(25, 90, 130);
        private static readonly Color BioGlow = new(40, 220, 190);
        private static readonly Color SurfaceShimmer = new(100, 215, 255);
        private static readonly Color FrameEdge = new(35, 130, 175);
        private static readonly Color TitleCyan = new(160, 240, 255);
        private static readonly Color TextSea = new(140, 210, 235);
        private static readonly Color AccentWave = new(55, 170, 220);
        private static readonly Color BarFillDeep = new(20, 100, 160);
        private static readonly Color BarFillBright = new(70, 200, 245);

        #endregion

        private float pulse;
        private float wave;
        private float causticPhase;
        private float shaderTime;
        private const int ShaderEdgePad = 8;

        public void Update(Rectangle widgetRect, float slideProgress) {
            pulse += 0.028f;
            wave += 0.022f;
            causticPhase += 0.015f;
            shaderTime += 0.016f;
            if (pulse > MathHelper.TwoPi) pulse -= MathHelper.TwoPi;
            if (wave > MathHelper.TwoPi) wave -= MathHelper.TwoPi;
            if (causticPhase > MathHelper.TwoPi * 3f) causticPhase -= MathHelper.TwoPi * 3f;
            if (shaderTime > 10000f) shaderTime -= 10000f;
        }

        public void Reset() { pulse = wave = causticPhase = shaderTime = 0f; }

        public void DrawWidgetBackground(SpriteBatch sb, Rectangle rect, float alpha) {
            var px = VaultAsset.placeholder2.Value;
            var uv = new Rectangle(0, 0, 1, 1);

            //软投影（偏右下，模拟水下漫射）
            sb.Draw(px, new Rectangle(rect.X + 2, rect.Y + 3, rect.Width, rect.Height),
                uv, Color.Black * (alpha * 0.4f));

            if (SeaShaderPanel.Available) {
                float pulse01 = MathF.Sin(pulse * 1.5f) * 0.5f + 0.5f;
                float shimmer = MathF.Sin(wave * 1.2f) * 0.5f + 0.5f;
                Color tint = Color.Lerp(new Color(210, 235, 255), Color.White, shimmer * 0.25f);
                SeaShaderPanel.Draw(sb, rect, alpha * 0.97f, pulse01, shaderTime, ShaderEdgePad, tint);
                sb.Draw(px, rect, uv, BioGlow * (alpha * 0.018f * pulse01));
                return;
            }

            //纵向深海渐变（由深到浅微曲线）
            int segs = 14;
            for (int i = 0; i < segs; i++) {
                float t = i / (float)segs;
                float t2 = (i + 1) / (float)segs;
                int y1 = rect.Y + (int)(t * rect.Height);
                int y2 = rect.Y + (int)(t2 * rect.Height);
                if (y2 <= y1) continue;
                //深度曲线：底部更深，顶部1/3微亮
                float depth = t < 0.35f ? (1f - t / 0.35f * 0.15f) : (0.85f + (t - 0.35f) / 0.65f * 0.15f);
                Color c = Color.Lerp(AbyssBg, MidDepth, (1f - depth) * 2f) * (alpha * 0.92f);
                sb.Draw(px, new Rectangle(rect.X, y1, rect.Width, y2 - y1), uv, c);
            }

            //焦散光斑层（3个漂移椭圆形亮区）
            for (int b = 0; b < 3; b++) {
                float bx = 0.2f + b * 0.3f + MathF.Sin(causticPhase + b * 1.9f) * 0.12f;
                float by = 0.3f + MathF.Cos(causticPhase * 0.8f + b * 2.5f) * 0.2f;
                int cx = rect.X + (int)(bx * rect.Width);
                int cy = rect.Y + (int)(by * rect.Height);
                float intensity = MathF.Sin(causticPhase * 1.3f + b * 1.1f) * 0.5f + 0.5f;
                int rw = 18 + (int)(intensity * 10f);
                int rh = 8 + (int)(intensity * 5f);
                sb.Draw(px, new Rectangle(cx - rw / 2, cy - rh / 2, rw, rh),
                    uv, CausticDim * (alpha * 0.06f * intensity));
            }

            //深层生物发光呼吸（整体低频脉冲）
            float bioBreath = MathF.Sin(pulse * 1.5f) * 0.5f + 0.5f;
            sb.Draw(px, rect, uv, BioGlow * (alpha * 0.025f * bioBreath));
        }

        public void DrawWidgetFrame(SpriteBatch sb, Rectangle rect, float alpha) {
            var px = VaultAsset.placeholder2.Value;
            var uv = new Rectangle(0, 0, 1, 1);
            float p = MathF.Sin(pulse * 2f) * 0.3f + 0.7f;
            Color edge = FrameEdge * (alpha * p);

            //非对称边框，左强右渐隐
            //左侧实线（全高，较粗）
            sb.Draw(px, new Rectangle(rect.X, rect.Y + 2, 2, rect.Height - 4), uv, edge * 0.9f);
            //左侧辉光衬线
            sb.Draw(px, new Rectangle(rect.X + 2, rect.Y + 4, 1, rect.Height - 8), uv, edge * 0.2f);

            //顶部：左起60%长度的顶线
            int topLineW = (int)(rect.Width * 0.6f);
            sb.Draw(px, new Rectangle(rect.X, rect.Y, topLineW, 1), uv, edge * 0.85f);
            //顶线末端渐隐（4px）
            for (int i = 0; i < 4; i++) {
                float fade = 1f - i / 4f;
                sb.Draw(px, new Rectangle(rect.X + topLineW + i, rect.Y, 1, 1),
                    uv, edge * (0.85f * fade));
            }

            //左上角 L 标
            sb.Draw(px, new Rectangle(rect.X, rect.Y, 8, 2), uv, SurfaceShimmer * (alpha * 0.6f));
            sb.Draw(px, new Rectangle(rect.X, rect.Y, 2, 8), uv, SurfaceShimmer * (alpha * 0.6f));

            //右侧虚线段（仅上半部分）
            int dashH = rect.Height / 2;
            for (int y = rect.Y + 4; y < rect.Y + dashH; y += 5) {
                int h = Math.Min(2, rect.Y + dashH - y);
                float fade = 1f - (float)(y - rect.Y) / dashH;
                sb.Draw(px, new Rectangle(rect.Right - 1, y, 1, h),
                    uv, edge * (0.35f * fade));
            }

            //底部波浪条，不封底
            int waveW = (int)(rect.Width * 0.7f);
            int waveBaseX = rect.X + (int)(rect.Width * 0.15f);
            int waveY = rect.Bottom - 2;
            int segments = waveW / 3;
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                int wx = waveBaseX + (int)(t * waveW);
                int wy = waveY + (int)(MathF.Sin(wave * 3f + t * MathHelper.TwoPi * 2.5f) * 1.5f);
                float fade = MathF.Sin(t * MathHelper.Pi);
                sb.Draw(px, new Rectangle(wx, wy, 3, 1), uv, AccentWave * (alpha * 0.45f * fade));
            }

            //生物发光粒点（2颗，缓慢漂移）
            for (int d = 0; d < 2; d++) {
                float dx = 0.7f + d * 0.15f + MathF.Sin(causticPhase * 0.6f + d * 3f) * 0.08f;
                float dy = 0.25f + d * 0.35f + MathF.Cos(causticPhase * 0.5f + d * 2f) * 0.06f;
                float glow = MathF.Sin(pulse * 2.5f + d * 1.5f);
                if (glow > 0.3f) {
                    float intensity = (glow - 0.3f) / 0.7f;
                    sb.Draw(px, new Vector2(rect.X + dx * rect.Width, rect.Y + dy * rect.Height), null,
                        BioGlow * (alpha * 0.2f * intensity), 0f, new Vector2(0.5f),
                        new Vector2(2f + intensity * 1.5f), SpriteEffects.None, 0f);
                }
            }
        }

        public void DrawWidgetHeader(SpriteBatch sb, Rectangle headerRect, string title, float alpha) {
            //标题以左侧竖向色带为锚点，CP2077式强调条
            var px = VaultAsset.placeholder2.Value;
            var uv = new Rectangle(0, 0, 1, 1);

            //标题左侧竖向渐变色带
            float p = MathF.Sin(pulse * 2f) * 0.2f + 0.8f;
            for (int y = headerRect.Y + 3; y < headerRect.Bottom - 2; y++) {
                float t = (float)(y - headerRect.Y - 3) / (headerRect.Height - 5);
                Color barC = Color.Lerp(CausticBright, BioGlow, t) * (alpha * 0.7f * p);
                sb.Draw(px, new Rectangle(headerRect.X + 5, y, 2, 1), uv, barC);
            }

            //标题文字
            Vector2 titlePos = new(headerRect.X + 12, headerRect.Y + (headerRect.Height - 14f) / 2f);
            //水下散射辉光（极淡）
            Utils.DrawBorderString(sb, title, titlePos + new Vector2(0, 1), AbyssBg * (alpha * 0.5f), 0.76f);
            Utils.DrawBorderString(sb, title, titlePos, TitleCyan * alpha, 0.76f);

            //标题下方分隔虚线（非实线，避免盒子感）
            int dashY = headerRect.Bottom - 1;
            for (int x = headerRect.X + 10; x < headerRect.Right - 10; x += 7) {
                int w = Math.Min(4, headerRect.Right - 10 - x);
                float t = (float)(x - headerRect.X) / headerRect.Width;
                float fade = MathF.Sin(t * MathHelper.Pi) * 0.5f;
                if (w > 0) sb.Draw(px, new Rectangle(x, dashY, w, 1), uv, FrameEdge * (alpha * fade));
            }
        }

        public void DrawWidgetProgress(SpriteBatch sb, Rectangle barRect, float progress,
            string progressText, float alpha) {
            var px = VaultAsset.placeholder2.Value;
            var uv = new Rectangle(0, 0, 1, 1);

            //进度条底，左右各缩 1px
            sb.Draw(px, new Rectangle(barRect.X + 1, barRect.Y, barRect.Width - 2, barRect.Height),
                uv, AbyssBg * (alpha * 0.85f));

            //填充（渐变+气泡粒感，按3px小段绘制减少DrawCall）
            int fillW = (int)((barRect.Width - 2) * MathHelper.Clamp(progress, 0f, 1f));
            if (fillW > 1) {
                int segW = 3;
                for (int sx = 0; sx < fillW; sx += segW) {
                    int sw = Math.Min(segW, fillW - sx);
                    float t = (float)sx / (barRect.Width - 2);
                    Color c = Color.Lerp(BarFillDeep, BarFillBright, t);
                    float noise = MathF.Sin(causticPhase * 2f + sx * 0.7f) * 0.15f;
                    c = Color.Lerp(c, CausticBright, Math.Max(0f, noise));
                    sb.Draw(px, new Rectangle(barRect.X + 1 + sx, barRect.Y + 1, sw, barRect.Height - 2),
                        uv, c * (alpha * 0.85f));
                }
                //填充前端亮缘
                sb.Draw(px, new Rectangle(barRect.X + 1 + fillW - 1, barRect.Y, 1, barRect.Height),
                    uv, SurfaceShimmer * (alpha * 0.5f));
            }

            //上下薄边线
            sb.Draw(px, new Rectangle(barRect.X + 1, barRect.Y, barRect.Width - 2, 1),
                uv, FrameEdge * (alpha * 0.45f));
            sb.Draw(px, new Rectangle(barRect.X + 1, barRect.Bottom - 1, barRect.Width - 2, 1),
                uv, FrameEdge * (alpha * 0.3f));

            if (!string.IsNullOrEmpty(progressText)) {
                var font = FontAssets.MouseText.Value;
                Vector2 sz = font.MeasureString(progressText) * 0.5f;
                Utils.DrawBorderString(sb, progressText,
                    new Vector2(barRect.X + barRect.Width / 2f - sz.X / 2f,
                        barRect.Y + barRect.Height / 2f - sz.Y / 2f),
                    SurfaceShimmer * alpha, 0.5f);
            }
        }

        public void DrawWidgetDivider(SpriteBatch sb, Vector2 start, Vector2 end, float alpha) {
            var px = VaultAsset.placeholder2.Value;
            //渐变虚线分隔
            float len = (end - start).Length();
            if (len < 1f) return;
            Vector2 dir = Vector2.Normalize(end - start);
            float rot = MathF.Atan2(dir.Y, dir.X);
            for (float c = 0; c < len; c += 6f) {
                float t = c / len;
                float fade = MathF.Sin(t * MathHelper.Pi) * 0.6f;
                float segLen = Math.Min(3f, len - c);
                sb.Draw(px, start + dir * c, new Rectangle(0, 0, 1, 1),
                    CausticDim * (alpha * fade), rot, new Vector2(0, 0.5f),
                    new Vector2(segLen, 1f), SpriteEffects.None, 0f);
            }
        }

        public void DrawWidgetOverlay(SpriteBatch sb, Rectangle rect, float alpha) {
            //表层顶部扫光
            var px = VaultAsset.placeholder2.Value;
            float shimmer = ((wave * 0.4f) % 1f);
            int shimX = rect.X + (int)(shimmer * rect.Width);
            int shimW = (int)(rect.Width * 0.2f);
            for (int dx = 0; dx < shimW; dx++) {
                int x = shimX + dx;
                if (x < rect.X || x >= rect.Right) continue;
                float fade = 1f - (float)dx / shimW;
                fade *= fade;
                sb.Draw(px, new Rectangle(x, rect.Y + 1, 1, 2),
                    new Rectangle(0, 0, 1, 1), SurfaceShimmer * (alpha * fade * 0.06f));
            }
        }

        public Color GetWidgetTitleColor(float alpha) => TitleCyan * alpha;
        public Color GetWidgetTextColor(float alpha) => TextSea * alpha;
        public Color GetWidgetAccentColor(float alpha) => AccentWave * alpha;

        public int? GetPreferredWidth() => 230;
        public int? GetMinHeight() => 80;
        public int? GetIdleCompactHeight(EntrustEntryData entry) {
            if (entry.Progress <= 0f && entry.Status != QuestEntryStatus.Completed)
                return 46;
            return null;
        }
    }
}
