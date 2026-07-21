using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.UIs.UIEffect
{
    /// <summary>SHPC赛博面板,失败由调用方CPU降级</summary>
    internal static class CyberShaderPanel
    {
        public static bool Available => EffectLoader.CyberPanel?.Value != null;

        /// <summary>画CyberPanel.fx</summary>
        /// <param name="sb">已Begin的SpriteBatch</param>
        /// <param name="rect">含边缘矩形</param>
        /// <param name="time">着色器时间,扫线/故障</param>
        /// <param name="edgePad">羽化px,六角溢出</param>
        /// <param name="tint">色叠加</param>
        public static void Draw(SpriteBatch sb, Rectangle rect, float alpha, float time, int edgePad, Color tint) {
            Effect effect = EffectLoader.CyberPanel?.Value;
            if (effect == null) {
                return;
            }

            Rectangle extRect = rect;
            extRect.Inflate(edgePad, edgePad);

            effect.Parameters["uTime"]?.SetValue(time);
            effect.Parameters["uAlpha"]?.SetValue(alpha);
            effect.Parameters["uResolution"]?.SetValue(new Vector2(extRect.Width, extRect.Height));
            effect.Parameters["uEdgePad"]?.SetValue((float)edgePad);

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
