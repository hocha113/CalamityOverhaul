using System;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Common
{
    /// <summary>
    /// 冲锋速度包络助手（MOB-REWORK 契约 M2 的标准实现）：起步缓入→峰值保持→力竭衰减。
    /// 纯函数无状态，各端确定性求值。调用方持有相位计时器，收尾自行清残速；
    /// 注入的承诺性速度记得除回提速补偿（MoveGain 口径，位移项除、重力项不除）
    /// </summary>
    internal static class MobDash
    {
        /// <summary>
        /// 三段包络系数 0..1：[0,rise) 二次缓出爬升（起步猛、临峰缓），
        /// [rise,rise+hold) 保持峰值，其后 decay 帧二次衰减（先快掉后拖尾）。越界按端点钳制
        /// </summary>
        public static float Envelope(int t, int rise, int hold, int decay) {
            if (t <= 0) {
                return 0f;
            }
            if (t < rise) {
                float u = t / (float)rise;
                return 1f - (1f - u) * (1f - u);
            }
            if (t < rise + hold) {
                return 1f;
            }
            int over = t - rise - hold;
            if (over >= decay) {
                return 0f;
            }
            float v = 1f - over / (float)decay;
            return v * v;
        }

        /// <summary>沿锁定方向的包络速度。peak 为名义峰速，未含提速补偿，调用方自行除回 MoveGain</summary>
        public static Vector2 Velocity(Vector2 lockedDir, float peak, int t, int rise, int hold, int decay)
            => lockedDir * (peak * Envelope(t, rise, hold, decay));

        /// <summary>
        /// 冲刺朝向倾斜：按包络强度把贴图压向运动方向，读作发力而非贴图平移。
        /// 返回值直接写 npc.rotation（地面怪常用 maxLean 0.12~0.25 弧度）
        /// </summary>
        public static float Lean(float envelope, float dirX, float maxLean)
            => Math.Sign(dirX) * maxLean * envelope;
    }
}
