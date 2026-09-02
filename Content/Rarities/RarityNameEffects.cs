using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using Terraria.UI.Chat;

namespace CalamityOverhaul.Content.Rarities
{
    /// <summary>
    /// 稀有度名称特效的绘制原语。全部在调用方当前的 SpriteBatch（提示框 Deferred+AlphaBlend）里直绘：
    /// 加色一律 A=0，黑底亮度贴图（StarGlow01/SoftGlow）只走 A=0，不画任何暗层
    /// </summary>
    internal static class RarityNameEffects
    {
        private static readonly Rectangle PixelSrc = new(0, 0, 1, 1);
        private static DynamicSpriteFont Font => FontAssets.MouseText.Value;
        private static Texture2D Pixel => VaultAsset.placeholder2.Value;

        /// <summary>客户端总开关，关掉后名称只剩纯色</summary>
        public static bool Enabled => CWRClientConfig.Instance?.RarityTextEffects ?? true;

        /// <summary>自绘面板题行统一入口，非本模组稀有度按原版描边字</summary>
        public static void DrawItemName(SpriteBatch sb, Item item, string text, Vector2 pos, Color color, float scale) {
            if (Enabled && item != null && !item.expert && !item.master
                && RarityLoader.GetRarity(item.rare) is CWRRarity rarity) {
                rarity.DrawName(sb, item, text, pos, color, new Vector2(scale), Main.GlobalTimeWrappedHourly);
                return;
            }
            Utils.DrawBorderString(sb, text, pos, color, scale);
        }

        #region 文本
        /// <summary>原版口径：四向 2px 黑阴影 + 正文</summary>
        public static void DrawPlain(SpriteBatch sb, string text, Vector2 pos, Color color, Vector2 scale) {
            ChatManager.DrawColorCodedStringWithShadow(sb, Font, text, pos, color, 0f, Vector2.Zero, scale);
        }

