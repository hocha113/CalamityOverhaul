using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using static CalamityOverhaul.Content.Rarities.RarityNameEffects;

namespace CalamityOverhaul.Content.Rarities
{
    /// <summary>星银，至尊灾厄后档。靛蓝描边的银白字，薄膜虹彩沿字滑过，偶发星芒</summary>
    internal sealed class StarsilverRarity : CWRRarity
    {
        public static readonly Color Starsilver = new(222, 230, 244);
        public static readonly Color Indigo = new(40, 44, 90);

        private const int GlintSlots = 2;
        private const float GlintLife = 0.28f;

        public override int Tier => 5;
        public override Color BaseColor => Starsilver;

        public override void DrawName(SpriteBatch sb, Item item, string text, Vector2 pos, Color color, Vector2 scale, float time) {
            float fade = FadeOf(color);
            //靛蓝描边是与普通白色物品区分的关键，不随特效开关以外的东西变
            DrawOutline(sb, text, pos, Fade(Indigo, fade), scale, 1.2f);

            GlyphLayout layout = Layout(text, pos, scale);
            for (int i = 0; i < layout.Count; i++) {
                float cx = layout.CenterX(i);
                //低饱和光谱色沿 X 缓慢换相，只在高光波峰处显色
                float hue = ((cx * 0.003f + time * 0.08f) % 1f + 1f) % 1f;
                Color spectral = Fade(Main.hslToRgb(hue, 0.7f, 0.72f), fade);
                float w = MathF.Pow(0.5f + 0.5f * MathF.Sin(cx * 0.045f - time * 2f), 5f);
                DrawGlyph(sb, layout, i, Vector2.Zero, Color.Lerp(color, spectral, 0.75f * w), scale);
            }

            for (int k = 0; k < GlintSlots; k++) {
                float period = 2.4f + Hash01(k, 13) * 1.6f;
                float cycle = time / period + Hash01(k, 17);
                int index = (int)MathF.Floor(cycle);
                float t = cycle - index;
                if (t > GlintLife) {
                    continue;
                }
                float intensity = MathF.Sin(MathHelper.Pi * t / GlintLife);
                Vector2 p = new(
                    pos.X + Hash01(index, k * 5 + 1) * layout.Width,
                    pos.Y + layout.Height * (0.2f + 0.5f * Hash01(index, k * 5 + 2)));
                DrawStar(sb, p, 9f + 4f * intensity, Color.White * (0.85f * intensity * fade), intensity * 0.6f);
            }
        }
    }
}
