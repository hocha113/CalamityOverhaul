using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.UIs.WeaponSkills
{
    /// <summary>
    /// 技能按钮 HUD 的 1px 矢量笔刷,按钮机枢与武器图标共用
    /// <br/>全部真透明载体,暗色能真正压暗;辉光走 <see cref="DrawGlow"/>(A=0 染色)
    /// </summary>
    internal static class WeaponSkillBrush
    {
        private static readonly Rectangle PixelSrc = new(0, 0, 1, 1);
        public static Texture2D Pixel => VaultAsset.placeholder2?.Value;

        /// <summary>两点线段</summary>
        public static void Line(SpriteBatch sb, Vector2 a, Vector2 b, Color color, float thickness) {
            Texture2D pixel = Pixel;
            if (pixel == null) {
                return;
            }
            Vector2 seg = b - a;
            float len = seg.Length();
            if (len < 0.01f) {
                return;
            }
            sb.Draw(pixel, a, PixelSrc, color, seg.ToRotation(),
                new Vector2(0f, 0.5f), new Vector2(len, thickness), SpriteEffects.None, 0f);
        }

        public static void FillRect(SpriteBatch sb, Rectangle rect, Color color) {
            Texture2D pixel = Pixel;
            if (pixel == null) {
                return;
            }
            sb.Draw(pixel, rect, PixelSrc, color);
        }

        public static void StrokeRect(SpriteBatch sb, Rectangle rect, int line, Color color) {
            FillRect(sb, new Rectangle(rect.X, rect.Y, rect.Width, line), color);
            FillRect(sb, new Rectangle(rect.X, rect.Bottom - line, rect.Width, line), color);
            FillRect(sb, new Rectangle(rect.X, rect.Y + line, line, rect.Height - line * 2), color);
            FillRect(sb, new Rectangle(rect.Right - line, rect.Y + line, line, rect.Height - line * 2), color);
        }

        /// <summary>折线圆弧,1px 笔按段铺</summary>
        public static void DrawArc(SpriteBatch sb, Vector2 center, float radius,
            float thickness, Color color, float from, float to, int segments) {
            Texture2D pixel = Pixel;
            if (pixel == null || segments < 2 || radius <= 0.5f || to - from < 0.001f) {
                return;
            }
            float step = (to - from) / segments;
            Vector2 prev = center + from.ToRotationVector2() * radius;
            for (int i = 1; i <= segments; i++) {
                Vector2 next = center + (from + step * i).ToRotationVector2() * radius;
                Vector2 seg = next - prev;
                float len = seg.Length();
                if (len > 0.01f) {
                    sb.Draw(pixel, prev, PixelSrc, color, seg.ToRotation(),
                        new Vector2(0f, 0.5f), new Vector2(len + 0.6f, thickness),
                        SpriteEffects.None, 0f);
                }
                prev = next;
            }
        }

        public static void DrawRing(SpriteBatch sb, Vector2 center, float radius,
            float thickness, Color color, int segments)
            => DrawArc(sb, center, radius, thickness, color, 0f, MathHelper.TwoPi, segments);

        /// <summary>扫线实心圆,2px 行填充</summary>
        public static void DrawFilledCircle(SpriteBatch sb, Vector2 center, float radius, Color color) {
            Texture2D pixel = Pixel;
            if (pixel == null || radius < 1f) {
                return;
            }
            const float Step = 2f;
            for (float y = -radius; y <= radius; y += Step) {
                float halfW = MathF.Sqrt(MathF.Max(radius * radius - y * y, 0f));
                if (halfW < 0.5f) {
                    continue;
                }
                sb.Draw(pixel, new Vector2(center.X - halfW, center.Y + y), PixelSrc, color, 0f,
                    new Vector2(0f, 0.5f), new Vector2(halfW * 2f, Step + 0.4f),
                    SpriteEffects.None, 0f);
            }
        }

        /// <summary>柔光点:SoftGlow 黑底灰度,A=0 染色在 AlphaBlend 下读作加法</summary>
        public static void DrawGlow(SpriteBatch sb, Vector2 center, float radius, Color color, float alpha) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null || alpha <= 0.01f || radius <= 1f) {
                return;
            }
            Color c = color;
            c.A = 0;
            sb.Draw(glow, center, null, c * alpha, 0f,
                glow.Size() * 0.5f, radius * 2f / glow.Width, SpriteEffects.None, 0f);
        }
    }
}
