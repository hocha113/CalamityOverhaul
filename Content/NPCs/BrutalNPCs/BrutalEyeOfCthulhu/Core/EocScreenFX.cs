namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu.Core
{
    /// <summary>全屏血雾/血闪状态，客户端 Push 写入，渲染句柄消费并衰减</summary>
    internal static class EocScreenFX
    {
        /// <summary>血幕收拢强度 0~1，边缘视野压缩</summary>
        internal static float VignetteIntensity { get; private set; }
        private static float vignetteGoal;

        /// <summary>血闪冲击 0~1，转阶段/死亡终爆一次性脉冲</summary>
        internal static float FlashIntensity { get; private set; }
        private static int flashAge;
        private static int flashLife;

        /// <summary>心跳脉动相位驱动，大招/低血时被推</summary>
        internal static float PulseIntensity { get; private set; }

        public static bool HasAny => VignetteIntensity > 0.02f || FlashActive || PulseIntensity > 0.02f;

        public static bool FlashActive => flashAge < flashLife && FlashIntensity > 0.01f;

        public static float FlashProgress => flashLife > 0 ? MathHelper.Clamp(flashAge / (float)flashLife, 0f, 1f) : 1f;

        /// <summary>每帧声明血幕目标值，未声明自然回落</summary>
        public static void PushVignette(float goal) {
            if (VaultUtils.isServer) {
                return;
            }
            if (goal > vignetteGoal) {
                vignetteGoal = MathHelper.Clamp(goal, 0f, 1f);
            }
        }

        /// <summary>心跳脉动，每帧推</summary>
        public static void PushPulse(float intensity) {
            if (VaultUtils.isServer) {
                return;
            }
            if (intensity > PulseIntensity) {
                PulseIntensity = MathHelper.Clamp(intensity, 0f, 1f);
            }
        }

        /// <summary>血闪，一次性</summary>
        public static void PushFlash(float intensity = 1f, int lifeFrames = 14) {
            if (VaultUtils.isServer) {
                return;
            }
            FlashIntensity = MathHelper.Clamp(intensity, 0f, 1f);
            flashAge = 0;
            flashLife = System.Math.Max(lifeFrames, 6);
        }

        /// <summary>渲染句柄每帧驱动</summary>
        public static void Update() {
            VignetteIntensity = MathHelper.Lerp(VignetteIntensity, vignetteGoal, 0.07f);
            if (VignetteIntensity < 0.015f && vignetteGoal <= 0f) {
                VignetteIntensity = 0f;
            }
            //目标值每帧回落，由状态每帧重新声明
            vignetteGoal *= 0.9f;
            if (vignetteGoal < 0.02f) {
                vignetteGoal = 0f;
            }

            if (flashAge < flashLife) {
                flashAge++;
            }

            PulseIntensity *= 0.9f;
            if (PulseIntensity < 0.02f) {
                PulseIntensity = 0f;
            }
        }

        /// <summary>卸载/切世界清空</summary>
        public static void Clear() {
            VignetteIntensity = 0f;
            vignetteGoal = 0f;
            FlashIntensity = 0f;
            flashAge = flashLife = 0;
            PulseIntensity = 0f;
        }
    }
}
