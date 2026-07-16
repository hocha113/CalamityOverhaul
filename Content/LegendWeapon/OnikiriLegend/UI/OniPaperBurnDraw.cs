using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI
{
    /// <summary>
    /// 封印札焚烧 shader 绘制(OniPaperBurn.fx)：纸条整体(纤维/折角/压边)连同
    /// 噪声阈值焚烧一并在 shader 内完成,燃线沿噪声轮廓爬行。<br/>
    /// 失败时由调用方退回 <c>OniBrush.DrawPaperStrip</c> + 逐列焦边
    /// </summary>
    internal static class OniPaperBurnDraw
    {
        public static bool Available => EffectLoader.OniPaperBurn?.Value != null;

        /// <summary>
        /// 绘制一张被烧的纸条。top 为纸条顶部中点,rot 为摆角(纸条沿旋转后的"下"铺开)。<br/>
        /// burn 0~1 为焚烧量。调用方保证当前批为 Deferred+UIScaleMatrix
        /// </summary>
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

            //阴影仍在 shader 外:烧穿的洞不该有影子跟着,纸影由残纸近似(半透黑,同角度微偏)
            sb.Draw(VaultAsset.placeholder2.Value, top, new Rectangle(0, 0, 1, 1), Color.White,
                rot, new Vector2(0.5f, 0f), size, SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.PointClamp, DepthStencilState.None,
                RasterizerState.CullNone, null, Main.UIScaleMatrix);
        }
    }
}
