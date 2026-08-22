using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI
{
    /// <summary>
    /// 结印盘盘体 shader 桥(OniSigilBoard.fx TechDisc):圆漆盘一件全包
    /// 轆轤旋纹/漆光/蒔絵六芒/金压线衬绯/三槽鬼火眠焰/烛染炭沉;
    /// 线宽随盘径折算,全幅工位与吊坠微缩共用同一支盘;
    /// 失败退回 <see cref="OniSigilRenderer"/> 的 CPU 简笔
    /// </summary>
    internal static class OniSigilBoardDraw
    {
        /// <summary>盘体形状种子(会话内恒定,漆理不逐帧变形)</summary>
        public const float BoardSeed = 8.41f;

        public static bool Available => EffectLoader.OniSigilBoard?.Value != null;

        /// <summary>
        /// 画一面漆盘;批须 Deferred+UIScaleMatrix 进入,内部切 Immediate 后还原。<br/>
        /// discR=漆盘外半径,starR=六芒尖端半径,slotR=结印位半径(全部 px);
        /// slotLit/slotDanger 分量按槽序(上/右下/左下);rot=盘体摆角(吊坠用,只转纹样)
        /// </summary>
        public static void DrawDisc(SpriteBatch sb, Vector2 center, float discR, float starR,
            float slotR, Vector3 slotLit, Vector3 slotDanger, float complete,
            float alpha, float time, float seed = BoardSeed, float rot = 0f) {
            Effect effect = EffectLoader.OniSigilBoard?.Value;
            if (effect == null || discR < 4f || alpha <= 0.01f) {
                return;
            }
            float half = discR * 1.06f + 6f;
            Rectangle dest = new((int)(center.X - half), (int)(center.Y - half),
                (int)(half * 2f), (int)(half * 2f));

            effect.Parameters["uTime"]?.SetValue(time);
            effect.Parameters["uAlpha"]?.SetValue(alpha);
            effect.Parameters["uResolution"]?.SetValue(new Vector2(dest.Width, dest.Height));
            effect.Parameters["uSeed"]?.SetValue(seed);
            effect.Parameters["uDiscR"]?.SetValue(discR);
            effect.Parameters["uStarR"]?.SetValue(starR);
            effect.Parameters["uSlotR"]?.SetValue(slotR);
            effect.Parameters["uRot"]?.SetValue(rot);
            effect.Parameters["uSlotLit"]?.SetValue(slotLit);
            effect.Parameters["uSlotDanger"]?.SetValue(slotDanger);
            effect.Parameters["uComplete"]?.SetValue(complete);
            effect.Parameters["uColInk"]?.SetValue(OnikiriUITheme.Ink.ToVector3());
            effect.Parameters["uColPaper"]?.SetValue(OnikiriUITheme.Paper.ToVector3());
            effect.Parameters["uColDeep"]?.SetValue(OnikiriUITheme.Deep.ToVector3());
            effect.Parameters["uColDark"]?.SetValue(OnikiriUITheme.Dark.ToVector3());
            effect.Parameters["uColCandle"]?.SetValue(OnikiriUITheme.CandleWarm.ToVector3());
            effect.Parameters["uColGold"]?.SetValue(OnikiriUITheme.GoldInlay.ToVector3());
            effect.Parameters["uColGoldDeep"]?.SetValue(OnikiriUITheme.GoldDeep.ToVector3());
            effect.Parameters["uColBurnDim"]?.SetValue(OnikiriUITheme.BurnDim.ToVector3());
            effect.Parameters["uColGhost"]?.SetValue(OnikiriUITheme.GhostFire.ToVector3());
            effect.Parameters["uColGhostDim"]?.SetValue(OnikiriUITheme.GhostDim.ToVector3());
            effect.CurrentTechnique = effect.Techniques["TechDisc"];

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

        /// <summary>从注册表读三槽占用/将醒/齐印,喂给 <see cref="DrawDisc"/></summary>
        public static (Vector3 lit, Vector3 danger, float complete) ReadSlotState() {
            Vector3 lit = Vector3.Zero;
            Vector3 danger = Vector3.Zero;
            for (int i = 0; i < OniRegistry.SlotCount && i < 3; i++) {
                OniGhostEntry entry = OniRegistry.SlotEntry(i);
                if (entry == null) {
                    continue;
                }
                switch (i) {
                    case 0: lit.X = 1f; if (entry.InDanger) { danger.X = 1f; } break;
                    case 1: lit.Y = 1f; if (entry.InDanger) { danger.Y = 1f; } break;
                    default: lit.Z = 1f; if (entry.InDanger) { danger.Z = 1f; } break;
                }
            }
            float complete = OniRegistry.EquippedCount >= OniRegistry.SlotCount ? 1f : 0f;
            return (lit, danger, complete);
        }
    }
}
