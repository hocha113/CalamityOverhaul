using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using static CalamityOverhaul.Content.Rarities.RarityNameEffects;

namespace CalamityOverhaul.Content.Rarities
{
    /// <summary>鎏金，犽戎后档。金描边裹暗褐芯，镜面高光逐字掠过，偶发整体亮闪</summary>
    internal sealed class GiltRarity : CWRRarity
    {
        public static readonly Color Gilt = new(240, 194, 96);
        public static readonly Color GiltEdge = new(255, 214, 120);
        public static readonly Color GiltCore = new(74, 40, 14);
        public static readonly Color Specular = new(255, 246, 220);

        //整体亮闪的周期(s)与占比
        private const float FlashPeriod = 7.3f;
        private const float FlashWidth = 0.05f;

        public override int Tier => 4;
        public override Color BaseColor => Gilt;

        public override void DrawName(SpriteBatch sb, Item item, string text, Vector2 pos, Color color, Vector2 scale, float time) {
            float fade = FadeOf(color);
            Color edge = Color.Lerp(color, Fade(GiltEdge, fade), 0.5f);
            DrawOutline(sb, text, pos, edge, scale, 2f);

            Color specular = Fade(Specular, fade);
            float flashT = (time / FlashPeriod + 0.37f) % 1f;
            float flash = flashT < FlashWidth ? MathF.Sin(MathHelper.Pi * flashT / FlashWidth) : 0f;
            Color core = Color.Lerp(Fade(GiltCore, fade), specular, flash * 0.85f);
            DrawText(sb, text, pos, core, scale);

            GlyphLayout layout = Layout(text, pos, scale);
            for (int i = 0; i < layout.Count; i++) {
                //pow(sin,120) 只在极窄区间可见，其余字符直接跳过
                float s = (MathF.Sin(layout.CenterX(i) * 0.02f + time * -1.5f) + 1f) * 0.5f;
                float strength = MathF.Pow(s, 120f);
                if (strength < 1f / 255f) {
                    continue;
                }
                DrawGlyph(sb, layout, i, Vector2.Zero, specular * strength, scale);
            }
        }
    }
}
