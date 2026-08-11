using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.UIs.UIEffect
{
    /// <summary>鬼雨叙事面板,失败由调用方CPU降级。调色板见
    /// <see cref="KikasaStoryTheme"/></summary>
    internal static class KikasaShaderPanel
    {
        public static bool Available => EffectLoader.KikasaNarrativePanel?.Value != null;

        /// <summary>画KikasaNarrativePanel.fx</summary>
        /// <param name="sb">已Begin的SpriteBatch</param>
        /// <param name="rect">不含边缘的矩形</param>
        /// <param name="alpha">面板体不透明度</param>
        /// <param name="reveal">开合0~1</param>
        /// <param name="time">着色器时间,单调增</param>
        /// <param name="edgePad">外扩px</param>
        /// <param name="tint">色叠加</param>
        public static void Draw(SpriteBatch sb, Rectangle rect, float alpha, float reveal, float time, int edgePad, Color tint) {
            Effect effect = EffectLoader.KikasaNarrativePanel?.Value;
            if (effect == null) {
                return;
            }

            Rectangle extRect = rect;
            extRect.Inflate(edgePad, edgePad);

            effect.Parameters["uTime"]?.SetValue(time);
            effect.Parameters["uAlpha"]?.SetValue(alpha);
            effect.Parameters["uReveal"]?.SetValue(reveal);
            effect.Parameters["uResolution"]?.SetValue(new Vector2(extRect.Width, extRect.Height));
            effect.Parameters["uEdgePad"]?.SetValue((float)edgePad);
            effect.Parameters["uColVoid"]?.SetValue(KikasaStoryTheme.Void.ToVector3());
            effect.Parameters["uColDeep"]?.SetValue(KikasaStoryTheme.Deep.ToVector3());
            effect.Parameters["uColRain"]?.SetValue(KikasaStoryTheme.Rain.ToVector3());
            effect.Parameters["uColMoon"]?.SetValue(KikasaStoryTheme.Moon.ToVector3());

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
