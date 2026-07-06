using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI
{
    /// <summary>鬼影 shader 一次调用的全部参数</summary>
    internal struct OniGhostShadowParams
    {
        /// <summary>0~1 扭动量(封印≈0,躁动=1)</summary>
        public float Writhe;
        /// <summary>0~1 碎裂溶解</summary>
        public float Break;
        /// <summary>0~1 睁眼量</summary>
        public float EyeOpen;
        /// <summary>瞳位偏移(UV 空间,凝视光标用,量级 ±0.03)</summary>
        public Vector2 Glance;
        /// <summary>个体差异种子</summary>
        public float Seed;
        /// <summary>整体透明度</summary>
        public float Alpha;
        /// <summary>着色器时间</summary>
        public float Time;
    }

    /// <summary>
    /// 鬼影 shader 绘制(OniGhostShadow.fx)。失败时由调用方走 CPU 烟团降级。<br/>
    /// 形体由域扭曲 fbm 每帧生成,眼睛/碎裂/凝视全部参数化,仪式与细节板共用
    /// </summary>
    internal static class OniGhostShadowDraw
    {
        public static bool Available => EffectLoader.OniGhostShadow?.Value != null;

        /// <summary>在矩形内绘制鬼影(调用方保证当前批为 Deferred+UIScaleMatrix)</summary>
        public static void Draw(SpriteBatch sb, Rectangle rect, in OniGhostShadowParams p) {
            Effect effect = EffectLoader.OniGhostShadow?.Value;
            if (effect == null) {
                return;
            }

            effect.Parameters["uTime"]?.SetValue(p.Time);
            effect.Parameters["uAlpha"]?.SetValue(p.Alpha);
            effect.Parameters["uWrithe"]?.SetValue(p.Writhe);
            effect.Parameters["uBreak"]?.SetValue(p.Break);
            effect.Parameters["uEyeOpen"]?.SetValue(p.EyeOpen);
            effect.Parameters["uGlance"]?.SetValue(p.Glance);
            effect.Parameters["uSeed"]?.SetValue(p.Seed);
            effect.Parameters["uColBody"]?.SetValue(OnikiriUITheme.Ink.ToVector3());
            effect.Parameters["uColRim"]?.SetValue(OnikiriUITheme.GhostDim.ToVector3());
            effect.Parameters["uColFire"]?.SetValue(OnikiriUITheme.GhostFire.ToVector3());

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.AnisotropicClamp, DepthStencilState.None,
                RasterizerState.CullNone, effect, Main.UIScaleMatrix);

            sb.Draw(VaultAsset.placeholder2.Value, rect, Color.White);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.PointClamp, DepthStencilState.None,
                RasterizerState.CullNone, null, Main.UIScaleMatrix);
        }

        /// <summary>由稳定键生成个体种子,同一只鬼的形体扰动跨屏一致</summary>
        public static float SeedFromKey(string key) {
            if (string.IsNullOrEmpty(key)) {
                return 0f;
            }
            int h = 17;
            foreach (char c in key) {
                h = h * 31 + c;
            }
            return OniBrush.Hash01(h) * 10f;
        }
    }
}
