using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI
{
    /// <summary>
    /// 改铭台台面 shader 桥(OniMeiStand.fx):TechLacquer 刀掛黑漆底板 / TechWood 烙印木牌;
    /// 失败退回 <see cref="OniMeiRenderer"/> 的 CPU 简笔
    /// </summary>
    internal static class OniMeiStandDraw
    {
        public static bool Available => EffectLoader.OniMeiStand?.Value != null;

        /// <summary>quad 绘制,批须 Deferred+UIScaleMatrix 进入,内部切 Immediate 后还原</summary>
        private static void DrawQuad(SpriteBatch sb, string technique, Rectangle dest, float alpha, float time, float seed) {
            Effect effect = EffectLoader.OniMeiStand?.Value;
            if (effect == null) {
                return;
            }

            effect.Parameters["uTime"]?.SetValue(time);
            effect.Parameters["uAlpha"]?.SetValue(alpha);
            effect.Parameters["uResolution"]?.SetValue(new Vector2(dest.Width, dest.Height));
            effect.Parameters["uSeed"]?.SetValue(seed);
            effect.Parameters["uColInk"]?.SetValue(OnikiriUITheme.Ink.ToVector3());
            effect.Parameters["uColPaper"]?.SetValue(OnikiriUITheme.Paper.ToVector3());
            effect.Parameters["uColDeep"]?.SetValue(OnikiriUITheme.Deep.ToVector3());
            effect.Parameters["uColDark"]?.SetValue(OnikiriUITheme.Dark.ToVector3());
            effect.Parameters["uColCandle"]?.SetValue(OnikiriUITheme.CandleWarm.ToVector3());
            effect.Parameters["uColGold"]?.SetValue(OnikiriUITheme.GoldInlay.ToVector3());
            effect.Parameters["uColGoldDeep"]?.SetValue(OnikiriUITheme.GoldDeep.ToVector3());
            effect.Parameters["uColBurnDim"]?.SetValue(OnikiriUITheme.BurnDim.ToVector3());
            effect.CurrentTechnique = effect.Techniques[technique];

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

        /// <summary>刀掛黑漆底板(漆理/漆光/蒔絵金尘/金压线/烛染)</summary>
        public static void DrawLacquerBoard(SpriteBatch sb, Rectangle dest, float alpha, float time)
            => DrawQuad(sb, "TechLacquer", dest, alpha, time, OnikiriUITheme.MeiBladeSeed + 3.7f);

        /// <summary>烙印木牌板体(手裁轮廓/木纹/焦边/绳孔)</summary>
        public static void DrawWoodPlank(SpriteBatch sb, Rectangle dest, float alpha, float time)
            => DrawQuad(sb, "TechWood", dest, alpha, time, OnikiriUITheme.MeiBladeSeed + 9.1f);
    }
}
