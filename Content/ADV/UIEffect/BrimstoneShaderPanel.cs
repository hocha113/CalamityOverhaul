using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.ADV.UIEffect
{
    /// <summary>
    /// 硫磺火面板着色器，失败时 CPU 降级
    /// </summary>
    internal static class BrimstoneShaderPanel
    {
        public static bool Available => EffectLoader.BrimstoneDialogueBox?.Value != null;

        /// <summary>
        /// 矩形内绘制 BrimstoneDialogueBox.fx 面板
        /// </summary>
        /// <param name="sb">当前已Begin的SpriteBatch</param>
        /// <param name="rect">面板包含边缘的矩形</param>
        /// <param name="alpha">整体透明度</param>
        /// <param name="pulse01">脉动0~1,用于驱动火焰整体节拍</param>
        /// <param name="time">单调递增的着色器时间</param>
        /// <param name="edgePad">面板边缘羽化像素</param>
        /// <param name="tint">最终颜色叠加,可用于hover/选中差异</param>
        public static void Draw(SpriteBatch sb, Rectangle rect, float alpha, float pulse01, float time, int edgePad, Color tint) {
            Effect effect = EffectLoader.BrimstoneDialogueBox?.Value;
            if (effect == null) {
                return;
            }

            Rectangle extRect = rect;
            extRect.Inflate(edgePad, edgePad);

            effect.Parameters["uTime"]?.SetValue(time);
            effect.Parameters["uAlpha"]?.SetValue(alpha);
            effect.Parameters["uResolution"]?.SetValue(new Vector2(extRect.Width, extRect.Height));
            effect.Parameters["uEdgePad"]?.SetValue((float)edgePad);
            effect.Parameters["uInfernoPulse"]?.SetValue(pulse01);

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
