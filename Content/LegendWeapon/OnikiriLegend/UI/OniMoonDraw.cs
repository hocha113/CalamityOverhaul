using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI
{
    /// <summary>
    /// 点鬼簿绯月 shader 桥(OniMoon.fx):圆盘月体+晕圈+危态竖瞳;
    /// 绑定 PerlinNoise;失败退回 <see cref="OniRegisterRenderer"/> 的 SoftGlow 简笔
    /// </summary>
    internal static class OniMoonDraw
    {
        public static bool Available => EffectLoader.OniMoon?.Value != null
            && CWRAsset.PerlinNoise?.Value != null;

        /// <summary>以 center 为心画绯月;半尺寸约 96px(含晕圈)</summary>
        public static void Draw(SpriteBatch sb, Vector2 center, float alpha, float time, float pupilOpen) {
            Effect effect = EffectLoader.OniMoon?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null || alpha <= 0.01f) {
                return;
            }

            const float Half = 96f;
            Rectangle dest = new((int)(center.X - Half), (int)(center.Y - Half),
                (int)(Half * 2f), (int)(Half * 2f));

            effect.Parameters["uTime"]?.SetValue(time);
            effect.Parameters["uAlpha"]?.SetValue(alpha);
            effect.Parameters["uPupil"]?.SetValue(MathHelper.Clamp(pupilOpen, 0f, 1f));
            effect.Parameters["uResolution"]?.SetValue(new Vector2(dest.Width, dest.Height));
            effect.Parameters["uColDeep"]?.SetValue(OnikiriUITheme.Deep.ToVector3());
            effect.Parameters["uColBright"]?.SetValue(OnikiriUITheme.Bright.ToVector3());
            effect.Parameters["uColHot"]?.SetValue(OnikiriUITheme.HotWhite.ToVector3());
            effect.Parameters["uColInk"]?.SetValue(OnikiriUITheme.Ink.ToVector3());

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.LinearWrap, DepthStencilState.None,
                RasterizerState.CullNone, effect, Main.UIScaleMatrix);

            //s0=占位像素,s1=Perlin 噪声(wrap)
            Main.graphics.GraphicsDevice.Textures[1] = noise;
            Main.graphics.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
            sb.Draw(VaultAsset.placeholder2.Value, dest, new Rectangle(0, 0, 1, 1), Color.White);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.PointClamp, DepthStencilState.None,
                RasterizerState.CullNone, null, Main.UIScaleMatrix);
        }
    }
}
