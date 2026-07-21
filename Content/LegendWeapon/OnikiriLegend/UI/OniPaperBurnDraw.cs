using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI
{
    /// <summary>
    /// 封印札焚烧(OniPaperBurn.fx),纸条+燃线在 shader 内;
    /// 失败退回 <c>OniBrush.DrawPaperStrip</c> + 焦边
    /// </summary>
    internal static class OniPaperBurnDraw
    {
        public static bool Available => EffectLoader.OniPaperBurn?.Value != null;

        /// <summary>烧纸条,top=顶中点,rot=摆角,burn 0~1,批须 Deferred+UIScaleMatrix</summary>
        public static void Draw(SpriteBatch sb, Vector2 top, float rot, Vector2 size, float alpha, float burn, float time) {
            Effect effect = EffectLoader.OniPaperBurn?.Value;
            if (effect == null) {
                return;
            }

            effect.Parameters["uTime"]?.SetValue(time);
            effect.Parameters["uAlpha"]?.SetValue(alpha);
            effect.Parameters["uBurn"]?.SetValue(burn);
            effect.Parameters["uSize"]?.SetValue(size);
            effect.Parameters["uColPaper"]?.SetValue(OnikiriUITheme.Paper.ToVector3());
            effect.Parameters["uColEdge"]?.SetValue(OnikiriUITheme.Deep.ToVector3());
            effect.Parameters["uColChar"]?.SetValue(OnikiriUITheme.Ink.ToVector3());
            effect.Parameters["uColFireDim"]?.SetValue(OnikiriUITheme.BurnDim.ToVector3());
            effect.Parameters["uColFireHot"]?.SetValue(OnikiriUITheme.BurnHot.ToVector3());

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.AnisotropicClamp, DepthStencilState.None,
                RasterizerState.CullNone, effect, Main.UIScaleMatrix);

            //阴影在 shader 外,烧穿洞不带影,残纸半透黑近似
            sb.Draw(VaultAsset.placeholder2.Value, top, new Rectangle(0, 0, 1, 1), Color.White,
                rot, new Vector2(0.5f, 0f), size, SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.PointClamp, DepthStencilState.None,
                RasterizerState.CullNone, null, Main.UIScaleMatrix);
        }
    }
}
