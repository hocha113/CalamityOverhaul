using CalamityOverhaul.Common;
using Terraria;
using Terraria.Graphics.CameraModifiers;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core
{
    /// <summary>仪式帷幕/闪光/震屏推送，客户端本地，由状态观察驱动</summary>
    internal static class CultistScreenFX
    {
        /// <summary>帷幕目标强度 0-1，每帧声明</summary>
        internal static float VeilTarget { get; private set; }
        /// <summary>帷幕当前强度（平滑追赶）</summary>
        internal static float VeilIntensity { get; private set; }
        /// <summary>帷幕世界中心</summary>
        internal static Vector2 VeilCenter { get; private set; }
        /// <summary>元素染色 0火 1冰 2雷（浮点内插过渡）</summary>
        internal static float ElementBlend { get; private set; }
        private static float elementTargetValue;
        /// <summary>死亡演出去饱和 0-1</summary>
        internal static float BreakGrade { get; private set; }
        private static float breakTarget;

        //白闪
        internal static float FlashIntensity { get; private set; }
        private static int flashAge;
        private static int flashLife = 1;

        private static bool declaredThisFrame;

        /// <summary>状态每帧声明帷幕；不声明则自动衰减</summary>
        public static void DeclareVeil(Vector2 worldCenter, float intensity, CultistElement element, float breakGrade = 0f) {
            if (VaultUtils.isServer) {
                return;
            }
            VeilCenter = worldCenter;
            VeilTarget = MathHelper.Clamp(intensity, 0f, 1f);
            elementTargetValue = (int)element;
            breakTarget = MathHelper.Clamp(breakGrade, 0f, 1f);
            declaredThisFrame = true;
        }

        /// <summary>一次性白闪</summary>
        public static void PushFlash(float intensity, int lifeFrames) {
            if (VaultUtils.isServer) {
                return;
            }
            if (intensity < FlashIntensity * (1f - flashAge / (float)flashLife)) {
                return;
            }
            FlashIntensity = MathHelper.Clamp(intensity, 0f, 1f);
            flashAge = 0;
            flashLife = System.Math.Max(lifeFrames, 4);
        }

        /// <summary>震屏，受设置门控</summary>
        public static void Punch(Vector2 pos, float strength, int frames, string id = "CultistFX", Vector2? dir = null) {
            if (VaultUtils.isServer || !CWRServerConfig.Instance.ScreenVibration) {
                return;
            }
            Vector2 d = dir.HasValue ? dir.Value.SafeNormalize(Vector2.UnitY) : Main.rand.NextVector2Unit();
            Main.instance.CameraModifiers.Add(new PunchCameraModifier(pos, d, strength, 8f, frames, 2600f, id));
        }

        /// <summary>渲染前推进（VeilRender 调）</summary>
        public static void Update() {
            if (!declaredThisFrame) {
                VeilTarget = 0f;
                breakTarget = 0f;
            }
            declaredThisFrame = false;

            //帷幕起得快收得慢，像舞台灯
            float rate = VeilTarget > VeilIntensity ? 0.08f : 0.03f;
            VeilIntensity = MathHelper.Lerp(VeilIntensity, VeilTarget, rate);
            if (VeilIntensity < 0.004f) {
                VeilIntensity = 0f;
            }

            //元素染色循环内插（0→1→2→0 沿最近方向）
            float diff = elementTargetValue - ElementBlend;
            if (diff > 1.5f) {
                diff -= 3f;
            }
            else if (diff < -1.5f) {
                diff += 3f;
            }
            ElementBlend += diff * 0.06f;
            if (ElementBlend < 0f) {
                ElementBlend += 3f;
            }
            else if (ElementBlend >= 3f) {
                ElementBlend -= 3f;
            }

            BreakGrade = MathHelper.Lerp(BreakGrade, breakTarget, 0.05f);

            if (flashAge < flashLife) {
                flashAge++;
            }
        }

        /// <summary>当前白闪值（含衰减）</summary>
        public static float CurrentFlash() {
            if (flashAge >= flashLife) {
                return 0f;
            }
            float t = flashAge / (float)flashLife;
            return FlashIntensity * (1f - t) * (1f - t);
        }

        public static bool HasAny => VeilIntensity > 0.004f || CurrentFlash() > 0.004f;
    }
}
