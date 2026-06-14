using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.MurasamaLegend.TrialQuests
{
    /// <summary>
    /// 鬼妖村正委托面板shader辅助，复用MurasamaPhantomPanel.fx
    /// Begin/End统一切换，失败由调用方CPU降级
    /// </summary>
    internal static class MurasamaPhantomShaderPanel
    {
        public static bool Available => EffectLoader.MurasamaPhantomPanel?.Value != null;

        /// <summary>
        /// 在指定矩形内绘制鬼妖村正风格血气面板
        /// </summary>
        /// <param name="sb">已 Begin 的 UI SpriteBatch</param>
        /// <param name="rect">面板矩形(不含羽化边)</param>
        /// <param name="alpha">整体透明度</param>
        /// <param name="pulse01">脉动 0~1</param>
        /// <param name="time">单调递增着色器时间(避免噪声跳变)</param>
        /// <param name="edgePad">边缘羽化像素</param>
        /// <param name="variant">0=条目 1=追踪窗口</param>
        /// <param name="intensity">0~1 强度(hover/select/Tracked 时升高)</param>
        /// <param name="accent">状态强调色(从 RGBA 解析)</param>
        public static void Draw(SpriteBatch sb, Rectangle rect, float alpha,
            float pulse01, float time, int edgePad,
            float variant, float intensity, Color accent) {
            Effect effect = EffectLoader.MurasamaPhantomPanel?.Value;
            if (effect == null) {
                return;
            }

            Rectangle extRect = rect;
            extRect.Inflate(edgePad, edgePad);

            Vector3 accentVec = new(accent.R / 255f, accent.G / 255f, accent.B / 255f);

            effect.Parameters["uTime"]?.SetValue(time);
            effect.Parameters["uAlpha"]?.SetValue(alpha);
            effect.Parameters["uResolution"]?.SetValue(new Vector2(extRect.Width, extRect.Height));
            effect.Parameters["uEdgePad"]?.SetValue((float)edgePad);
            effect.Parameters["uVariant"]?.SetValue(variant);
            effect.Parameters["uIntensity"]?.SetValue(intensity);
            effect.Parameters["uPulse"]?.SetValue(pulse01);
            effect.Parameters["uAccent"]?.SetValue(accentVec);

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.AnisotropicClamp, DepthStencilState.None,
                RasterizerState.CullNone, effect, Main.UIScaleMatrix);

            sb.Draw(VaultAsset.placeholder2.Value, extRect, Color.White);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.PointClamp, DepthStencilState.None,
                RasterizerState.CullNone, null, Main.UIScaleMatrix);
        }
    }
}
