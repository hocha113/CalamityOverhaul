using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI
{
    /// <summary>气力墨脉 shader 的帧参数包,动画状态由 <see cref="OniVigorStroke"/> 推导后灌入</summary>
    internal struct OniVigorInkParams
    {
        /// <summary>0~1 当前气力(显示值)</summary>
        public float Fill;
        /// <summary>&gt;= Fill,消耗残痕右缘</summary>
        public float TrailFill;
        /// <summary>显示值变化速度,+恢复/-消耗,约 -1~1</summary>
        public float Flow;
        /// <summary>0~1 消耗脉冲</summary>
        public float SpendPulse;
        /// <summary>0~1 补气脉冲</summary>
        public float GainPulse;
        /// <summary>0~1 回满收笔脉冲</summary>
        public float FullPulse;
        public float Alpha;
        public float Time;
    }

    /// <summary>
    /// 气力墨脉 shader 绘制(OniVigorInk.fx)：宣纸底痕/湿墨主体/飞白/墨锋前沿/
    /// 消耗残痕/回满收笔扫光全部在 shader 内完成。<br/>
    /// 失败时由调用方退回 <c>OniBrush</c> 简笔
    /// </summary>
    internal static class OniVigorInkDraw
    {
        public static bool Available => EffectLoader.OniVigorInk?.Value != null;

        /// <summary>绘制气力墨脉 quad。调用方保证当前批为 Deferred+UIScaleMatrix</summary>
        public static void Draw(SpriteBatch sb, Rectangle dest, in OniVigorInkParams p) {
            Effect effect = EffectLoader.OniVigorInk?.Value;
            if (effect == null) {
                return;
            }

            effect.Parameters["uTime"]?.SetValue(p.Time);
            effect.Parameters["uAlpha"]?.SetValue(p.Alpha);
            effect.Parameters["uResolution"]?.SetValue(new Vector2(dest.Width, dest.Height));
            effect.Parameters["uFill"]?.SetValue(p.Fill);
            effect.Parameters["uTrailFill"]?.SetValue(p.TrailFill);
            effect.Parameters["uFlow"]?.SetValue(p.Flow);
            effect.Parameters["uSpendPulse"]?.SetValue(p.SpendPulse);
            effect.Parameters["uGainPulse"]?.SetValue(p.GainPulse);
            effect.Parameters["uFullPulse"]?.SetValue(p.FullPulse);
            effect.Parameters["uSeed"]?.SetValue(OnikiriUITheme.HudVigorSeed);
            effect.Parameters["uColInk"]?.SetValue(OnikiriUITheme.Ink.ToVector3());
            effect.Parameters["uColPaper"]?.SetValue(OnikiriUITheme.Paper.ToVector3());
            effect.Parameters["uColDeep"]?.SetValue(OnikiriUITheme.Deep.ToVector3());
            effect.Parameters["uColBright"]?.SetValue(OnikiriUITheme.Bright.ToVector3());
            effect.Parameters["uColHot"]?.SetValue(OnikiriUITheme.HotWhite.ToVector3());

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.AnisotropicClamp, DepthStencilState.None,
                RasterizerState.CullNone, effect, Main.UIScaleMatrix);

            sb.Draw(VaultAsset.placeholder2.Value, dest, new Rectangle(0, 0, 1, 1), Color.White);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.PointClamp, DepthStencilState.None,
                RasterizerState.CullNone, null, Main.UIScaleMatrix);
        }
    }
}
