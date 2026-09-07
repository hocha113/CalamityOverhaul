using CalamityOverhaul.Common;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.EyeOfCthulhu
{
    /// <summary>
    /// 血雾之瞳全屏层：只剩免死/伏击命中的一拍血闪（≤16 帧），复用克眼 EocBloodFog 的血闪通道，
    /// 雾团/常驻暗角/心跳全部撤掉——玩家反馈全屏雾降低能见度，视觉改由血带/血盾承担
    /// </summary>
    internal sealed class BloodfogIrisRender : RenderHandle
    {
        /// <summary>认领槽位 1.722（错开环境渲染 AetherglimPhaseRender 的 1.72）</summary>
        public override float Weight => 1.722f;

        private static readonly Vector4[] emptyBlobs = new Vector4[10];

        public override void EndCaptureDraw(SpriteBatch sb, GraphicsDevice gd, RenderTarget2D screenSwap) {
            BloodfogScreenFX.Update();
            if (Main.gameMenu || !BloodfogScreenFX.HasAny) {
                return;
            }
            if (screenSwap == null || Main.screenTarget == null) {
                return;
            }
            Effect shader = EffectLoader.EocBloodFog?.Value;
            if (shader == null) {
                return;
            }

            shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.017f);
            shader.Parameters["uAspect"]?.SetValue(Main.screenWidth / (float)Main.screenHeight);
            shader.Parameters["blobData"]?.SetValue(emptyBlobs);
            shader.Parameters["blobCount"]?.SetValue(0f);
            shader.Parameters["uVignette"]?.SetValue(0f);
            shader.Parameters["uPulse"]?.SetValue(0f);
            shader.Parameters["uFlash"]?.SetValue(BloodfogScreenFX.Flash);
            //噪声显式绑 s1：SpriteBatch.Draw 会把 s0 覆写成拷屏贴图
            gd.Textures[1] = CWRAsset.PerlinNoise.Value;
            gd.SamplerStates[1] = SamplerState.LinearWrap;

            //拷屏再回写；中途异常也要把目标绑回 screenTarget，否则后续整屏画进交换缓冲
            try {
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
            finally {
                gd.SetRenderTarget(Main.screenTarget);
                gd.Textures[1] = null;
            }
        }
    }

    /// <summary>
    /// 血雾之瞳屏效状态：客户端本地视觉积累器，只剩血闪（事件 Push，指数退潮）
    /// </summary>
    internal static class BloodfogScreenFX
    {
        /// <summary>血闪 0~1，免死/伏击命中一次性脉冲</summary>
        internal static float Flash { get; private set; }

        internal static bool HasAny => Flash > 0.02f;

        internal static void PushFlash(float intensity) {
            if (VaultUtils.isServer) {
                return;
            }
            Flash = MathHelper.Clamp(Math.Max(Flash, intensity), 0f, 1f);
        }

        /// <summary>渲染句柄每帧驱动：血闪指数退潮</summary>
        internal static void Update() {
            Flash *= 0.82f;
            if (Flash < 0.02f) {
                Flash = 0f;
            }
        }

        internal static void Clear() {
            Flash = 0f;
        }
    }
}
