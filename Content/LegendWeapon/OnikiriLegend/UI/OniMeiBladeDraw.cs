using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI
{
    /// <summary>
    /// 改铭台横陈刀身(OniMeiBlade.fx):素钢+刃文+茎段锈锉;
    /// 失败退回 <see cref="OniMeiRenderer.DrawBladeFallback"/>
    /// </summary>
    internal static class OniMeiBladeDraw
    {
        public static bool Available => EffectLoader.OniMeiBlade?.Value != null;

        /// <summary>刀身 quad,center=刀心,批须 Deferred+UIScaleMatrix</summary>
        public static void Draw(SpriteBatch sb, Vector2 center, float rot, Vector2 size, float alpha, float time) {
            Effect effect = EffectLoader.OniMeiBlade?.Value;
            if (effect == null) {
                return;
            }

            effect.Parameters["uTime"]?.SetValue(time);
            effect.Parameters["uAlpha"]?.SetValue(alpha);
            effect.Parameters["uResolution"]?.SetValue(size);
            effect.Parameters["uSeed"]?.SetValue(OnikiriUITheme.MeiBladeSeed);
            effect.Parameters["uTangFrac"]?.SetValue(OnikiriUITheme.MeiTangFraction);
            effect.Parameters["uColInk"]?.SetValue(OnikiriUITheme.Ink.ToVector3());
            effect.Parameters["uColPaper"]?.SetValue(OnikiriUITheme.Paper.ToVector3());
            effect.Parameters["uColDeep"]?.SetValue(OnikiriUITheme.Deep.ToVector3());
            effect.Parameters["uColHot"]?.SetValue(OnikiriUITheme.HotWhite.ToVector3());
            effect.Parameters["uColGold"]?.SetValue(OnikiriUITheme.GoldInlay.ToVector3());
            effect.Parameters["uColGoldDeep"]?.SetValue(OnikiriUITheme.GoldDeep.ToVector3());
            effect.Parameters["uColCandle"]?.SetValue(OnikiriUITheme.CandleWarm.ToVector3());

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.AnisotropicClamp, DepthStencilState.None,
                RasterizerState.CullNone, effect, Main.UIScaleMatrix);

            sb.Draw(VaultAsset.placeholder2.Value, center, new Rectangle(0, 0, 1, 1), Color.White,
                rot, new Vector2(0.5f), size, SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.PointClamp, DepthStencilState.None,
                RasterizerState.CullNone, null, Main.UIScaleMatrix);
        }
    }
}
