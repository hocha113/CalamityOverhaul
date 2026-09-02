using CalamityOverhaul.Common;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniFinaleSlashs
{
    /// <summary>终之太刀屏幕后效状态(客户端). Push 抬高,Update 衰减</summary>
    internal static class OniFinaleFX
    {
        /// <summary>场景压暗 0..1，随推送平滑逼近、停推后自然回落</summary>
        public static float Dim { get; private set; }
        private static float dimTarget;
        /// <summary>负片反相脉冲 0..1，触发后指数衰减</summary>
        public static float Negative { get; private set; }
        /// <summary>裂缝辉光 0..1</summary>
        public static float SeamGlow { get; private set; }
        /// <summary>裂屏滑移量（像素，两半各滑一半）</summary>
        public static float SplitOffsetPx { get; private set; }
        /// <summary>裂屏滑移逐帧衰减系数</summary>
        public const float SplitDecay = 0.70f;
        /// <summary>刀线角度（世界空间弧度，缩放各向同性故与屏幕空间一致）</summary>
        public static float SplitAngle { get; private set; }
        /// <summary>刀线中心（世界坐标）</summary>
        public static Vector2 SplitCenterWorld { get; private set; }
        /// <summary>压暗聚焦中心（世界坐标）</summary>
        public static Vector2 FocusWorld { get; private set; }

        /// <summary>过刃切片槽：刀线新生时把画面本身切开一瞬（两侧沿法线错开 + 缝内白热线），
        /// 四槽循环复用、快速衰减，多条近拍刀线可并存</summary>
        private struct SliceState
        {
            public Vector2 CenterWorld;
            public float Angle;
            public float AmpPx;
        }

        private static readonly SliceState[] slices = new SliceState[4];
        private static int sliceCursor;
        private static bool sliceLive;

        public static bool HasAny => Dim > 0.012f || Negative > 0.012f
            || SeamGlow > 0.012f || SplitOffsetPx > 0.05f || sliceLive;

        /// <summary>登记一次过刃切片（ampPx=两侧各错开的像素量，指数衰减 ~5 帧自灭）</summary>
        public static void PushSlice(Vector2 centerWorld, float angle, float ampPx) {
            if (!FocusNearLocalView(centerWorld)) {
                return;
            }
            slices[sliceCursor] = new SliceState {
                CenterWorld = centerWorld,
                Angle = angle,
                AmpPx = ampPx,
            };
            sliceCursor = (sliceCursor + 1) % slices.Length;
            sliceLive = true;
        }

        internal static (Vector2 CenterWorld, float Angle, float AmpPx) GetSlice(int i)
            => (slices[i].CenterWorld, slices[i].Angle, slices[i].AmpPx);

        /// <summary>演出焦点离本地视野中心过远时忽略推送，多人下远处玩家不承受全屏后效</summary>
        private static bool FocusNearLocalView(Vector2 focusWorld) {
            if (VaultUtils.isServer) {
                return false;
            }
            Vector2 viewCenter = Main.screenPosition + new Vector2(Main.screenWidth, Main.screenHeight) * 0.5f;
            return Vector2.Distance(viewCenter, focusWorld) < 2800f;
        }

        /// <summary>每帧推高压暗目标（演出期间持续调用）</summary>
        public static void PushDim(Vector2 focusWorld, float dim) {
            if (!FocusNearLocalView(focusWorld)) {
                return;
            }
            FocusWorld = focusWorld;
            dimTarget = MathHelper.Clamp(MathF.Max(dimTarget, dim), 0f, 1f);
        }

        /// <summary>负片闪脉冲，一次触发自行衰减</summary>
        public static void PushNegative(Vector2 focusWorld, float strength) {
            if (!FocusNearLocalView(focusWorld)) {
                return;
            }
            Negative = MathHelper.Clamp(MathF.Max(Negative, strength), 0f, 1f);
        }

        /// <summary>裂屏状态直写（终斩引爆期每帧驱动自己的滑移曲线）</summary>
        public static void PushSplit(Vector2 centerWorld, float angle, float offsetPx, float seamGlow) {
            if (!FocusNearLocalView(centerWorld)) {
                return;
            }
            SplitCenterWorld = centerWorld;
            SplitAngle = angle;
            SplitOffsetPx = MathF.Max(SplitOffsetPx, offsetPx);
            SeamGlow = MathHelper.Clamp(MathF.Max(SeamGlow, seamGlow), 0f, 1f);
        }

        /// <summary>渲染端每帧衰减（由 <see cref="OniFinaleRender"/> 驱动）</summary>
        public static void Update() {
            Dim = MathHelper.Lerp(Dim, dimTarget, 0.18f);
            if (Dim < 0.012f && dimTarget < 0.012f) {
                Dim = 0f;
            }
            dimTarget *= 0.90f;

            Negative *= 0.70f;
            if (Negative < 0.012f) {
                Negative = 0f;
            }
            SeamGlow *= 0.88f;
            if (SeamGlow < 0.012f) {
                SeamGlow = 0f;
            }
            SplitOffsetPx *= SplitDecay;
            if (SplitOffsetPx < 0.05f) {
                SplitOffsetPx = 0f;
            }

            sliceLive = false;
            for (int i = 0; i < slices.Length; i++) {
                slices[i].AmpPx *= 0.70f;
                if (slices[i].AmpPx < 0.15f) {
                    slices[i].AmpPx = 0f;
                }
                else {
                    sliceLive = true;
                }
            }
        }

        /// <summary>世界切换/卸载兜底清空</summary>
        public static void Clear() {
            Dim = dimTarget = Negative = SeamGlow = SplitOffsetPx = 0f;
            sliceLive = false;
            for (int i = 0; i < slices.Length; i++) {
                slices[i].AmpPx = 0f;
            }
        }
    }

    /// <summary>世界卸载/切换时清空屏幕演出状态、碎晶流向标记与疾走隐身旗。
    /// 子世界切换（旧网/超梦/鬼雨）不走 OnWorldUnload 但一定过 ClearWorld，处决途中登入旧网时
    /// 红幕与本地隐身旗会原样带进新世界（反馈五 #19），两处一并挂清理</summary>
    internal sealed class OniFinaleFXSystem : ModSystem
    {
        public override void OnWorldUnload() => ClearShowState();

        public override void ClearWorld() => ClearShowState();

        private static void ClearShowState() {
            OniFinaleFX.Clear();
            OniFinaleSlash.ShatterFlowActive = false;
            OniFinaleLattice.Clear();
            OniFinaleShatter.Clear();
            //驱动它的疾走弹幕随旧世界一起消失，不会再走 OnKill 放行
            OniFlashSteps.OniFlashStep.LocalPlayerHidden = false;
        }
    }

    /// <summary>终之太刀全屏后效、<see</summary>
    internal sealed class OniFinaleRender : RenderHandle
    {
        /// <summary>权重 1.09、晚于 PrimeScreenEffectRender(1.08)</summary>
        public override float Weight => 1.09f;

        private static readonly Vector4[] sliceGeo = new Vector4[4];
        private static readonly float[] sliceAmp = new float[4];

        public override void EndCaptureDraw(SpriteBatch sb, GraphicsDevice gd, RenderTarget2D screenSwap) {
            OniFinaleFX.Update();

            if (!OniFinaleFX.HasAny) {
                return;
            }
            if (screenSwap == null || Main.screenTarget == null) {
                return;
            }
            Effect fx = EffectLoader.OniFinalePost?.Value;
            if (fx == null) {
                return;
            }

            fx.Parameters["uDim"]?.SetValue(MathHelper.Clamp(OniFinaleFX.Dim, 0f, 1f));
            //暗场压向暗酒红、与鬼切绯红主题同色相，只降明度不偏色

            fx.Parameters["uDimTint"]?.SetValue(new Vector3(0.74f, 0.40f, 0.42f));
            fx.Parameters["uDesat"]?.SetValue(0.55f);
            fx.Parameters["uCenter"]?.SetValue(WorldToScreenUV(OniFinaleFX.FocusWorld));
            fx.Parameters["uAspect"]?.SetValue(Main.screenWidth / (float)Main.screenHeight);
            fx.Parameters["uNegative"]?.SetValue(OniFinaleFX.Negative);
            fx.Parameters["uSplitOffset"]?.SetValue(OniFinaleFX.SplitOffsetPx / Main.screenHeight
                * Main.GameViewMatrix.Zoom.Y);
            fx.Parameters["uSplitAngle"]?.SetValue(OniFinaleFX.SplitAngle);
            fx.Parameters["uSplitCenter"]?.SetValue(WorldToScreenUV(OniFinaleFX.SplitCenterWorld));
            fx.Parameters["uSeamGlow"]?.SetValue(OniFinaleFX.SeamGlow);
            fx.Parameters["uSeamColor"]?.SetValue(new Vector3(1.80f, 1.18f, 0.92f));

            //过刃切片槽上载（空槽 amp=0，shader 侧 step 门掉）

            float aspect = Main.screenWidth / (float)Main.screenHeight;
            for (int i = 0; i < 4; i++) {
                (Vector2 centerWorld, float angle, float ampPx) = OniFinaleFX.GetSlice(i);
                Vector2 uv = WorldToScreenUV(centerWorld);
                sliceGeo[i] = new Vector4(uv.X * aspect, uv.Y, -MathF.Sin(angle), MathF.Cos(angle));
                sliceAmp[i] = ampPx / Main.screenHeight * Main.GameViewMatrix.Zoom.Y;
            }
            fx.Parameters["uSliceGeo"]?.SetValue(sliceGeo);
            fx.Parameters["uSliceAmp"]?.SetValue(new Vector4(sliceAmp[0], sliceAmp[1], sliceAmp[2], sliceAmp[3]));

            //拷屏到 screenSwap 再带着后效写回 screenTarget

            gd.SetRenderTarget(screenSwap);
            gd.Clear(Color.Transparent);
            sb.Begin(SpriteSortMode.Deferred, BlendState.Opaque);
            sb.Draw(Main.screenTarget, Vector2.Zero, Color.White);
            sb.End();

            gd.SetRenderTarget(Main.screenTarget);
            gd.Clear(Color.Transparent);
            sb.Begin(SpriteSortMode.Immediate, BlendState.Opaque);
            fx.CurrentTechnique.Passes[0].Apply();
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
