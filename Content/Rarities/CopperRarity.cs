using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using static CalamityOverhaul.Content.Rarities.RarityNameEffects;

namespace CalamityOverhaul.Content.Rarities
{
    /// <summary>赤铜，神明吞噬者后档。热铜逐字热浪起伏，偶发火星上弹</summary>
    internal sealed class CopperRarity : CWRRarity
    {
        public static readonly Color Copper = new(226, 128, 84);
        public static readonly Color CopperHot = new(255, 176, 118);
        public static readonly Color SparkHot = new(255, 214, 150);
        public static readonly Color SparkCool = new(196, 70, 40);

        private const int SparkSlots = 2;
        //火星存活占周期比例；重力 px/s²
        private const float SparkLife = 0.5f;
        private const float Gravity = 70f;

        public override int Tier => 3;
        public override Color BaseColor => Copper;

        public override void DrawName(SpriteBatch sb, Item item, string text, Vector2 pos, Color color, Vector2 scale, float time) {
            float fade = FadeOf(color);
            DrawShadow(sb, text, pos, new Color(0, 0, 0, color.A), scale);

            GlyphLayout layout = Layout(text, pos, scale);
            Color hot = Fade(CopperHot, fade);
            for (int i = 0; i < layout.Count; i++) {
                //≤0.7px 的上下起伏沿字错相，阴影不随动，人眼读作热浪而非抖动
                float dy = MathF.Sin(time * 5.5f + i * 1.15f) * 0.7f * scale.Y;
                float heat = 0.5f + 0.5f * MathF.Sin(time * 3.1f + i * 0.8f);
                DrawGlyph(sb, layout, i, new Vector2(0f, dy), Color.Lerp(color, hot, heat * 0.35f), scale);
            }

            Color sparkHot = Fade(SparkHot, fade);
            Color sparkCool = Fade(SparkCool, fade);
            for (int k = 0; k < SparkSlots; k++) {
                float period = 0.95f + Hash01(k, 5) * 0.7f;
                float cycle = time / period + Hash01(k, 9);
                int index = (int)MathF.Floor(cycle);
                float t = cycle - index;
                if (t > SparkLife) {
                    continue;
                }
                float age = t * period;
                float x0 = pos.X + Hash01(index, k * 3 + 1) * layout.Width;
                float y0 = pos.Y + layout.Height * 0.82f;
                float vx = (Hash01(index, k * 3 + 2) - 0.5f) * 26f;
                float vy = -(34f + 16f * Hash01(index, k * 3 + 3));
                Vector2 p = new(x0 + vx * age, y0 + vy * age + 0.5f * Gravity * age * age);
                float alive = 1f - t / SparkLife;
                Color c = Color.Lerp(sparkHot, sparkCool, 1f - alive);
                DrawFleck(sb, p, 1.6f * scale.X, c * alive);
                //沿速度反向拖一格残影
                Vector2 velocity = new(vx, vy + Gravity * age);
                DrawFleck(sb, p - velocity * 0.03f, 1.2f * scale.X, c * (alive * 0.5f));
            }
        }
    }
}
