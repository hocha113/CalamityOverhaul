using CalamityOverhaul.Content.EntrustManager;
using CalamityOverhaul.Content.UIs.UIEffect;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace CalamityOverhaul.Content.Narrative.Scenarios.Helen.Quest
{
    /// <summary>海洋委托条目样式</summary>
    internal class OceanEntryStyle : IEntrustEntryStyle
    {
        #region 色板

        //深海到浅海的渐变色阶
        private static readonly Color DeepAbyss = new(2, 10, 22);
        private static readonly Color MidOcean = new(6, 26, 50);
        private static readonly Color ShallowSea = new(14, 48, 78);
        private static readonly Color HoverTint = new(12, 40, 68);
        private static readonly Color SelectedTint = new(20, 55, 85);

        //焦散/水面光效
        private static readonly Color CausticBright = new(55, 195, 240);
        private static readonly Color CausticDim = new(28, 110, 160);
        private static readonly Color FoamWhite = new(175, 225, 245);

        //生物发光
        private static readonly Color BioGlow = new(35, 215, 175);
        private static readonly Color CoralAccent = new(75, 175, 215);
        private static readonly Color WaveCrest = new(85, 205, 250);

        //文字
        private static readonly Color TitleIce = new(170, 235, 255);
        private static readonly Color TitleComplete = new(45, 225, 145);

        #endregion

        private float waveTime;
        private float causticTime;
        private float bubbleTime;
        //着色器专用单调递增时间
        private float shaderTime;
        private const int ShaderEdgePad = 5;

        public void Update() {
            waveTime += 0.025f;
            causticTime += 0.018f;
            bubbleTime += 0.04f;
            shaderTime += 0.016f;
            const float wrap = MathHelper.TwoPi * 4f;
            if (waveTime > wrap) waveTime -= wrap;
            if (causticTime > wrap) causticTime -= wrap;
            if (bubbleTime > 100f) bubbleTime -= 100f;
            if (shaderTime > 10000f) shaderTime -= 10000f;
        }

        public bool DrawEntryBackground(SpriteBatch sb, Rectangle entryRect, EntrustEntryData entry,
            bool isSelected, bool isHovered, float alpha) {
            var px = VaultAsset.placeholder2.Value;
            var uv = new Rectangle(0, 0, 1, 1);

            if (SeaShaderPanel.Available) {
                DrawShaderBackground(sb, entryRect, entry, isSelected, isHovered, alpha);
            }
            else {
                DrawFallbackBackground(sb, entryRect, isSelected, isHovered, alpha);
            }

            //水面波光碎片（顶部边缘间断的白色亮点）
            int step = 3;
            for (int x = 0; x < entryRect.Width; x += step) {
                float shimmer = MathF.Sin(waveTime * 3f + x * 0.08f)
                              * MathF.Sin(waveTime * 1.7f + x * 0.05f);
                if (shimmer > 0.35f) {
                    float si = (shimmer - 0.35f) / 0.65f * 0.18f;
                    sb.Draw(px, new Rectangle(entryRect.X + x, entryRect.Y + 1, step, 1),
                        uv, FoamWhite * (alpha * si));
                }
            }

            //左侧生物发光带（4px宽主色带 + 光晕 + 高亮芯线）
            Color statusC = GetAccentColor(entry.Status, 1f);
            float glowPulse = MathF.Sin(waveTime * 2f) * 0.3f + 0.7f;
            sb.Draw(px, new Rectangle(entryRect.X, entryRect.Y + 1, 4, entryRect.Height - 2),
                uv, statusC * (alpha * glowPulse));
            sb.Draw(px, new Rectangle(entryRect.X + 4, entryRect.Y + 3, 8, entryRect.Height - 6),
                uv, statusC * (alpha * glowPulse * 0.1f));
            sb.Draw(px, new Rectangle(entryRect.X + 1, entryRect.Y + 2, 1, entryRect.Height - 4),
                uv, FoamWhite * (alpha * glowPulse * 0.3f));

            //波浪形上下边框（每3px一段，亮度随正弦变化）
            for (int x = 0; x < entryRect.Width; x += step) {
                float topB = MathF.Sin(waveTime * 1.5f + x * 0.06f) * 0.5f + 0.5f;
                float botB = MathF.Sin(waveTime * 1.2f + x * 0.05f + 2f) * 0.5f + 0.5f;
                int w = Math.Min(step, entryRect.Width - x);
                sb.Draw(px, new Rectangle(entryRect.X + x, entryRect.Y, w, 1),
                    uv, Color.Lerp(CausticDim, WaveCrest, topB) * (alpha * 0.5f));
                sb.Draw(px, new Rectangle(entryRect.X + x, entryRect.Bottom - 1, w, 1),
                    uv, Color.Lerp(CausticDim, CoralAccent, botB) * (alpha * 0.3f));
            }

            return true;
        }

        private void DrawShaderBackground(SpriteBatch sb, Rectangle entryRect, EntrustEntryData entry,
            bool isSelected, bool isHovered, float alpha) {
            float pulse01 = MathF.Sin(causticTime * 1.7f) * 0.5f + 0.5f;
            Color statusTint = GetAccentColor(entry.Status, 1f);
            Color stateTint = isSelected ? SelectedTint
                : isHovered ? HoverTint
                : Color.White;

            Color tint = Color.Lerp(new Color(220, 238, 255), statusTint, 0.08f);
            if (isSelected || isHovered) {
                tint = Color.Lerp(tint, stateTint, isSelected ? 0.22f : 0.14f);
            }

            SeaShaderPanel.Draw(sb, entryRect, alpha * (isSelected ? 1.0f : 0.93f), pulse01, shaderTime, ShaderEdgePad, tint);
        }

        private void DrawFallbackBackground(SpriteBatch sb, Rectangle entryRect,
            bool isSelected, bool isHovered, float alpha) {
            var px = VaultAsset.placeholder2.Value;
            var uv = new Rectangle(0, 0, 1, 1);

            //多段海水渐变，叠加双层波浪色相偏移
            int segs = 16;
            for (int i = 0; i < segs; i++) {
                float t = i / (float)segs;
                float t2 = (i + 1) / (float)segs;
                int y1 = entryRect.Y + (int)(t * entryRect.Height);
                int y2 = entryRect.Y + (int)(t2 * entryRect.Height);
                if (y2 <= y1) continue;

                Color baseC = isSelected ? SelectedTint
                    : isHovered ? HoverTint
                    : Color.Lerp(DeepAbyss, MidOcean, t);

                float w1 = MathF.Sin(waveTime * 1.6f + t * 5f) * 0.5f + 0.5f;
                float w2 = MathF.Sin(waveTime * 0.9f + t * 3f + 1.5f) * 0.5f + 0.5f;
                Color waveTint = Color.Lerp(baseC, ShallowSea, w1 * 0.25f + w2 * 0.15f);

                //焦散亮带：两个正弦相乘后取正值再平方，产生随机感的亮条
                float caustic = MathF.Sin(causticTime * 2.2f + t * 8f)
                              * MathF.Sin(causticTime * 1.3f + t * 4f);
                caustic = MathF.Max(0f, caustic);
                caustic *= caustic;
                Color c = Color.Lerp(waveTint, CausticDim, caustic * 0.35f) * (alpha * 0.95f);

                sb.Draw(px, new Rectangle(entryRect.X, y1, entryRect.Width, y2 - y1), uv, c);
            }

            //焦散光斑：3个椭圆形亮区在背景上缓慢漂移
            for (int p = 0; p < 3; p++) {
                float fx = MathF.Sin(causticTime * (0.7f + p * 0.3f) + p * 2.1f) * 0.5f + 0.5f;
                float fy = MathF.Sin(causticTime * (0.5f + p * 0.2f) + p * 1.4f) * 0.5f + 0.5f;
                float bright = MathF.Sin(causticTime * 1.5f + p * 1.8f);
                bright = MathF.Max(0f, bright) * 0.07f;
                if (bright < 0.005f) continue;

                int cx = entryRect.X + (int)(fx * entryRect.Width * 0.7f + entryRect.Width * 0.15f);
                int cy = entryRect.Y + (int)(fy * entryRect.Height * 0.5f + entryRect.Height * 0.25f);
                int rw = (int)(entryRect.Width * (0.12f + p * 0.04f));
                int rh = rw / 3;

                //由外到内3层递增亮度模拟径向衰减
                for (int layer = 2; layer >= 0; layer--) {
                    float lt = (2 - layer) / 2f;
                    int lw = rw - layer * (rw / 3);
                    int lh = rh - layer * (rh / 3);
                    if (lw < 1 || lh < 1) continue;
                    Rectangle gr = Rectangle.Intersect(
                        new Rectangle(cx - lw, cy - lh, lw * 2, lh * 2), entryRect);
                    if (gr.Width > 0 && gr.Height > 0)
                        sb.Draw(px, gr, uv, CausticBright * (alpha * bright * (0.3f + lt * 0.7f)));
                }
            }
        }

        public float DrawEntryIcon(SpriteBatch sb, Vector2 titlePos, EntrustEntryData entry, float alpha) {
            var px = VaultAsset.placeholder2.Value;
            float cx = titlePos.X + 8f;
            float cy = titlePos.Y + 9f;
            float pulse = MathF.Sin(waveTime * 1.5f + 1f) * 0.3f + 0.7f;

            //扩散涟漪环（周期性扩大并淡出）
            float ripplePhase = (waveTime * 0.8f) % MathHelper.TwoPi;
            float rippleScale = 4f + ripplePhase / MathHelper.TwoPi * 8f;
            float rippleAlpha = 1f - ripplePhase / MathHelper.TwoPi;
            sb.Draw(px, new Vector2(cx, cy), null, CausticBright * (alpha * rippleAlpha * 0.2f),
                MathHelper.PiOver4, new Vector2(0.5f), new Vector2(rippleScale), SpriteEffects.None, 0f);

            //水滴菱形主体
            sb.Draw(px, new Vector2(cx, cy), null, CoralAccent * (alpha * pulse),
                MathHelper.PiOver4, new Vector2(0.5f), new Vector2(5.5f), SpriteEffects.None, 0f);

            //内核高光
            sb.Draw(px, new Vector2(cx, cy), null, FoamWhite * (alpha * pulse * 0.45f),
                MathHelper.PiOver4, new Vector2(0.5f), new Vector2(2.5f), SpriteEffects.None, 0f);

            //生物发光光晕
            sb.Draw(px, new Vector2(cx, cy), null, BioGlow * (alpha * pulse * 0.12f),
                MathHelper.PiOver4, new Vector2(0.5f), new Vector2(11f), SpriteEffects.None, 0f);

            return 22f;
        }

        public void DrawEntryOverlay(SpriteBatch sb, Rectangle entryRect, EntrustEntryData entry, float alpha) {
            var px = VaultAsset.placeholder2.Value;

            //上浮气泡（5个，确定性种子驱动位置，随时间循环上升）
            for (int b = 0; b < 5; b++) {
                float seed = b * 37.7f;
                float bx = entryRect.X + (int)((seed * 73.1f) % entryRect.Width);
                float speed = 0.3f + (seed % 1f) * 0.4f;
                float by = entryRect.Bottom - ((bubbleTime * speed + seed * 11.3f) % entryRect.Height);

                if (by < entryRect.Y || by > entryRect.Bottom) continue;

                float t = (by - entryRect.Y) / entryRect.Height;
                float bAlpha = MathF.Sin(t * MathHelper.Pi) * 0.25f;
                float size = 1f + (b % 3) * 0.5f;

                sb.Draw(px, new Vector2(bx, by), null, WaveCrest * (alpha * bAlpha),
                    0f, new Vector2(0.5f), new Vector2(size), SpriteEffects.None, 0f);
                //气泡高光点
                sb.Draw(px, new Vector2(bx - 0.5f, by - 0.5f), null, FoamWhite * (alpha * bAlpha * 0.4f),
                    0f, new Vector2(0.5f), new Vector2(size * 0.35f), SpriteEffects.None, 0f);
            }

            //右下角深海微光
            float deepGlow = MathF.Sin(waveTime * 0.8f) * 0.5f + 0.5f;
            int gs = 28;
            Rectangle glowRect = Rectangle.Intersect(
                new Rectangle(entryRect.Right - gs - 4, entryRect.Bottom - gs / 2 - 2, gs, gs / 2),
                entryRect);
            if (glowRect.Width > 0 && glowRect.Height > 0)
                sb.Draw(px, glowRect, new Rectangle(0, 0, 1, 1), BioGlow * (alpha * deepGlow * 0.06f));
        }

        public Color GetAccentColor(QuestEntryStatus status, float alpha) {
            return status switch {
                QuestEntryStatus.Completed => BioGlow * alpha,
                QuestEntryStatus.Failed => new Color(220, 70, 60) * alpha,
                QuestEntryStatus.Suspended => new Color(100, 130, 150) * alpha,
                QuestEntryStatus.Tracked => WaveCrest * alpha,
                _ => CoralAccent * alpha,
            };
        }

        public Color GetTitleColor(QuestEntryStatus status, float alpha) {
            return status switch {
                QuestEntryStatus.Completed => TitleComplete * (alpha * 0.85f),
                _ => TitleIce * alpha,
            };
        }

        public int? GetCustomEntryHeight() => null;

        public void Reset() {
            waveTime = 0f;
            causticTime = 0f;
            bubbleTime = 0f;
            shaderTime = 0f;
        }
    }
}
