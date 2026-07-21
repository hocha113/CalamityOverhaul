using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI
{
    /// <summary>架势鞘刀 shader 的帧参数包,状态由 <see cref="OniStanceSheath"/> 推导后灌入</summary>
    internal struct OniStanceBladeParams
    {
        /// <summary>0~1 拔刀进度(钢的右缘)</summary>
        public float Reveal;
        /// <summary>进度变化速度,+蓄/-泄,约 -1~1</summary>
        public float Flow;
        /// <summary>0~1 满架势刃口点火</summary>
        public float FullGlow;
        /// <summary>0~1 释放拔刀闪</summary>
        public float ReleaseFlash;
        public float Alpha;
        public float Time;
    }

    /// <summary>刃/鞘段(OniStanceBlade.fx),失败 CPU 简笔,柄镡始终 CPU</summary>
    internal static class OniStanceBladeDraw
    {
        public static bool Available => EffectLoader.OniStanceBlade?.Value != null;

        /// <summary>刃/鞘段 quad,leftCenter=镡侧中点,批须 Deferred+UIScaleMatrix</summary>
        public static void Draw(SpriteBatch sb, Vector2 leftCenter, float rot, Vector2 size, in OniStanceBladeParams p) {
            Effect effect = EffectLoader.OniStanceBlade?.Value;
            if (effect == null) {
                return;
            }

            effect.Parameters["uTime"]?.SetValue(p.Time);
            effect.Parameters["uAlpha"]?.SetValue(p.Alpha);
            effect.Parameters["uResolution"]?.SetValue(size);
            effect.Parameters["uReveal"]?.SetValue(p.Reveal);
            effect.Parameters["uFlow"]?.SetValue(p.Flow);
            effect.Parameters["uFullGlow"]?.SetValue(p.FullGlow);
            effect.Parameters["uReleaseFlash"]?.SetValue(p.ReleaseFlash);
            effect.Parameters["uSeed"]?.SetValue(OnikiriUITheme.HudStanceSeed);
            effect.Parameters["uColInk"]?.SetValue(OnikiriUITheme.Ink.ToVector3());
            effect.Parameters["uColPaper"]?.SetValue(OnikiriUITheme.Paper.ToVector3());
            effect.Parameters["uColDeep"]?.SetValue(OnikiriUITheme.Deep.ToVector3());
            effect.Parameters["uColBright"]?.SetValue(OnikiriUITheme.Bright.ToVector3());
            effect.Parameters["uColHot"]?.SetValue(OnikiriUITheme.HotWhite.ToVector3());

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.AnisotropicClamp, DepthStencilState.None,
                RasterizerState.CullNone, effect, Main.UIScaleMatrix);

            sb.Draw(VaultAsset.placeholder2.Value, leftCenter, new Rectangle(0, 0, 1, 1), Color.White,
                rot, new Vector2(0f, 0.5f), size, SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.PointClamp, DepthStencilState.None,
                RasterizerState.CullNone, null, Main.UIScaleMatrix);
        }
    }
}
