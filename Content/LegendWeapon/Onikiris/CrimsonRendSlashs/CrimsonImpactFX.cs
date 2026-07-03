using CalamityOverhaul.Common;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.Onikiris.CrimsonRendSlashs
{
    /// <summary>
    /// 绯红裂空斩屏幕级演出状态（仅客户端）：冲击白闪 + Bloom 提亮<br/>
    /// 弹幕侧每帧 Push 推高目标值，渲染端 <see cref="Update"/> 自然衰减 —— 弹幕消失后画面自动回落<br/>
    /// 不做压暗/震屏/变焦：全屏级镜头运动容易造成眩晕，打击感交给顿帧与白闪
    /// </summary>
    internal static class CrimsonImpactFX
    {
        /// <summary>冲击白闪 0..1，触发后指数衰减</summary>
        public static float FlashIntensity { get; private set; }
        /// <summary>Bloom 强度 0..1</summary>
        public static float BloomIntensity { get; private set; }
        /// <summary>白闪中心（世界坐标）</summary>
        public static Vector2 FocusWorldCenter { get; private set; }

        public static bool HasAny => FlashIntensity > 0.01f || BloomIntensity > 0.01f;

        /// <summary>每帧推高 Bloom（弹幕存活期间持续调用）</summary>
        public static void PushAmbience(Vector2 focusWorld, float bloom) {
            if (VaultUtils.isServer) {
                return;
            }
            FocusWorldCenter = focusWorld;
            BloomIntensity = MathHelper.Clamp(MathHelper.Max(BloomIntensity, bloom), 0f, 1.2f);
        }

        /// <summary>冲击瞬间白闪，一次触发自行衰减</summary>
        public static void PushImpact(Vector2 focusWorld, float flash) {
            if (VaultUtils.isServer) {
                return;
            }
            FocusWorldCenter = focusWorld;
            FlashIntensity = MathHelper.Clamp(MathHelper.Max(FlashIntensity, flash), 0f, 1f);
        }

        /// <summary>渲染端每帧衰减（由 <see cref="OnikiriImpactRender"/> 驱动）</summary>
        public static void Update() {
            FlashIntensity *= 0.70f;
            if (FlashIntensity < 0.01f) {
                FlashIntensity = 0f;
            }
            BloomIntensity *= 0.90f;
            if (BloomIntensity < 0.01f) {
                BloomIntensity = 0f;
            }
        }

        /// <summary>世界切换/卸载兜底清空</summary>
        public static void Clear() {
            FlashIntensity = BloomIntensity = 0f;
        }
    }

    /// <summary>世界卸载时清空屏幕演出状态</summary>
    internal sealed class CrimsonImpactSystem : ModSystem
    {
        public override void OnWorldUnload() => CrimsonImpactFX.Clear();
    }

    /// <summary>
    /// 绯红裂空斩全屏后效：Bloom（提亮→双迭代高斯→加色合成）+ 冲击白闪，
    /// screenTarget ping-pong 回写；Bloom 提取先于白闪，辉光形状不受闪光干扰
    /// </summary>
    internal sealed class OnikiriImpactRender : RenderHandle
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

            Effect bloomFx = EffectLoader.OniCrimsonBloom?.Value;
            Effect postFx = EffectLoader.OniCrimsonImpactPost?.Value;

            bool doBloom = CrimsonImpactFX.BloomIntensity > 0.02f && bloomFx != null
                && ScreenTargets != null && ScreenTargets.Length >= 2
                && ScreenTargets[0] != null && ScreenTargets[1] != null;

            //1) 提取亮部并模糊
            if (doBloom) {
                BuildBloom(sb, gd, bloomFx);
            }

            //2) 冲击白闪 ping-pong 回写
            if (postFx != null && CrimsonImpactFX.FlashIntensity > 0.01f) {
                ApplyPost(sb, gd, screenSwap, postFx);
            }

            //3) Bloom 加色合成：Main.screenTarget 为 DiscardContents（tML Main.InitTargets 未指定 usage），
            //   重绑定即丢弃原画面 —— 必须经 screenSwap 全帧往返，绑定后立刻整帧重绘，
            //   否则场景被丢弃、只剩加色 Bloom → 全屏黑屏
            if (doBloom) {
                gd.SetRenderTarget(screenSwap);
                gd.Clear(Color.Transparent);
                sb.Begin(SpriteSortMode.Deferred, BlendState.Opaque);
                sb.Draw(Main.screenTarget, Vector2.Zero, Color.White);
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp
                    , DepthStencilState.None, RasterizerState.CullNone);
                sb.Draw(ScreenTargets[0], Vector2.Zero, Color.White * MathHelper.Clamp(CrimsonImpactFX.BloomIntensity, 0f, 1f));
                sb.End();

                gd.SetRenderTarget(Main.screenTarget);
                gd.Clear(Color.Transparent);
                sb.Begin(SpriteSortMode.Deferred, BlendState.Opaque);
                sb.Draw(screenSwap, Vector2.Zero, Color.White);
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

        /// <summary>拷屏到 screenSwap 再带着 OniCrimsonImpactPost 写回 screenTarget（仅白闪，无压暗）</summary>
        private static void ApplyPost(SpriteBatch sb, GraphicsDevice gd, RenderTarget2D screenSwap, Effect postFx) {
            Vector2 centerUV = WorldToScreenUV(CrimsonImpactFX.FocusWorldCenter);

            postFx.Parameters["uDim"]?.SetValue(0f);
            postFx.Parameters["uFlash"]?.SetValue(CrimsonImpactFX.FlashIntensity);
            postFx.Parameters["uCenter"]?.SetValue(centerUV);
            postFx.Parameters["uAspect"]?.SetValue(Main.screenWidth / (float)Main.screenHeight);
            postFx.Parameters["uDimTint"]?.SetValue(new Vector3(1f, 1f, 1f));
            postFx.Parameters["uDesat"]?.SetValue(0f);

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
