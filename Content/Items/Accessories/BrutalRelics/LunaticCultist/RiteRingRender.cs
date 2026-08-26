using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Rendering;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.LunaticCultist
{
    /// <summary>
    /// 集环绘制层：给每个装备者画身周仪式符印环（CultistRuneSigil 语汇）+
    /// 八枚离散符文刻位逐枚点亮；环整体旋转、半径呼吸
    /// </summary>
    internal sealed class RiteRingRender : RenderHandle
    {
        /// <summary>残酷遗物认领表分配槽位</summary>
        public override float Weight => 1.86f;

        public override void EndEntityDraw(SpriteBatch spriteBatch, Main main) {
            if (Main.gameMenu) {
                return;
            }

            bool begun = false;
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player player = Main.player[i];
                if (player == null || !player.active || player.dead
                    || !player.TryGetModPlayer(out RiteRingPlayer mp) || mp.RingReveal <= 0.02f) {
                    continue;
                }
                if (!CultistMotion.OnScreen(player.Center, 240f)) {
                    continue;
                }
                if (!begun) {
                    begun = true;
                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                        DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
                }
                DrawRing(spriteBatch, player, mp);
            }
            if (begun) {
                spriteBatch.End();
            }
        }

        private static void DrawRing(SpriteBatch sb, Player player, RiteRingPlayer mp) {
            float radius = RiteRingPlayer.RingRadius(player);
            Color ritual = RiteRingPlayer.RitualColor(mp.RitualIndex);
            Color tint = Color.Lerp(CultistMotion.RuneGold, ritual, 0.55f);

            //底层符印：装备时按弧序描绘显形，充能扇区=符文集满度
            float fill = mp.RuneCount / (float)RiteRingPlayer.RuneMax;
            float alpha = MathHelper.Clamp(0.42f * mp.RingReveal + 0.30f * mp.CommitPulse, 0f, 0.8f);
            CultistRenderHelper.DrawSigil(sb, player.Center, radius, tint,
                mp.RingReveal, mp.CommitPulse, fill, alpha);

            //离散符文刻位（预乘 AlphaBlend 批里 A=0 加色）
            Texture2D stroke = CWRUtils.GetT2DAsset(CWRConstant.Masking + "Extra_98")?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (stroke == null || glow == null) {
                return;
            }

            float t = Main.GlobalTimeWrappedHourly;
            for (int slot = 0; slot < RiteRingPlayer.RuneMax; slot++) {
                bool lit = slot < mp.RuneCount;
                Vector2 pos = RiteRingPlayer.SlotPos(player, slot) - Main.screenPosition;
                float angle = (RiteRingPlayer.SlotPos(player, slot) - player.Center).ToRotation();
                float wobble = (float)Math.Sin(t * 2.1f + slot * 1.7f) * 0.12f;
                float rot = angle + MathHelper.PiOver2 + wobble;

                float scale = mp.RingReveal * (lit ? 1f : 0.8f);
                //最新点亮的一枚吃闪光弹跳
                if (lit && slot == mp.RuneCount - 1) {
                    scale *= 1f + mp.LitFlash * 0.7f;
                }

                if (lit) {
                    Color glowC = ritual with { A = 0 };
                    Color coreC = Color.White with { A = 0 };
                    float breathe = 0.82f + 0.18f * (float)Math.Sin(t * 3.3f + slot);
                    sb.Draw(glow, pos, null, glowC * (0.38f * breathe * mp.RingReveal), 0f,
                        glow.Size() * 0.5f, 0.30f * scale, SpriteEffects.None, 0f);
                    sb.Draw(stroke, pos, null, glowC * (0.95f * breathe), rot,
                        stroke.Size() * 0.5f, new Vector2(0.11f, 0.34f) * scale, SpriteEffects.None, 0f);
                    sb.Draw(stroke, pos, null, glowC * (0.6f * breathe), rot + MathHelper.PiOver2,
                        stroke.Size() * 0.5f, new Vector2(0.08f, 0.16f) * scale, SpriteEffects.None, 0f);
                    sb.Draw(stroke, pos, null, coreC * (0.55f * breathe), rot,
                        stroke.Size() * 0.5f, new Vector2(0.05f, 0.24f) * scale, SpriteEffects.None, 0f);
                }
                else {
                    //空刻位：极暗的金痕，读作"这里还有位子"
                    Color dim = CultistMotion.RuneGold with { A = 0 };
                    sb.Draw(stroke, pos, null, dim * (0.10f * mp.RingReveal), rot,
                        stroke.Size() * 0.5f, new Vector2(0.08f, 0.22f) * scale, SpriteEffects.None, 0f);
                }
            }
        }
    }
}
