using System;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.SeaShrimp.Kinematics
{
    /// <summary>二骨解析 IK 的一帧解</summary>
    internal struct TwoBoneSolve
    {
        public Vector2 Shoulder;
        public Vector2 Elbow;
        public Vector2 Wrist;
        /// <summary>上臂方向（肩→肘单位向量）</summary>
        public Vector2 UpperDir;
        /// <summary>前臂方向（肘→腕单位向量）</summary>
        public Vector2 ForeDir;
    }

    /// <summary>
    /// 单侧螯臂二骨解析 IK（MLordArmIK 的轻量化）：
    /// 腕目标点挂速度弹簧（滞后/过冲/余摆的质量感来源，MOTION §4），
    /// 每帧对弹簧位置做余弦定理精确解，肘极性固定、可达域钳制。
    /// 纯本地确定性求解，只消费已同步的头位与状态指令
    /// </summary>
    internal class TwoBoneIK
    {
        private readonly float bone1;
        private readonly float bone2;

        /// <summary>腕目标弹簧位置（世界坐标）</summary>
        private Vector2 targetPos;
        private Vector2 targetVel;
        private bool init;

        public TwoBoneIK(float bone1, float bone2) {
            this.bone1 = bone1;
            this.bone2 = bone2;
        }

        /// <summary>硬重建：同步纠偏把身体拽走时臂直接归位防抽搐</summary>
        public void Snap(Vector2 pos) {
            targetPos = pos;
            targetVel = Vector2.Zero;
            init = true;
        }

        /// <summary>注入一次冲量（出拳弹出/后坐余摆）</summary>
        public void Impulse(Vector2 impulse) => targetVel += impulse;

        /// <summary>当前弹簧腕位（判定层读取）</summary>
        public Vector2 SpringWrist => targetPos;

        /// <summary>
        /// 推进弹簧并求解。want 为本帧期望腕位；spring/damping 由指令给；
        /// bendSign 为肘弯极性 +1/-1（随身体贴附朝向动态传入，保持肘朝背侧）。
        /// hardSnapDist：与目标偏离超过此值直接重建（默认防半屏拉扯）
        /// </summary>
        public TwoBoneSolve Solve(Vector2 shoulder, Vector2 want, float spring, float damping,
            float bendSign, float hardSnapDist = 480f) {
            if (!init || Vector2.DistanceSquared(targetPos, want) > hardSnapDist * hardSnapDist) {
                Snap(want);
            }

            targetVel = (targetVel + (want - targetPos) * spring) * damping;
            targetPos += targetVel;

            //可达域钳制：留 2px 余量防 acos 边界抖动
            Vector2 d = targetPos - shoulder;
            float len = d.Length();
            float maxReach = bone1 + bone2 - 2f;
            float minReach = MathF.Abs(bone1 - bone2) + 6f;
            if (len < 0.001f) {
                d = Vector2.UnitX;
                len = 0.001f;
            }
            Vector2 dN = d / len;
            float clamped = Math.Clamp(len, minReach, maxReach);
            Vector2 wrist = shoulder + dN * clamped;

            //余弦定理解上臂偏角
            float cosA = (bone1 * bone1 + clamped * clamped - bone2 * bone2) / (2f * bone1 * clamped);
            float bend = MathF.Acos(Math.Clamp(cosA, -1f, 1f));
            Vector2 upperDir = dN.RotatedBy(bend * bendSign);
            Vector2 elbow = shoulder + upperDir * bone1;
            Vector2 foreDir = (wrist - elbow).SafeNormalize(upperDir);

            return new TwoBoneSolve {
                Shoulder = shoulder,
                Elbow = elbow,
                Wrist = wrist,
                UpperDir = upperDir,
                ForeDir = foreDir,
            };
        }
    }
}
