using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.MainMenus.Characters
{
    /// <summary>码头程序化绘制笔刷，1px 白像素为载体</summary>
    internal static class CharacterDockRenderer
    {
        private static Texture2D Pixel => VaultAsset.placeholder2.Value;
        private static readonly Rectangle unit = new(0, 0, 1, 1);

        public static void DrawLine(SpriteBatch sb, Vector2 a, Vector2 b, Color color, float thickness = 1f) {
            Vector2 diff = b - a;
            float length = diff.Length();
            if (length < 0.5f) {
                return;
            }
            sb.Draw(Pixel, a, unit, color, diff.ToRotation(),
                new Vector2(0f, 0.5f), new Vector2(length, thickness), SpriteEffects.None, 0f);
        }

        /// <summary>切角八边形描线框，cut 为角斜切 px</summary>
        public static void DrawCutFrame(SpriteBatch sb, Rectangle rect, float cut, Color color, float thickness = 1f) {
            float l = rect.X, r = rect.Right, t = rect.Y, b = rect.Bottom;
            Vector2 p1 = new(l + cut, t);
            Vector2 p2 = new(r - cut, t);
            Vector2 p3 = new(r, t + cut);
            Vector2 p4 = new(r, b - cut);
            Vector2 p5 = new(r - cut, b);
            Vector2 p6 = new(l + cut, b);
            Vector2 p7 = new(l, b - cut);
            Vector2 p8 = new(l, t + cut);
            DrawLine(sb, p1, p2, color, thickness);
            DrawLine(sb, p2, p3, color, thickness);
            DrawLine(sb, p3, p4, color, thickness);
            DrawLine(sb, p4, p5, color, thickness);
            DrawLine(sb, p5, p6, color, thickness);
            DrawLine(sb, p6, p7, color, thickness);
            DrawLine(sb, p7, p8, color, thickness);
            DrawLine(sb, p8, p1, color, thickness);
        }

        /// <summary>切角八边形填充，三条矩形近似，角部斜缝由描线盖住</summary>
        public static void DrawCutFill(SpriteBatch sb, Rectangle rect, int cut, Color color) {
            sb.Draw(Pixel, new Rectangle(rect.X, rect.Y + cut, rect.Width, rect.Height - cut * 2), unit, color);
            sb.Draw(Pixel, new Rectangle(rect.X + cut, rect.Y, rect.Width - cut * 2, cut), unit, color);
            sb.Draw(Pixel, new Rectangle(rect.X + cut, rect.Bottom - cut, rect.Width - cut * 2, cut), unit, color);
        }

        /// <summary>底缘亮色描光，多 pass 增宽降 alpha 的亮线配方</summary>
        public static void DrawBottomGlow(SpriteBatch sb, Rectangle rect, Color accent, float alpha) {
            int b = rect.Bottom;
            sb.Draw(Pixel, new Rectangle(rect.X + 4, b - 1, rect.Width - 8, 2), unit, accent * (alpha * 0.8f));
            sb.Draw(Pixel, new Rectangle(rect.X + 8, b - 2, rect.Width - 16, 4), unit, accent * (alpha * 0.28f));
            sb.Draw(Pixel, new Rectangle(rect.X + 14, b - 3, rect.Width - 28, 7), unit, accent * (alpha * 0.1f));
        }

        /// <summary>菱形销钉</summary>
        public static void DrawDiamond(SpriteBatch sb, Vector2 center, float size, Color color) {
            sb.Draw(Pixel, center, unit, color, MathHelper.PiOver4,
                new Vector2(0.5f, 0.5f), size, SpriteEffects.None, 0f);
        }

        /// <summary>立绘本体，紧贴投影 + 呼吸描边 + 主体，绘制矩形与命中矩形同一份</summary>
        public static void DrawPortrait(SpriteBatch sb, Texture2D tex, Vector2 topLeft, float scale,
            float alpha, Color glow, float pulse01, float time) {
            //紧贴 4px 投影，单层不做同心羽化
            sb.Draw(tex, topLeft + new Vector2(4f, 4f), null, new Color(10, 5, 5) * (alpha * 0.3f),
                0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

            Color rim = glow * (alpha * 0.07f * pulse01);
            for (int i = 0; i < 4; i++) {
                Vector2 offset = (MathHelper.TwoPi * i / 4f + time).ToRotationVector2() * 3f;
                sb.Draw(tex, topLeft + offset, null, rim, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }

            sb.Draw(tex, topLeft, null, Color.White * alpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        /// <summary>缓出三次方</summary>
        public static float EaseOutCubic(float t) => 1f - MathF.Pow(1f - Math.Clamp(t, 0f, 1f), 3f);
    }
}
