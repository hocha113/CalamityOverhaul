using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.Rendering;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.BrainOfCthulhu
{
    /// <summary>
    /// 镜心与换位裂隙的世界绘制层。
    /// 镜心=冷色负片迷你镜脑(复用 BrainMirrorImage 语汇：DrawBrainBody uCold=1)，
    /// 换位瞬间双端各撕开一道 BrainRift 裂隙。
    /// DrawBeforePlayers 每帧会被触发多次，用 DrawAfterTiles 上膛的闩锁保证只画一次
    /// </summary>
    internal sealed class MirrorheartRender : RenderHandle
    {
        /// <summary>残酷遗物系列预留权重槽</summary>
        public override float Weight => 1.74f;

        private static bool armed;

        public override void DrawAfterTiles(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, RenderTarget2D screenSwap)
            => armed = true;

        public override void DrawBeforePlayers(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            if (!armed || Main.gameMenu) {
                return;
            }
            armed = false;

            //先扫一遍有没有活干，避免空开批
            bool anyWork = false;
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player player = Main.player[i];
                if (player == null || !player.active
                    || !player.TryGetModPlayer(out MirrorheartPlayer mp)) {
                    continue;
                }
                if (NeedsClone(player, mp) || mp.SwapFxTimer > 0) {
                    anyWork = true;
                    break;
                }
            }
            if (!anyWork) {
                return;
            }

            Texture2D brainTex = BrainRenderHelper.GetBrainTexture();
            if (brainTex == null) {
                return;
            }

            //与 DrawBrainBody 的还原配置保持一致，保证嵌套 End/Begin 后状态不漂
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            for (int i = 0; i < Main.maxPlayers; i++) {
                Player player = Main.player[i];
                if (player == null || !player.active
                    || !player.TryGetModPlayer(out MirrorheartPlayer mp)) {
                    continue;
                }

                if (NeedsClone(player, mp) && BrainMotion.OnScreen(mp.MirrorPos)) {
                    DrawClone(spriteBatch, brainTex, player, mp);
                }
                if (mp.SwapFxTimer > 0) {
                    DrawSwapRifts(spriteBatch, mp);
                }
            }

            spriteBatch.End();
        }

        private static bool NeedsClone(Player player, MirrorheartPlayer mp)
            => mp.Equipped && !player.dead && mp.ShatterTimer <= 0 && mp.CloneMaterialize > 0.03f;

        /// <summary>冷色负片镜心：心光底衬按心跳收缩，本体经 BrainMirrorImage 着色</summary>
        private static void DrawClone(SpriteBatch sb, Texture2D brainTex, Player player, MirrorheartPlayer mp) {
            float mat = mp.CloneMaterialize;
            Vector2 drawPos = mp.MirrorPos - Main.screenPosition;

            //本地心跳包络(周期54帧，纯观感不参与判定)
            float phase = Main.GameUpdateCount % 54 / 54f;
            float pulse = (float)Math.Exp(-phase * 6.5f);

            BrainRenderHelper.DrawHeartGlow(sb, drawPos, 0.5f, pulse, 0.7f * mat);

            int frame = (int)(Main.GameUpdateCount / 9) % 4;
            Rectangle frameRect = BrainRenderHelper.GetFrameRect(brainTex, frame);
            Color light = Lighting.GetColor((int)(mp.MirrorPos.X / 16f), (int)(mp.MirrorPos.Y / 16f));
            float rotation = (float)Math.Sin(Main.GameUpdateCount * 0.045f + player.whoAmI * 1.3f) * 0.09f;
            //镜像与玩家对脸
            SpriteEffects effects = -player.direction > 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            float ghost = MathHelper.Lerp(1f, 0.28f, mat);
            float scale = 0.45f * (1f + pulse * 0.05f);

            BrainRenderHelper.DrawBrainBody(sb, brainTex, drawPos, frameRect, light,
                rotation, scale, effects, ghost, 1f, 0.85f * mat);
        }

        /// <summary>双侧裂隙：复用 BrainRift 着色器，落点撕口更大；开合走极锐包络</summary>
        private static void DrawSwapRifts(SpriteBatch sb, MirrorheartPlayer mp) {
            float t = 1f - mp.SwapFxTimer / (float)MirrorheartPlayer.SwapFxTime;
            float open = BrainMotion.SharpOut(Math.Min(t * 3.2f, 1f), 4)
                * (1f - MathHelper.SmoothStep(0f, 1f, MathHelper.Clamp((t - 0.62f) / 0.38f, 0f, 1f)));
            if (open <= 0.02f) {
                return;
            }
            float pulse = (float)Math.Exp(-t * 4.2f);

            Effect shader = EffectLoader.BrainRift?.Value;
            if (shader == null) {
                //着色器缺位时由换位当帧的 PRT 血雾兜底，不再画裂隙
                return;
            }
            Texture2D canvas = CWRUtils.GetT2DAsset(CWRConstant.VaultPlaceholder2).Value;

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            //噪声显式绑 s1，s0 会被 Draw 覆写成画布(合同同 BrainTeleportRift)
            GraphicsDevice gd = Main.instance.GraphicsDevice;
            gd.Textures[1] = CWRAsset.PerlinNoise.Value;
            gd.SamplerStates[1] = SamplerState.LinearWrap;

            DrawOneRift(sb, shader, canvas, mp.SwapPosA, open, pulse, 250f, 0.29f);
            DrawOneRift(sb, shader, canvas, mp.SwapPosB, open, pulse, 290f, 0.61f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }

        private static void DrawOneRift(SpriteBatch sb, Effect shader, Texture2D canvas,
            Vector2 worldPos, float open, float pulse, float canvasSize, float seed) {
            if (!BrainMotion.OnScreen(worldPos, 400f)) {
                return;
            }
            shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            shader.Parameters["uOpen"]?.SetValue(open);
            shader.Parameters["uPulse"]?.SetValue(pulse);
            shader.Parameters["uSeed"]?.SetValue(seed);
            shader.CurrentTechnique.Passes[0].Apply();

            float scale = canvasSize / canvas.Width;
            sb.Draw(canvas, worldPos - Main.screenPosition, null, Color.White, 0f,
                canvas.Size() * 0.5f, scale, SpriteEffects.None, 0f);
        }
    }
}
