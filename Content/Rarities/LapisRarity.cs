using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using static CalamityOverhaul.Content.Rarities.RarityNameEffects;

namespace CalamityOverhaul.Content.Rarities
{
    /// <summary>青金，月后档。青金石体，黄铁矿屑偶发闪现</summary>
    internal sealed class LapisRarity : CWRRarity
    {
        public static readonly Color Lapis = new(96, 128, 216);
        public static readonly Color Pyrite = new(255, 214, 120);

        //同时最多闪现的金屑槽位
        private const int FleckSlots = 3;
        //每槽一个周期里金屑可见的占比
        private const float FleckLife = 0.22f;

        public override int Tier => 1;
        public override Color BaseColor => Lapis;

        //与翠玉构成可换铸的两档，再往上各档唯一
        public override int GetPrefixedRarity(int offset, float valueMult) => offset switch {
            -2 => ItemRarityID.Red,
            -1 => ItemRarityID.Purple,
            1 or 2 => ModContent.RarityType<JadeRarity>(),
            _ => Type,
        };

        public override void DrawName(SpriteBatch sb, Item item, string text, Vector2 pos, Color color, Vector2 scale, float time) {
            float fade = FadeOf(color);
            DrawShadow(sb, text, pos, new Color(0, 0, 0, color.A), scale);
            DrawText(sb, text, pos, Scale(color, Breath(time, 3.4f, 0.92f, 1f)), scale);

            GlyphLayout layout = Layout(text, pos, scale);
            Color gold = Fade(Pyrite, fade);
            for (int k = 0; k < FleckSlots; k++) {
                float period = 1.4f + Hash01(k, 11) * 0.9f;
                float cycle = time / period + Hash01(k, 3);
                int index = (int)MathF.Floor(cycle);
                float t = cycle - index;
                if (t > FleckLife) {
                    continue;
                }
                float intensity = MathF.Sin(MathHelper.Pi * t / FleckLife);
                //每个周期换一处落点，纵向限制在字身高度带内
                Vector2 p = new(
                    pos.X + Hash01(index, k * 7 + 1) * layout.Width,
                    pos.Y + layout.Height * (0.3f + 0.4f * Hash01(index, k * 7 + 2)));
                DrawStar(sb, p, 7f + 3f * intensity, gold * (0.55f * intensity));
                DrawFleck(sb, p, 2f * scale.X, gold * intensity);
            }
        }
    }
}