        public static void DrawText(SpriteBatch sb, string text, Vector2 pos, Color color, Vector2 scale) {
            sb.DrawString(Font, text, pos, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        /// <summary>四向阴影，不含正文</summary>
        public static void DrawShadow(SpriteBatch sb, string text, Vector2 pos, Color color, Vector2 scale, float offset = 2f) {
            foreach (Vector2 dir in ChatManager.ShadowDirections) {
                DrawText(sb, text, pos + dir * offset, color, scale);
            }
        }

        /// <summary>环形描边，不含正文</summary>
        public static void DrawOutline(SpriteBatch sb, string text, Vector2 pos, Color color, Vector2 scale, float radius, int directions = 8) {
            for (int i = 0; i < directions; i++) {
                Vector2 offset = new Vector2(radius, 0f).RotatedBy(MathHelper.TwoPi * i / directions);
                DrawText(sb, text, pos + offset, color, scale);
            }
        }
        #endregion

        #region 逐字布局
        /// <summary>逐字位置表，沿用整段前缀测量保留字距；单例复用不每帧分配</summary>
        public sealed class GlyphLayout
        {
            public int Count;
            public string[] Glyphs = new string[32];
            public Vector2[] Positions = new Vector2[32];
            public float[] Widths = new float[32];
            public float Width;
            public float Height;
            public Vector2 Origin;

            /// <summary>第 i 字中心 X</summary>
            public float CenterX(int i) => Positions[i].X + Widths[i] * 0.5f;

            internal void Ensure(int n) {
                if (Glyphs.Length >= n) {
                    return;
                }
                int size = Math.Max(n, Glyphs.Length * 2);
                Array.Resize(ref Glyphs, size);
                Array.Resize(ref Positions, size);
                Array.Resize(ref Widths, size);
            }
        }

        private static readonly GlyphLayout sharedLayout = new();
        private static readonly Dictionary<char, string> glyphStrings = [];

        private static string GlyphString(char c) {
            if (!glyphStrings.TryGetValue(c, out string s)) {
                s = c.ToString();
                glyphStrings[c] = s;
            }
            return s;
        }

        public static GlyphLayout Layout(string text, Vector2 pos, Vector2 scale) {
            GlyphLayout layout = sharedLayout;
            int n = text.Length;
            layout.Ensure(n);
            layout.Count = n;
            layout.Origin = pos;
            layout.Height = Font.MeasureString(" ").Y * scale.Y;
            float prefix = 0f;
            for (int i = 0; i < n; i++) {
                layout.Glyphs[i] = GlyphString(text[i]);
                layout.Positions[i] = new Vector2(pos.X + prefix, pos.Y);
                float next = Font.MeasureString(text[..(i + 1)]).X * scale.X;
                layout.Widths[i] = MathF.Max(0f, next - prefix);
                prefix = next;
            }
            layout.Width = prefix;
            return layout;
        }

        public static void DrawGlyph(SpriteBatch sb, GlyphLayout layout, int i, Vector2 offset, Color color, Vector2 scale) {
            sb.DrawString(Font, layout.Glyphs[i], layout.Positions[i] + offset, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }
        #endregion

        #region 数值与材质
        /// <summary>确定性哈希 [0,1)</summary>
        public static float Hash01(int a, int b = 0) {
            uint h = (uint)(a * 374761393) ^ (uint)(b * 668265263) ^ 0x9E3779B9u;
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / 16777216f;
        }

        public static float Breath(float time, float period, float min, float max)
            => MathHelper.Lerp(min, max, 0.5f + 0.5f * MathF.Sin(time * MathHelper.TwoPi / period));

        /// <summary>只缩放 RGB，保留 A（mouseTextColor 衰减已在 A 里）</summary>
        public static Color Scale(Color color, float k) {
            return new Color(
                (int)MathHelper.Clamp(color.R * k, 0f, 255f),
                (int)MathHelper.Clamp(color.G * k, 0f, 255f),
                (int)MathHelper.Clamp(color.B * k, 0f, 255f),
                color.A);
        }

        /// <summary>把调色板色按行衰减 fade 压暗（同步 mouseTextColor 呼吸）</summary>
        public static Color Fade(Color palette, float fade) => palette * fade;

        /// <summary>行衰减系数，取自 tML 传入的名称行颜色</summary>
        public static float FadeOf(Color lineColor) => lineColor.A / 255f;

        /// <summary>四芒星光点（StarGlow01 黑底亮度图，恒 A=0 加色）</summary>
        public static void DrawStar(SpriteBatch sb, Vector2 center, float size, Color color, float rotation = 0f) {
            Texture2D tex = CWRAsset.StarGlow01?.Value;
            if (tex == null) {
                return;
            }
            sb.Draw(tex, center, null, color with { A = 0 }, rotation, tex.Size() * 0.5f, size / tex.Width, SpriteEffects.None, 0f);
        }

        /// <summary>柔光点（SoftGlow 黑底亮度图，恒 A=0 加色）</summary>
        public static void DrawMote(SpriteBatch sb, Vector2 center, float size, Color color) {
            Texture2D tex = CWRAsset.SoftGlow?.Value;
            if (tex == null) {
                return;
            }
            sb.Draw(tex, center, null, color with { A = 0 }, 0f, tex.Size() * 0.5f, size / tex.Width, SpriteEffects.None, 0f);
        }

        /// <summary>实心小方屑（真 alpha 像素）</summary>
        public static void DrawFleck(SpriteBatch sb, Vector2 center, float size, Color color) {
            sb.Draw(Pixel, center, PixelSrc, color, 0f, new Vector2(0.5f), size, SpriteEffects.None, 0f);
        }

        /// <summary>水平细线</summary>
        public static void DrawHLine(SpriteBatch sb, Vector2 start, float length, float thickness, Color color) {
            sb.Draw(Pixel, start, PixelSrc, color, 0f, new Vector2(0f, 0.5f), new Vector2(length, thickness), SpriteEffects.None, 0f);
        }
        #endregion
    }
}
