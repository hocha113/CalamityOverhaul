using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI
{
    /// <summary>鬼切 UI 笔触原语,叙事皮肤与点鬼簿共用</summary>
    internal static class OniBrush
    {
        private static Texture2D Pixel => VaultAsset.placeholder2.Value;
        private static readonly Rectangle PixelSrc = new(0, 0, 1, 1);

        /// <summary>刀痕,sweep 0~1 截断(hover 扫入)</summary>
        public static void DrawTaperedSlash(SpriteBatch spriteBatch, Vector2 start, Vector2 end, float maxThick, float bow, float alpha, float sweep = 1f) {
            Vector2 edge = end - start;
            float fullLen = edge.Length();
            if (fullLen < 2f || alpha <= 0.01f || sweep <= 0.02f) {
                return;
            }

            Vector2 dir = edge / fullLen;
            Vector2 perp = new(dir.Y, -dir.X);
            float len = fullLen * MathHelper.Clamp(sweep, 0f, 1f);
            const int Seg = 14;
            float segLen = len / Seg;
            float rot = dir.ToRotation();

            for (int i = 0; i < Seg; i++) {
                float tm = (i + 0.5f) / Seg;
                //形状参数按完整长度归一,截断只影响画到哪(扫线时笔锋在前沿)
                float tShape = tm * MathHelper.Clamp(sweep, 0f, 1f);
                float profile = (float)Math.Pow(Math.Sin(tShape * Math.PI), 0.62);
                float thick = maxThick * Math.Max(profile, 0.12f);
                Vector2 pos = start + dir * (segLen * i) + perp * ((float)Math.Sin(tShape * Math.PI) * bow);
                Color col = Color.Lerp(OnikiriUITheme.Dark, OnikiriUITheme.Bright, profile) * alpha;
                spriteBatch.Draw(Pixel, pos, PixelSrc, col, rot, new Vector2(0f, 0.5f), new Vector2(segLen + 0.7f, thick), SpriteEffects.None, 0f);

                //前 45% 叠一条更细的白热芯,像刚划开还没冷却的部分
                if (tShape > 0.04f && tShape < 0.45f) {
                    float core = (float)Math.Sin((tShape - 0.04f) / 0.41f * Math.PI);
                    spriteBatch.Draw(Pixel, pos, PixelSrc, OnikiriUITheme.HotWhite * (alpha * 0.75f * core), rot, new Vector2(0f, 0.5f), new Vector2(segLen + 0.7f, thick * 0.4f), SpriteEffects.None, 0f);
                }
            }
        }

        /// <summary>朱印方章,integrity&lt;1 褪色裂痕</summary>
        public static void DrawSealGlyph(SpriteBatch spriteBatch, Vector2 center, float size, float alpha, float rotation = 0f, float integrity = 1f) {
            if (size < 1f || alpha <= 0.01f) {
                return;
            }

            Vector2 half = new(0.5f);
            Color sealBody = Color.Lerp(OnikiriUITheme.Dark, OnikiriUITheme.Seal, 0.35f + integrity * 0.65f);

            spriteBatch.Draw(Pixel, center + new Vector2(1f, 1.4f), PixelSrc, OnikiriUITheme.Dark * (alpha * 0.6f), rotation, half, new Vector2(size), SpriteEffects.None, 0f);
            spriteBatch.Draw(Pixel, center, PixelSrc, OnikiriUITheme.Deep * (alpha * 0.95f), rotation, half, new Vector2(size + 2f), SpriteEffects.None, 0f);
            spriteBatch.Draw(Pixel, center, PixelSrc, sealBody * alpha, rotation, half, new Vector2(size), SpriteEffects.None, 0f);

            //刻痕:一横一竖一点,偏移随章体一起旋转
            Color carve = OnikiriUITheme.Paper * (alpha * (0.5f + integrity * 0.42f));
            Vector2 hOff = new Vector2(0f, -size * 0.24f).RotatedBy(rotation);
            Vector2 vOff = new Vector2(-size * 0.08f, size * 0.10f).RotatedBy(rotation);
            Vector2 dOff = new Vector2(size * 0.24f, size * 0.24f).RotatedBy(rotation);
            spriteBatch.Draw(Pixel, center + hOff, PixelSrc, carve, rotation, half, new Vector2(size * 0.54f, 1.6f), SpriteEffects.None, 0f);
            spriteBatch.Draw(Pixel, center + vOff, PixelSrc, carve, rotation, half, new Vector2(1.6f, size * 0.46f), SpriteEffects.None, 0f);
            spriteBatch.Draw(Pixel, center + dOff, PixelSrc, carve * 0.9f, rotation, half, new Vector2(2.1f, 2.1f), SpriteEffects.None, 0f);

            //裂痕:一道斜贯的暗线,integrity 越低越长越深
            if (integrity < 0.999f) {
                float crack = 1f - integrity;
                Vector2 crackDir = (rotation + 1.05f).ToRotationVector2();
                Vector2 crackStart = center - crackDir * size * (0.25f + crack * 0.30f);
                spriteBatch.Draw(Pixel, crackStart, PixelSrc, OnikiriUITheme.Ink * (alpha * (0.5f + crack * 0.45f)),
                    rotation + 1.05f, new Vector2(0f, 0.5f), new Vector2(size * (0.5f + crack * 0.6f), 1.3f), SpriteEffects.None, 0f);
            }
        }

        /// <summary>双纸垂,落点与 shader 绸带下垂同源</summary>
        public static void DrawShide(SpriteBatch spriteBatch, Rectangle rect, float alpha, float swayTimer) {
            DrawSingleShide(spriteBatch, rect, 0.10f, 15f, alpha, swayTimer, 0f);
            DrawSingleShide(spriteBatch, rect, 0.78f, 18f, alpha * 0.92f, swayTimer, 2.1f);
        }

        /// <summary>单纸垂,u=顶沿归一,折面明暗+折缝高光</summary>
        public static void DrawSingleShide(SpriteBatch sb, Rectangle rect, float u, float length, float alpha, float swayTimer, float phase) {
            float sag = (float)Math.Sin(u * Math.PI) * 3.4f;
            Vector2 anchor = new(rect.X + rect.Width * u, rect.Y - 6f + sag);

            //双谐波摆 + 低频阵风:风起时摆幅涨,平息后归于微晃
            float gust = (float)Math.Pow(Math.Max(0f, Math.Sin(swayTimer * 0.23f + phase * 0.7f)), 3.0) * 0.6f;
            float sway = (float)Math.Sin(swayTimer * 1.5f + phase) * 0.085f * (1f + gust)
                + (float)Math.Sin(swayTimer * 3.7f + phase * 1.3f) * 0.026f;

            //绳结
            sb.Draw(Pixel, anchor, PixelSrc, OnikiriUITheme.Deep * (alpha * 0.9f), sway * 0.5f + MathHelper.PiOver4, new Vector2(0.5f), new Vector2(4.2f, 4.2f), SpriteEffects.None, 0f);

            //三段之字折纸;虚拟光源在左上
            Vector2 lightDir = Vector2.Normalize(new Vector2(-0.42f, -1f));
            Vector2 pos = anchor + new Vector2(0f, 1.5f);
            float segLen = length / 3f;
            const float zig = 0.46f;
            for (int i = 0; i < 3; i++) {
                float lean = (i % 2 == 0 ? zig : -zig) * 0.9f;
                float rot = MathHelper.PiOver2 + lean + sway * (0.5f + i * 0.45f);
                Vector2 dir = rot.ToRotationVector2();
                Vector2 size = new(segLen + 1.2f, 4.6f - i * 0.5f);

                //折面明暗:面法线与光向的点积,交替的折面自然一亮一暗,并随摆动呼吸
                Vector2 normal = (rot - MathHelper.PiOver2).ToRotationVector2();
                float lit = 0.72f + 0.30f * Math.Max(0f, Vector2.Dot(normal, lightDir));
                Color face = OnikiriUITheme.Paper * (alpha * 0.85f * lit);

                sb.Draw(Pixel, pos + new Vector2(0.8f, 0.8f), PixelSrc, OnikiriUITheme.Dark * (alpha * 0.45f), rot, new Vector2(0f, 0.5f), size, SpriteEffects.None, 0f);
                sb.Draw(Pixel, pos, PixelSrc, face, rot, new Vector2(0f, 0.5f), size, SpriteEffects.None, 0f);
                //折缝高光:段起点一线,纸脊接住光
                if (i > 0) {
                    sb.Draw(Pixel, pos, PixelSrc, OnikiriUITheme.HotWhite * (alpha * 0.22f * lit), rot + MathHelper.PiOver2,
                        new Vector2(0.5f), new Vector2(size.Y * 0.9f, 1f), SpriteEffects.None, 0f);
                }
                pos += dir * segLen * 0.9f;
            }
        }

        /// <summary>纸条,top=顶中点,沿 rot 的下方向铺</summary>
        public static void DrawPaperStrip(SpriteBatch sb, Vector2 top, float rot, Vector2 size, float alpha, float sheenPhase) {
            Vector2 down = (MathHelper.PiOver2 + rot).ToRotationVector2();
            Vector2 side = rot.ToRotationVector2();
            Vector2 center = top + down * (size.Y * 0.5f);
            Vector2 half = new(0.5f);

            //阴影
            sb.Draw(Pixel, center + new Vector2(1.5f, 2f), PixelSrc, OnikiriUITheme.Dark * (alpha * 0.5f), rot, half, size, SpriteEffects.None, 0f);

            //纵向三段明暗:纸从光里垂下来,顶承光底沉影
            Span<(float f0, float f1, float lit)> bands = [(0f, 0.34f, 1.05f), (0.34f, 0.72f, 0.97f), (0.72f, 1f, 0.88f)];
            foreach ((float f0, float f1, float lit) in bands) {
                Vector2 bandCenter = top + down * (size.Y * (f0 + f1) * 0.5f);
                Vector2 bandSize = new(size.X, size.Y * (f1 - f0) + 0.6f);
                sb.Draw(Pixel, bandCenter, PixelSrc, OnikiriUITheme.Paper * (alpha * 0.9f * lit), rot, half, bandSize, SpriteEffects.None, 0f);
            }

            //上折角
            sb.Draw(Pixel, top + down * 3f, PixelSrc, OnikiriUITheme.TextDim * (alpha * 0.5f), rot, half, new Vector2(size.X, 6f), SpriteEffects.None, 0f);

            //双侧深红压边
            sb.Draw(Pixel, center - side * (size.X * 0.5f - 1f), PixelSrc, OnikiriUITheme.Deep * (alpha * 0.5f), rot + MathHelper.PiOver2, half, new Vector2(size.Y, 1.4f), SpriteEffects.None, 0f);
            sb.Draw(Pixel, center + side * (size.X * 0.5f - 1f), PixelSrc, OnikiriUITheme.Deep * (alpha * 0.5f), rot + MathHelper.PiOver2, half, new Vector2(size.Y, 1.4f), SpriteEffects.None, 0f);

            //光泽带:一条极淡的亮痕沿纸面缓移,纸在光里轻轻转
            float sheenT = sheenPhase - (float)Math.Floor(sheenPhase);
            Vector2 sheenCenter = top + down * (size.Y * MathHelper.Lerp(0.12f, 0.88f, sheenT));
            float sheenA = (float)Math.Sin(sheenT * Math.PI);
            sb.Draw(Pixel, sheenCenter, PixelSrc, OnikiriUITheme.HotWhite * (alpha * 0.10f * sheenA), rot, half, new Vector2(size.X - 2f, 5f), SpriteEffects.None, 0f);
        }

        /// <summary>绘马挂绳+流苏(弹窗)</summary>
        public static void DrawHangingKnot(SpriteBatch spriteBatch, Rectangle rect, float alpha, float swayTimer) {
            Vector2 knot = new(rect.Center.X, rect.Y - 15f);
            Color rope = OnikiriUITheme.Deep * (alpha * 0.8f);
            Color ropeFade = OnikiriUITheme.Dark * (alpha * 0.25f);
            DrawGradientLine(spriteBatch, new Vector2(rect.X + 14f, rect.Y + 1f), knot, ropeFade, rope, 1.4f);
            DrawGradientLine(spriteBatch, new Vector2(rect.Right - 14f, rect.Y + 1f), knot, ropeFade, rope, 1.4f);

            spriteBatch.Draw(Pixel, knot, PixelSrc, OnikiriUITheme.Seal * alpha, MathHelper.PiOver4, new Vector2(0.5f), new Vector2(5f, 5f), SpriteEffects.None, 0f);

            float sway = (float)Math.Sin(swayTimer * 2.4f) * 0.22f;
            float tasselRot = MathHelper.PiOver2 + sway;
            Vector2 tasselEnd = knot + tasselRot.ToRotationVector2() * 10f;
            DrawGradientLine(spriteBatch, knot, tasselEnd, OnikiriUITheme.Bright * (alpha * 0.75f), OnikiriUITheme.Deep * (alpha * 0.1f), 1.6f);
        }

        /// <summary>两端渐变的直线段</summary>
        public static void DrawGradientLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color startColor, Color endColor, float thickness) {
            Vector2 edge = end - start;
            float len = edge.Length();
            if (len < 1f) {
                return;
            }
            float rot = edge.ToRotation();
            const int Seg = 8;
            float segLen = len / Seg;
            for (int i = 0; i < Seg; i++) {
                float t = (i + 0.5f) / Seg;
                Color col = Color.Lerp(startColor, endColor, t);
                Vector2 pos = start + edge * (i / (float)Seg);
                spriteBatch.Draw(Pixel, pos, PixelSrc, col, rot, new Vector2(0f, 0.5f), new Vector2(segLen + 0.6f, thickness), SpriteEffects.None, 0f);
            }
        }

        /// <summary>焦边,intensity 0~1 炭化高与火苗密度</summary>
        public static void DrawCharredEdge(SpriteBatch sb, Rectangle rect, float intensity, float time, float alpha) {
            if (intensity <= 0.01f || alpha <= 0.01f) {
                return;
            }

            int step = 3;
            for (int x = rect.X; x < rect.Right; x += step) {
                //确定性 hash:每列炭高稳定,火苗相位稳定
                float h = Hash01(x * 7 + rect.Y * 131);
                float charH = (2f + h * 5f) * intensity;
                //炭黑参差
                sb.Draw(Pixel, new Rectangle(x, (int)(rect.Bottom - charH), step, (int)charH + 1), PixelSrc,
                    OnikiriUITheme.Ink * (alpha * 0.9f));
                //焦褐过渡
                sb.Draw(Pixel, new Rectangle(x, (int)(rect.Bottom - charH - 2f), step, 2), PixelSrc,
                    OnikiriUITheme.Dark * (alpha * 0.75f * intensity));

                //青焰苗:只在部分列上,随时间低闪
                if (h > 0.62f) {
                    float flick = (float)Math.Sin(time * (3.2f + h * 2.4f) + x * 1.7f) * 0.5f + 0.5f;
                    float flameH = (2.5f + h * 4f) * intensity * (0.4f + flick * 0.6f);
                    Vector2 flamePos = new(x + step * 0.5f, rect.Bottom - charH + 1f);
                    sb.Draw(Pixel, flamePos, PixelSrc, OnikiriUITheme.GhostDim * (alpha * 0.55f * flick),
                        0f, new Vector2(0.5f, 1f), new Vector2(step - 0.5f, flameH), SpriteEffects.None, 0f);
                    sb.Draw(Pixel, flamePos, PixelSrc, OnikiriUITheme.GhostFire * (alpha * 0.7f * flick),
                        0f, new Vector2(0.5f, 1f), new Vector2(1.4f, flameH * 0.6f), SpriteEffects.None, 0f);
                }
            }
        }

        /// <summary>背光径向辉,A=0 预乘加法</summary>
        public static void DrawBacklight(SpriteBatch sb, Vector2 center, float radius, Color color, float alpha) {
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 origin = glow.Size() * 0.5f;
            float scale = radius * 2f / glow.Width;
            Color add = new Color(color.R, color.G, color.B, 0);
            sb.Draw(glow, center, null, add * (alpha * 0.55f), 0f, origin, scale, SpriteEffects.None, 0f);
            sb.Draw(glow, center, null, add * (alpha * 0.35f), 0f, origin, scale * 0.55f, SpriteEffects.None, 0f);
        }

        /// <summary>确定性 0~1 hash</summary>
        public static float Hash01(int n) {
            unchecked {
                n = n * 374761393 + 668265263;
                n = (n ^ (n >> 13)) * 1274126177;
                return ((n ^ (n >> 16)) & 0x7FFFFFFF) / (float)int.MaxValue;
            }
        }

        /// <summary>含 CJK 则名录竖排</summary>
        public static bool ContainsCJK(string text) {
            if (string.IsNullOrEmpty(text)) {
                return false;
            }
            foreach (char c in text) {
                if ((c >= 0x4E00 && c <= 0x9FFF) || (c >= 0x3040 && c <= 0x30FF) || (c >= 0x3400 && c <= 0x4DBF)) {
                    return true;
                }
            }
            return false;
        }

    }
}
