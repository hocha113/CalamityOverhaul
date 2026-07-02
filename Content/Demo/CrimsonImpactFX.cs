using CalamityOverhaul.Common;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Graphics;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Demo
{
    /// <summary>
    /// 绯红裂空斩屏幕级演出状态（仅客户端）：压暗聚焦 / 冲击白闪 / Bloom 提亮 / 镜头变焦 punch<br/>
    /// 弹幕侧每帧 Push 推高目标值，渲染端 <see cref="Update"/> 自然衰减 —— 弹幕消失后画面自动回落
    /// </summary>
    internal static class CrimsonImpactFX
    {
        /// <summary>场景压暗 0..1，Push 取最大值维持</summary>
        public static float DimIntensity { get; private set; }
        /// <summary>冲击白闪 0..1，触发后指数衰减</summary>
        public static float FlashIntensity { get; private set; }
        /// <summary>Bloom 强度 0..1</summary>
        public static float BloomIntensity { get; private set; }
        /// <summary>变焦 punch 当前值（乘到 Zoom 上的增量）</summary>
        public static float ZoomPunch { get; private set; }
        /// <summary>白闪/压暗聚焦中心（世界坐标）</summary>
        public static Vector2 FocusWorldCenter { get; private set; }

        public static bool HasAny => DimIntensity > 0.01f || FlashIntensity > 0.01f || BloomIntensity > 0.01f;

        /// <summary>每帧推高场景压暗与 Bloom（弹幕存活期间持续调用）</summary>
        public static void PushAmbience(Vector2 focusWorld, float dim, float bloom) {
            if (VaultUtils.isServer) {
                return;
            }
            FocusWorldCenter = focusWorld;
            DimIntensity = MathHelper.Clamp(MathHelper.Max(DimIntensity, dim), 0f, 1f);
            BloomIntensity = MathHelper.Clamp(MathHelper.Max(BloomIntensity, bloom), 0f, 1.2f);
        }

        /// <summary>冲击瞬间：白闪 + 变焦 punch，一次触发自行衰减</summary>
        public static void PushImpact(Vector2 focusWorld, float flash, float zoomPunch) {
            if (VaultUtils.isServer) {
                return;
            }
            FocusWorldCenter = focusWorld;
            FlashIntensity = MathHelper.Clamp(MathHelper.Max(FlashIntensity, flash), 0f, 1f);
            ZoomPunch = MathHelper.Max(ZoomPunch, zoomPunch);
        }

        /// <summary>渲染端每帧衰减（由 <see cref="DemoImpactRender"/> 驱动）</summary>
        public static void Update() {
            DimIntensity *= 0.90f;
            if (DimIntensity < 0.01f) {
                DimIntensity = 0f;
            }
            FlashIntensity *= 0.70f;
            if (FlashIntensity < 0.01f) {
                FlashIntensity = 0f;
            }
            BloomIntensity *= 0.90f;
            if (BloomIntensity < 0.01f) {
                BloomIntensity = 0f;
            }
        }

        /// <summary>逻辑帧衰减变焦 punch（由 <see cref="CrimsonImpactSystem"/> 驱动，与渲染帧率解耦）</summary>
        internal static void UpdateZoom() {
            ZoomPunch *= 0.82f;
            if (ZoomPunch < 0.0012f) {
                ZoomPunch = 0f;
            }
        }

        /// <summary>世界切换/卸载兜底清空</summary>
        public static void Clear() {
            DimIntensity = FlashIntensity = BloomIntensity = ZoomPunch = 0f;
        }
    }

    /// <summary>变焦 punch 注入与逻辑帧衰减</summary>
    internal sealed class CrimsonImpactSystem : ModSystem
    {
        public override void ModifyTransformMatrix(ref SpriteViewMatrix Transform) {
            float punch = CrimsonImpactFX.ZoomPunch;
            if (punch > 0.001f) {
                Transform.Zoom *= 1f + punch;
            }
        }

        public override void PostUpdateEverything() => CrimsonImpactFX.UpdateZoom();

        public override void OnWorldUnload() => CrimsonImpactFX.Clear();
    }

    /// <summary>
    /// 绯红裂空斩全屏后效：Bloom（提亮→双迭代高斯→加色合成）+ 压暗聚焦/冲击白闪，
    /// screenTarget ping-pong 回写；Bloom 提取自压暗前的画面，保证刀光辉光不被压暗削弱
    /// </summary>
    internal sealed class DemoImpactRender : RenderHandle
    {
        /// <summary>权重 1.10，晚于 PrimeScreenEffectRender(1.08)，早于弹幕扩展层(1.2)</summary>
        public override float Weight => 1.10f;

        /// <summary>两块全屏缓冲：Bloom 亮部 ping-pong</summary>
        public override int ScreenSlot => 2;

        public override void EndCaptureDraw(SpriteBatch sb, GraphicsDevice gd, RenderTarget2D screenSwap) {
            CrimsonImpactFX.Update();

            if (!CrimsonImpactFX.HasAny) {
                return;
            }
            if (screenSwap == null || Main.screenTarget == null) {
                return;
            }

            Effect bloomFx = EffectLoader.DemoBloom?.Value;
            Effect postFx = EffectLoader.DemoImpactPost?.Value;

            bool doBloom = CrimsonImpactFX.BloomIntensity > 0.02f && bloomFx != null
                && ScreenTargets != null && ScreenTargets.Length >= 2
                && ScreenTargets[0] != null && ScreenTargets[1] != null;

            //1) 从未压暗的画面提取亮部并模糊
            if (doBloom) {
                BuildBloom(sb, gd, bloomFx);
            }

            //2) 压暗聚焦 + 白闪 ping-pong 回写
            if (postFx != null && (CrimsonImpactFX.DimIntensity > 0.01f || CrimsonImpactFX.FlashIntensity > 0.01f)) {
                ApplyPost(sb, gd, screenSwap, postFx);
            }

            //3) Bloom 加色合成到最终画面
            if (doBloom) {
                gd.SetRenderTarget(Main.screenTarget);
                sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp
                    , DepthStencilState.None, RasterizerState.CullNone);
                sb.Draw(ScreenTargets[0], Vector2.Zero, Color.White * MathHelper.Clamp(CrimsonImpactFX.BloomIntensity, 0f, 1f));
                sb.End();
            }
        }

        /// <summary>screenTarget → ST0(亮部) → ST1(横模糊) → ST0(纵模糊) → 二迭代加宽，结果留在 ST0</summary>
        private void BuildBloom(SpriteBatch sb, GraphicsDevice gd, Effect bloomFx) {
            bloomFx.Parameters["uThreshold"]?.SetValue(0.60f);
            bloomFx.Parameters["uBoost"]?.SetValue(1.0f);

            //亮部提取
            gd.SetRenderTarget(ScreenTargets[0]);
            gd.Clear(Color.Transparent);
            bloomFx.CurrentTechnique = bloomFx.Techniques["ThresholdTech"];
            sb.Begin(SpriteSortMode.Immediate, BlendState.Opaque);
            bloomFx.CurrentTechnique.Passes[0].Apply();
            sb.Draw(Main.screenTarget, Vector2.Zero, Color.White);
            sb.End();

            bloomFx.CurrentTechnique = bloomFx.Techniques["BlurTech"];
            float texelX = 1f / Main.screenWidth;
            float texelY = 1f / Main.screenHeight;

            //两轮可分离模糊，第二轮步长加宽拉开辉光半径
            for (int i = 0; i < 2; i++) {
                float radius = i == 0 ? 2.0f : 4.5f;

                gd.SetRenderTarget(ScreenTargets[1]);
                gd.Clear(Color.Transparent);
                bloomFx.Parameters["uDelta"]?.SetValue(new Vector2(texelX * radius, 0f));
                sb.Begin(SpriteSortMode.Immediate, BlendState.Opaque);
                bloomFx.CurrentTechnique.Passes[0].Apply();
                sb.Draw(ScreenTargets[0], Vector2.Zero, Color.White);
                sb.End();

                gd.SetRenderTarget(ScreenTargets[0]);
                gd.Clear(Color.Transparent);
                bloomFx.Parameters["uDelta"]?.SetValue(new Vector2(0f, texelY * radius));
                sb.Begin(SpriteSortMode.Immediate, BlendState.Opaque);
                bloomFx.CurrentTechnique.Passes[0].Apply();
                sb.Draw(ScreenTargets[1], Vector2.Zero, Color.White);
                sb.End();
            }
        }

        /// <summary>拷屏到 screenSwap 再带着 DemoImpactPost 写回 screenTarget</summary>
        private static void ApplyPost(SpriteBatch sb, GraphicsDevice gd, RenderTarget2D screenSwap, Effect postFx) {
            Vector2 centerUV = WorldToScreenUV(CrimsonImpactFX.FocusWorldCenter);

            postFx.Parameters["uDim"]?.SetValue(CrimsonImpactFX.DimIntensity);
            postFx.Parameters["uFlash"]?.SetValue(CrimsonImpactFX.FlashIntensity);
            postFx.Parameters["uCenter"]?.SetValue(centerUV);
            postFx.Parameters["uAspect"]?.SetValue(Main.screenWidth / (float)Main.screenHeight);
            postFx.Parameters["uDimTint"]?.SetValue(new Vector3(0.72f, 0.60f, 0.78f));
            postFx.Parameters["uDesat"]?.SetValue(0.35f);

            gd.SetRenderTarget(screenSwap);
            gd.Clear(Color.Transparent);
            sb.Begin(SpriteSortMode.Deferred, BlendState.Opaque);
            sb.Draw(Main.screenTarget, Vector2.Zero, Color.White);
            sb.End();

            gd.SetRenderTarget(Main.screenTarget);
            gd.Clear(Color.Transparent);
            sb.Begin(SpriteSortMode.Immediate, BlendState.Opaque);
            postFx.CurrentTechnique.Passes[0].Apply();
            sb.Draw(screenSwap, Vector2.Zero, Color.White);
            sb.End();
        }

        /// <summary>世界坐标 → 归一化 uv（含 GameViewMatrix.Zoom）</summary>
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
