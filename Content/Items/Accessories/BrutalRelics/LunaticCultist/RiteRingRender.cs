using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
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
            //帧戳门：无任何环形显形时跳过全玩家表扫描
            if (!RiteRingPlayer.PresenceStamp.ActiveWithin()) {
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
            DrawSigil(sb, player.Center, radius, tint,
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

        /// <summary>
        /// 底层符印：整块交给 CultistRuneSigil 着色器画，按弧序描绘显形，充能扇区读符文集满度
        /// </summary>
        private static void DrawSigil(SpriteBatch sb, Vector2 worldPos, float radiusPx,
            Color tint, float progress, float commit, float fill, float alpha) {
            if (alpha <= 0.01f || progress <= 0.001f || radiusPx < 4f) {
                return;
            }

            Effect effect = EffectLoader.CultistRuneSigil?.Value;
            Texture2D canvas = VaultAsset.placeholder2?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || canvas == null || noise == null) {
                DrawSigilFallback(sb, worldPos, radiusPx, tint, alpha * progress);
                return;
            }

            //shader 外环位于内容半径 0.84，quad 折算后留护栏余量
            float halfPx = radiusPx / 0.84f / 0.92f;
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly + worldPos.X * 0.0007f);
            effect.Parameters["uAlpha"]?.SetValue(MathHelper.Clamp(alpha, 0f, 1f));
            effect.Parameters["uTint"]?.SetValue(tint.ToVector3());
            effect.Parameters["uProgress"]?.SetValue(MathHelper.Clamp(progress, 0f, 1f));
            effect.Parameters["uCommit"]?.SetValue(MathHelper.Clamp(commit, 0f, 1f));
            effect.Parameters["uFill"]?.SetValue(MathHelper.Clamp(fill, 0f, 1f));

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);
            GraphicsDevice gd = Main.instance.GraphicsDevice;
            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
            effect.CurrentTechnique.Passes[0].Apply();

            float quadSize = halfPx * 2f;
            sb.Draw(canvas, worldPos - Main.screenPosition, null, Color.White, 0f, canvas.Size() * 0.5f,
                quadSize / canvas.Width, SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>精灵回退：双扩散环近似，黑底加色安全</summary>
        private static void DrawSigilFallback(SpriteBatch sb, Vector2 worldPos, float radiusPx, Color tint, float alpha) {
            Texture2D body = CWRUtils.GetT2DAsset(CWRConstant.Masking + "DiffusionCircle5")?.Value;
            Texture2D rim = CWRUtils.GetT2DAsset(CWRConstant.Masking + "DiffusionCircle4")?.Value;
            if (body == null || rim == null) {
                return;
            }
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.AnisotropicClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            Vector2 drawPos = worldPos - Main.screenPosition;
            Color tintA = tint with { A = 255 };
            float bodyScale = radiusPx / (body.Width * 0.5f * 0.39f);
            float rimScale = radiusPx / (rim.Width * 0.5f * 0.95f);
            sb.Draw(body, drawPos, null, tintA * (alpha * 0.7f), 0f, body.Size() * 0.5f, bodyScale, SpriteEffects.None, 0f);
            sb.Draw(rim, drawPos, null, tintA * (alpha * 0.55f), 0f, rim.Size() * 0.5f, rimScale, SpriteEffects.None, 0f);
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }
    }
}
