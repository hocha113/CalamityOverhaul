using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.ADV.UIEffect
{
    /// <summary>
    /// SHPC赛博朋克风格面板着色器辅助绘制<br/>
    /// 复用CyberPanel.fx,统一调用流程,失败时由调用方走CPU降级
    /// </summary>
    internal static class CyberShaderPanel
    {
        public static bool Available => EffectLoader.CyberPanel?.Value != null;

        /// <summary>
        /// 在指定矩形内绘制SHPC风格面板
        /// 调用前需保证当前SpriteBatch已开启,内部会切换到Immediate应用着色器,再恢复Deferred
        /// </summary>
        /// <param name="sb">当前已Begin的SpriteBatch</param>
        /// <param name="rect">面板包含边缘的矩形</param>
        /// <param name="alpha">整体透明度</param>
        /// <param name="time">单调递增的着色器时间(驱动扫描线/扫掠光/故障)</param>
        /// <param name="edgePad">面板边缘羽化像素(给六角溢出留空间)</param>
        /// <param name="tint">最终颜色叠加,可用于hover/选中差异</param>
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
