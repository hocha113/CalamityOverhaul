using CalamityOverhaul.Common;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.Rendering
{
    /// <summary>
    /// 水膜泡统一绘制：一次 Immediate 批画完全场被接管的爆裂气泡，
    /// 逐泡换参 + Apply（气泡上限 70，逐 NPC 重启批次的开销不可接受）。
    /// DrawBeforePlayers 每帧被 BehindNPCs 与主玩家层各触发一次，
    /// 用 DrawAfterTiles 上膛 + 首次消费闩锁保证只画一次（Unsunghero 同款），
    /// 首次触发落在 NPC 层之下，与原版气泡的实体层次一致。
    /// 着色器缺失时此层静默，<see cref="FishronBubbleAI"/> 回退原版贴图绘制
    /// </summary>
    internal class FishronBubbleRender : RenderHandle
    {
        /// <summary>盘径契约：可见半径 = 画布半宽 × 0.42（同 CultistPlanet/Abyssrend）</summary>
        private const float DiskFrac = 0.42f;

        private static bool armed;

        /// <summary>着色器路径是否可用：泡体 AI 据此决定是否交出原版绘制</summary>
        internal static bool PathReady => EffectLoader.FishronBubble?.Value != null
            && CWRAsset.PerlinNoise?.Value != null;

        public override void DrawAfterTiles(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, RenderTarget2D screenSwap)
            => armed = true;

        public override void DrawBeforePlayers(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            if (!armed || Main.gameMenu || Main.dedServ || !PathReady) {
                return;
            }
            armed = false;

            //近两帧无气泡盖戳（无公爵战）：跳过全表探测
            if (!FishronBubbleAI.PresenceStamp.ActiveWithin()) {
                return;
            }

            //先探一遍有没有被接管的泡，空场不开批
            bool any = false;
            foreach (var npc in Main.ActiveNPCs) {
                if (npc.type == NPCID.DetonatingBubble && npc.TryGetOverride<FishronBubbleAI>(out _)) {
                    any = true;
                    break;
                }
            }
            if (!any) {
                return;
            }

            Effect fx = EffectLoader.FishronBubble.Value;
            fx.Parameters["uTint"]?.SetValue(FishronMotionFX.SeaGreen.ToVector3() * 1.05f);
            fx.Parameters["uDeepColor"]?.SetValue(FishronMotionFX.DeepSea.ToVector3());

            graphicsDevice.Textures[1] = CWRAsset.PerlinNoise.Value;
            graphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;

            Texture2D pixel = VaultAsset.placeholder2.Value;
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, fx, Main.GameViewMatrix.TransformationMatrix);

            foreach (var npc in Main.ActiveNPCs) {
                if (npc.type != NPCID.DetonatingBubble
                    || !npc.TryGetOverride(out FishronBubbleAI bubble)
                    || !FishronMotionFX.OnScreen(npc.Center, 160f)) {
                    continue;
                }

                fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly + npc.whoAmI * 0.61f);
                fx.Parameters["uSeed"]?.SetValue(npc.whoAmI * 0.173f);
                fx.Parameters["uWobble"]?.SetValue(bubble.RenderWobble);
                fx.Parameters["uArm"]?.SetValue(bubble.RenderArm);
                fx.Parameters["uBurst"]?.SetValue(bubble.RenderBurst);
                fx.Parameters["uFade"]?.SetValue(bubble.RenderFade);
                fx.CurrentTechnique.Passes[0].Apply();

                //可见膜环直径 ≥ 命中盒：判定永不宽于可见亮体
                float visRadius = npc.width * 0.62f * npc.scale;
                float quad = visRadius / DiskFrac * 2f;
                Vector2 scale = new(quad / pixel.Width, quad / pixel.Height);
                spriteBatch.Draw(pixel, npc.Center - Main.screenPosition, null, Color.White,
                    0f, pixel.Size() / 2f, scale, SpriteEffects.None, 0f);
            }

            spriteBatch.End();
        }
    }
}
