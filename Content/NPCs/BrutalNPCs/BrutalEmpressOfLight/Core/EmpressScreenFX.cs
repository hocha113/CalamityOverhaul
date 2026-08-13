namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Core
{
    /// <summary>全屏棱彩FX状态，客户端，Push*写入，渲染句柄调 Update</summary>
    internal static class EmpressScreenFX
    {
        //棱彩脉冲（色散冲击帧）：转阶段/大招终唱/死亡绽散
        internal static float PulseIntensity { get; private set; }
        internal static int PulseAge { get; private set; }
        internal static int PulseLife { get; private set; }
        internal static Vector2 PulseWorldCenter { get; private set; }
        internal static bool PulseActive => PulseAge < PulseLife && PulseIntensity > 0.01f;

        //昼形态环境棱彩描边，缓动值
        internal static float AmbientGrade { get; private set; }
        private static float ambientTarget;

        public static bool HasAny => PulseActive || AmbientGrade > 0.012f;

        /// <summary>棱彩脉冲：radial 色散+白闪，一次演出一记</summary>
        public static void PushPrismPulse(Vector2 worldCenter, float intensity = 1f, int lifeFrames = 34) {
            if (VaultUtils.isServer) {
                return;
            }
            //强者优先，弱脉冲不顶替进行中的强脉冲
            float remain = PulseActive ? PulseIntensity * (1f - PulseAge / (float)PulseLife) : 0f;
            if (intensity < remain) {
                return;
            }
            PulseIntensity = MathHelper.Clamp(intensity, 0f, 1f);
            PulseWorldCenter = worldCenter;
            PulseAge = 0;
            PulseLife = System.Math.Max(lifeFrames, 10);
        }

        /// <summary>昼形态环境档位，每帧由主控声明，未声明自动退潮</summary>
        public static void DeclareAmbient(float grade) {
            if (VaultUtils.isServer) {
                return;
            }
            ambientTarget = MathHelper.Clamp(grade, 0f, 1f);
        }

        /// <summary>每帧推进（渲染句柄驱动，仅客户端）</summary>
        public static void Update() {
            if (PulseAge < PulseLife) {
                PulseAge++;
            }
            AmbientGrade = MathHelper.Lerp(AmbientGrade, ambientTarget, 0.06f);
            if (AmbientGrade < 0.01f && ambientTarget <= 0f) {
                AmbientGrade = 0f;
            }
            //环境档每帧衰减声明，主控活跃时会重新声明
            ambientTarget *= 0.92f;
        }

        /// <summary>卸载/换世界清空</summary>
        public static void Clear() {
            PulseIntensity = 0f;
            PulseAge = PulseLife = 0;
            AmbientGrade = 0f;
            ambientTarget = 0f;
        }
    }
}
