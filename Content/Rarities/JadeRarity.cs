using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using static CalamityOverhaul.Content.Rarities.RarityNameEffects;

namespace CalamityOverhaul.Content.Rarities
{
    /// <summary>翠玉，亵渎天神后档。玉体带厚度，一道润光缓慢横扫</summary>
    internal sealed class JadeRarity : CWRRarity
    {
        public static readonly Color Jade = new(88, 186, 140);
        public static readonly Color JadeDeep = new(26, 82, 62);
        public static readonly Color JadeSheen = new(206, 244, 224);

        //润光高斯半宽(px)与一次横扫周期(s)
        private const float SheenSigma = 26f;
        private const float SheenPeriod = 4.2f;

        public override int Tier => 2;
        public override Color BaseColor => Jade;

        public override int GetPrefixedRarity(int offset, float valueMult) => offset switch {
            -2 => ItemRarityID.Purple,
            -1 => ModContent.RarityType<LapisRarity>(),
            _ => Type,
        };

        public override void DrawName(SpriteBatch sb, Item item, string text, Vector2 pos, Color color, Vector2 scale, float time) {
            float fade = FadeOf(color);
            DrawShadow(sb, text, pos, new Color(0, 0, 0, color.A), scale);
            //正文下压一层深玉，读作有厚度的玉料而非平涂
            DrawText(sb, text, pos + new Vector2(0f, 1.5f * scale.Y), Fade(JadeDeep, fade) * 0.85f, scale);

            GlyphLayout layout = Layout(text, pos, scale);
            float span = layout.Width + SheenSigma * 4f;
            float sweepX = pos.X - SheenSigma * 2f + (time / SheenPeriod % 1f) * span;
            Color sheen = Fade(JadeSheen, fade);
            for (int i = 0; i < layout.Count; i++) {
                float d = (layout.CenterX(i) - sweepX) / SheenSigma;
                float w = MathF.Exp(-d * d);
                DrawGlyph(sb, layout, i, Vector2.Zero, Color.Lerp(color, sheen, 0.6f * w), scale);
            }
        }
    }
}
