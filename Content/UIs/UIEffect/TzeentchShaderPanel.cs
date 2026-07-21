using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.UIs.UIEffect
{
    /// <summary>奸奇面板着色器,失败则CPU降级</summary>
    internal static class TzeentchShaderPanel
    {
        public static bool Available => EffectLoader.TzeentchPanel?.Value != null;

        /// <summary>画TzeentchPanel.fx</summary>
        /// <param name="sb">已Begin的SpriteBatch</param>
        /// <param name="rect">含边缘矩形</param>
        /// <param name="warp01">变数0~1</param>
        /// <param name="time">着色器时间,单调增</param>
        /// <param name="edgePad">边缘羽化px</param>
        /// <param name="tint">色叠加</param>
        public static void Draw(SpriteBatch sb, Rectangle rect, float alpha, float warp01, float time, int edgePad, Color tint) {
            Effect effect = EffectLoader.TzeentchPanel?.Value;
            if (effect == null) {
                return;
            }

            Rectangle extRect = rect;
            extRect.Inflate(edgePad, edgePad);

            effect.Parameters["uTime"]?.SetValue(time);
            effect.Parameters["uAlpha"]?.SetValue(alpha);
            effect.Parameters["uResolution"]?.SetValue(new Vector2(extRect.Width, extRect.Height));
            effect.Parameters["uEdgePad"]?.SetValue((float)edgePad);
            effect.Parameters["uMiasma"]?.SetValue(warp01);

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.AnisotropicClamp, DepthStencilState.None,
                RasterizerState.CullNone, effect, Main.UIScaleMatrix);

            sb.Draw(VaultAsset.placeholder2.Value, extRect, tint);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.PointClamp, DepthStencilState.None,
                RasterizerState.CullNone, null, Main.UIScaleMatrix);
        }
    }
}
