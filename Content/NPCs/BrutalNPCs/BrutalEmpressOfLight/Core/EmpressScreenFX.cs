using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Core
{
    /// <summary>全屏后效状态，客户端，Push*/Declare* 写入，渲染句柄每帧 Update 推进</summary>
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

        //命中链：整屏压黑一拍再回亮（×0.93 消退）
        internal static float HitDark { get; private set; }

        //定向闪光：光束擦身/被击方向的运动模糊，向量编码方向与强度
        internal static Vector2 FlashDir { get; private set; }
        private static Vector2 flashRaw;

        //竞技场：越界深度（本帧声明）与被捕闪光
        internal static float ArenaPull { get; private set; }
        private static float arenaPullTarget;
        internal static float ArenaFlash { get; private set; }

        //终章停顿倒计时提示：-90f 光、-30f 闪
        internal static float PhaseGlow { get; private set; }
        internal static float PhaseFlash { get; private set; }

        //月影：世界照白（每帧声明，缓动），白光中心=她的世界坐标
        internal static float Whiteout { get; private set; }
        private static float whiteoutTarget;
        internal static Vector2 SunWorld { get; private set; }

        public static bool HasAny => PulseActive || AmbientGrade > 0.012f || HitDark > 0.01f
            || FlashDir.LengthSquared() > 0.0001f || ArenaPull > 0.01f || ArenaFlash > 0.01f
            || PhaseGlow > 0.01f || PhaseFlash > 0.01f || Whiteout > 0.005f;

        /// <summary>月影每帧声明白光强度与中心；未声明自动退潮</summary>
        public static void DeclareWhiteout(float amount, Vector2 sunWorld) {
            if (VaultUtils.isServer) {
                return;
            }
            whiteoutTarget = MathHelper.Clamp(amount, 0f, 1f);
            SunWorld = sunWorld;
        }

        /// <summary>棱彩脉冲：radial 色散+白闪，一次演出一记；弱脉冲不顶替进行中的强脉冲</summary>
        public static void PushPrismPulse(Vector2 worldCenter, float intensity = 1f, int lifeFrames = 34) {
            if (VaultUtils.isServer) {
                return;
            }
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

        /// <summary>命中压黑：先暗一下，闪光才亮得起来</summary>
        public static void PushHitDark(float amount) {
            if (VaultUtils.isServer) {
                return;
            }
            HitDark = MathHelper.Clamp(HitDark + amount, 0f, 0.8f);
        }

        /// <summary>定向闪光累加（方向×强度）</summary>
        public static void PushFlash(Vector2 dir, float strength) {
            if (VaultUtils.isServer) {
                return;
            }
            flashRaw += dir.SafeNormalize(Vector2.Zero) * strength;
            if (flashRaw.Length() > 1.2f) {
                flashRaw = flashRaw.SafeNormalize(Vector2.Zero) * 1.2f;
            }
        }

        public static void DeclareArenaPull(float depth) {
            if (VaultUtils.isServer) {
                return;
            }
            arenaPullTarget = MathHelper.Clamp(depth, 0f, 1f);
        }

        public static void PushArenaFlash() {
            if (VaultUtils.isServer) {
                return;
            }
            ArenaFlash = 0.8f;
        }

        public static void PushPhaseGlow() {
            if (!VaultUtils.isServer) {
                PhaseGlow = 1f;
            }
        }

        public static void PushPhaseFlash(float amount = 0.4f) {
            if (!VaultUtils.isServer) {
                PhaseFlash = System.Math.Max(PhaseFlash, amount);
            }
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
            ambientTarget *= 0.92f;

            HitDark = HitDark < 0.005f ? 0f : HitDark * 0.93f;

            flashRaw = flashRaw.LengthSquared() < 0.0001f ? Vector2.Zero : flashRaw * 0.75f;
            FlashDir = Vector2.Distance(FlashDir, flashRaw) < 0.005f ? flashRaw : FlashDir + (flashRaw - FlashDir) * 0.35f;

            ArenaPull = MathHelper.Lerp(ArenaPull, arenaPullTarget, 0.15f);
            if (ArenaPull < 0.01f && arenaPullTarget <= 0f) {
                ArenaPull = 0f;
            }
            arenaPullTarget = 0f;
            ArenaFlash = ArenaFlash < 0.01f ? 0f : ArenaFlash * 0.94f;

            PhaseGlow = PhaseGlow < 0.01f ? 0f : PhaseGlow * 0.96f;
            PhaseFlash = PhaseFlash < 0.01f ? 0f : PhaseFlash * 0.85f;

            //白光：起得慢（每帧 0.06）、退得快（0.12），都按声明值追
            Whiteout = MathHelper.Lerp(Whiteout, whiteoutTarget, Whiteout < whiteoutTarget ? 0.06f : 0.12f);
            if (Whiteout < 0.004f && whiteoutTarget <= 0f) {
                Whiteout = 0f;
            }
            whiteoutTarget = 0f;
        }

        /// <summary>卸载/换世界清空</summary>
        public static void Clear() {
            PulseIntensity = 0f;
            PulseAge = PulseLife = 0;
            AmbientGrade = 0f;
            ambientTarget = 0f;
            HitDark = 0f;
            FlashDir = flashRaw = Vector2.Zero;
            ArenaPull = arenaPullTarget = 0f;
            ArenaFlash = 0f;
            PhaseGlow = PhaseFlash = 0f;
            Whiteout = whiteoutTarget = 0f;
        }
    }
}
