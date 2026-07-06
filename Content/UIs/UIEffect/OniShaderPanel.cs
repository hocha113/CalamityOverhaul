using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.Onikiris.CrimsonRendSlashs;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.UIs.UIEffect
{
    /// <summary>鬼切叙事面板着色器绘制,失败时由调用方 CPU 降级。调色板从
    /// <see cref="CrimsonSlashRenderer"/> 取用,保证与武器特效同源</summary>
    internal static class OniShaderPanel
    {
        public static bool Available => EffectLoader.OniNarrativePanel?.Value != null;

        /// <summary>矩形内绘制 OniNarrativePanel.fx 面板</summary>
        /// <param name="sb">当前已Begin的SpriteBatch</param>
        /// <param name="rect">面板不含边缘的矩形</param>
        /// <param name="alpha">面板体透明度</param>
        /// <param name="reveal">拔刀开合进度0~1(通常直接喂面板开合Alpha)</param>
        /// <param name="time">单调递增的着色器时间</param>
        /// <param name="edgePad">面板边缘外扩像素(注连墨绸/绯月/纸垂住在这一圈)</param>
        /// <param name="tint">最终颜色叠加</param>
        public static void Draw(SpriteBatch sb, Rectangle rect, float alpha, float reveal, float time, int edgePad, Color tint) {
            Effect effect = EffectLoader.OniNarrativePanel?.Value;
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
            effect.Parameters["uColHot"]?.SetValue(CrimsonSlashRenderer.ColHot);
            effect.Parameters["uColBright"]?.SetValue(CrimsonSlashRenderer.ColBright);
            effect.Parameters["uColDeep"]?.SetValue(CrimsonSlashRenderer.ColDeep);
            effect.Parameters["uColDark"]?.SetValue(CrimsonSlashRenderer.ColDark);

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
