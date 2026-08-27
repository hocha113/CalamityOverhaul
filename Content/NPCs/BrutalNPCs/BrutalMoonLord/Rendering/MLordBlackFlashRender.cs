using CalamityOverhaul.Common;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Rendering
{
    /// <summary>
    /// 黑闪爆点全屏缓冲：客户端 Push 写入，渲染句柄消费衰减。
    /// 距离门控：爆心离本地玩家过远只留弱波，不打满帧冲击。
    /// 强度可超 1（残血底牌拍推 2.2）：shader 侧主波加宽并显形尾随涟漪列
    /// </summary>
    internal static class MLordBlackFlashFX
    {
        /// <summary>冲击帧持续（前 2 帧全屏黑白反转，其后红波衰减）</summary>
        internal const int ImpactFrames = 2;
        /// <summary>默认寿命（开幕拍/失手引爆）</summary>
        internal const int BaseLife = 26;
        /// <summary>全效距离；超过打折，超过两倍不推</summary>
        private const float FullRange = 1500f;

        internal static Vector2 WorldCenter { get; private set; }
        internal static int Age { get; private set; }
        internal static int Life { get; private set; } = BaseLife;
        internal static float Strength { get; private set; }
        internal static bool Active => Age < Life && Strength > 0.02f;

        /// <summary>推入一次黑闪爆点（仅绘制端）。strength 超 1 = 残血大爆，life 拉长扩散窗</summary>
        public static void PushFlash(Vector2 worldCenter, float strength = 1f, int life = BaseLife) {
            if (VaultUtils.isServer) {
                return;
            }
            float dist = Vector2.Distance(worldCenter, Main.LocalPlayer.Center);
            if (dist > FullRange * 2f) {
                return;
            }
            WorldCenter = worldCenter;
            Strength = (dist < FullRange ? 1f : 1f - (dist - FullRange) / FullRange) * strength;
            Life = life;
            Age = 0;
        }

        internal static void Update() {
            if (Age < Life) {
                Age++;
            }
        }

        public static void Clear() {
            Age = Life;
            Strength = 0f;
        }
    }

    /// <summary>
    /// 黑闪全屏后效：一帧黑白反转冲击帧（红缘描边）+ 红黑冲击波扩散。
    /// 权重 1.424（本目录分配频段 1.422~1.428），晚于天体后效(1.16)与扭曲(1.2)
    /// 冲击帧是"最后一笔"，不被透镜再弯折
    /// </summary>
    internal class MLordBlackFlashRender : RenderHandle
    {
        public override float Weight => 1.424f;

        public override void EndCaptureDraw(SpriteBatch sb, GraphicsDevice gd, RenderTarget2D screenSwap) {
            MLordBlackFlashFX.Update();

            if (!MLordBlackFlashFX.Active) {
                return;
            }
            if (screenSwap == null || Main.screenTarget == null) {
                return;
            }
            if (EffectLoader.MLordBlackFlashScreen?.IsLoaded != true) {
                return;
            }

            float t = MLordBlackFlashFX.Age / (float)MLordBlackFlashFX.Life;
            float impact = MLordBlackFlashFX.Age < MLordBlackFlashFX.ImpactFrames ? 1f : 0f;
            //红波：快速外扩，尾段平方衰减；加长寿命的大爆把波扫到 1.7 屏高外（涟漪列跟着出画）
            float span = 1.1f + 0.6f * MathHelper.Clamp(
                (MLordBlackFlashFX.Life - MLordBlackFlashFX.BaseLife) / 32f, 0f, 1f);
            float waveR = VaultUtils.EaseOutCubic(t) * span;
            float waveStrength = (1f - t) * (1f - t) * MLordBlackFlashFX.Strength;

            Vector2 centerUV = WorldToScreenUV(MLordBlackFlashFX.WorldCenter);
            Effect shader = EffectLoader.MLordBlackFlashScreen.Value;
            shader.Parameters["uAspect"]?.SetValue(Main.screenWidth / (float)Main.screenHeight);
            shader.Parameters["uFlash"]?.SetValue(new Vector4(centerUV.X, centerUV.Y,
                impact * MLordBlackFlashFX.Strength, waveStrength));
            shader.Parameters["uWave"]?.SetValue(new Vector4(centerUV.X, centerUV.Y, waveR, waveStrength));

            //拷屏再 shader 回写
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

        /// <summary>世界→归一化 uv（含 Zoom）</summary>
        private static Vector2 WorldToScreenUV(Vector2 worldPos) {
            float screenW = Main.screenWidth;
            float screenH = Main.screenHeight;
            Vector2 zoom = Main.GameViewMatrix.Zoom;
            if (zoom.X <= 0f) {
                zoom.X = 1f;
            }
            if (zoom.Y <= 0f) {
                zoom.Y = 1f;
            }
            Vector2 screenCenterPx = new(screenW * 0.5f, screenH * 0.5f);
            Vector2 viewWorldCenter = Main.screenPosition + screenCenterPx;
            Vector2 screenPx = screenCenterPx + (worldPos - viewWorldCenter) * zoom;
            return new Vector2(screenPx.X / screenW, screenPx.Y / screenH);
        }
    }
}
