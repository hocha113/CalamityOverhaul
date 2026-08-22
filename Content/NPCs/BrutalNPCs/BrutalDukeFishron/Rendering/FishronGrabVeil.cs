using CalamityOverhaul.Common;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.Rendering
{
    /// <summary>
    /// 入水滤镜状态：只描述"本机视点是否沉在涡底"。
    /// 屏幕级静态按 Viewed=自己 gate：只有被抓玩家的客户端会 Report，
    /// 旁观者/服务器永远是 0；停报后自行衰减，异常断线自愈
    /// </summary>
    internal static class FishronGrabVeilFX
    {
        private static float target;

        /// <summary>当前滤镜浓度 0~1</summary>
        internal static float Veil { get; private set; }

        /// <summary>被抓玩家的客户端每帧上报目标浓度</summary>
        internal static void Report(float intensity) {
            if (VaultUtils.isServer) {
                return;
            }
            target = MathHelper.Clamp(intensity, 0f, 1f);
        }

        /// <summary>渲染层每帧推进：上升快（没顶一瞬），退场稍缓（出水拖尾）</summary>
        internal static void Update() {
            Veil = MathHelper.Lerp(Veil, target, Veil < target ? 0.16f : 0.09f);
            if (Veil < 0.004f) {
                Veil = 0f;
            }
            //上报是逐帧的：先衰减目标，有人续报会顶回去
            target *= 0.86f;
            if (target < 0.004f) {
                target = 0f;
            }
        }

        internal static void Clear() {
            target = 0f;
            Veil = 0f;
        }
    }

    /// <summary>涡底入水全屏后效：screenTarget ping-pong 单 pass，缺着色器时静默跳过</summary>
    internal class FishronGrabVeilRender : RenderHandle
    {
        /// <summary>权重 1.34（本批预分配槽），介于面影(1.3)与生吞(1.35)之间</summary>
        public override float Weight => 1.34f;

        public override void EndCaptureDraw(SpriteBatch sb, GraphicsDevice gd, RenderTarget2D screenSwap) {
            FishronGrabVeilFX.Update();

            if (FishronGrabVeilFX.Veil <= 0.01f) {
                return;
            }
            if (screenSwap == null || Main.screenTarget == null) {
                return;
            }
            if (EffectLoader.FishronGrabVeil?.IsLoaded != true) {
                return;
            }
            Effect shader = EffectLoader.FishronGrabVeil.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (shader == null || noise == null) {
                return;
            }

            shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.016f);
            shader.Parameters["uIntensity"]?.SetValue(FishronGrabVeilFX.Veil);
            shader.Parameters["uAspect"]?.SetValue(Main.screenWidth / (float)Main.screenHeight);
            //噪声显式绑到 s1：SpriteBatch.Draw 会把 s0 覆写成拷屏贴图，
            //参数式贴图绑定实机失效（合同同 ShockRingDraw.Draw）
            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;

            PingPong(sb, gd, screenSwap, shader);
        }

        /// <summary>拷屏再 shader 回写</summary>
        private static void PingPong(SpriteBatch sb, GraphicsDevice gd, RenderTarget2D screenSwap, Effect shader) {
            gd.SetRenderTarget(screenSwap);
            gd.Clear(Color.Transparent);
            sb.Begin(SpriteSortMode.Deferred, BlendState.Opaque);
            sb.Draw(Main.screenTarget, Vector2.Zero, Color.White);
            sb.End();

            gd.SetRenderTarget(Main.screenTarget);
            gd.Clear(Color.Transparent);
            sb.Begin(SpriteSortMode.Immediate, BlendState.Opaque);
            shader.CurrentTechnique.Passes[0].Apply();
            sb.Draw(screenSwap, Vector2.Zero, Color.White);
            sb.End();
        }
    }
}
