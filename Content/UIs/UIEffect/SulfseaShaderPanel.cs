using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.UIs.UIEffect
{
    /// <summary>硫磺海面板着色器，失败时 CPU 降级</summary>
    internal static class SulfseaShaderPanel
    {
        public static bool Available => EffectLoader.SulfseaPanel?.Value != null;

        /// <summary>矩形内绘制 SulfseaPanel.fx 面板</summary>
        /// <param name="sb">当前已Begin的SpriteBatch</param>
        /// <param name="rect">面板包含边缘的矩形</param>
        /// <param name="alpha">整体透明度</param>
        /// <param name="miasma01">毒雾脉动0~1,用于驱动焦散与内边</param>
        /// <param name="time">单调递增的着色器时间</param>
        /// <param name="edgePad">面板边缘羽化像素</param>
        /// <param name="tint">最终颜色叠加,可用于hover/选中差异</param>
        public static void Draw(SpriteBatch sb, Rectangle rect, float alpha, float miasma01, float time, int edgePad, Color tint) {
            Effect effect = EffectLoader.SulfseaPanel?.Value;
            if (effect == null) {
                return;
            }

            Rectangle extRect = rect;
            extRect.Inflate(edgePad, edgePad);

            effect.Parameters["uTime"]?.SetValue(time);
            effect.Parameters["uAlpha"]?.SetValue(alpha);
            effect.Parameters["uResolution"]?.SetValue(new Vector2(extRect.Width, extRect.Height));
            effect.Parameters["uEdgePad"]?.SetValue((float)edgePad);
            effect.Parameters["uMiasma"]?.SetValue(miasma01);

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
