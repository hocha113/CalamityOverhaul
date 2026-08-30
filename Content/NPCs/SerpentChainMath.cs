using System;

namespace CalamityOverhaul.Content.NPCs
{
    /// <summary>
    /// 蠕虫链条运动学共享数学（荒花/脓蕾沙蟒共用）。
    /// 只放无状态纯函数：颈部刚度梯度、颈段弯角钳制、纵向肌肉波节距系数。
    /// 调参与事件时机归各族 Director 与状态自身。
    /// </summary>
    internal static class SerpentChainMath
    {
        /// <summary>纵向肌肉波：无波</summary>
        public const int WaveNone = 0;
        /// <summary>纵向肌肉波：释放（出手帧头→尾，节距放大再回落 = 蓄势付出去）</summary>
        public const int WaveRelease = 1;
        /// <summary>纵向肌肉波：追压（急刹头→尾，节距压缩 = 身体向刹住的头追压）</summary>
        public const int WavePress = 2;

        /// <summary>
        /// 转角带动系数按链序渐变：颈段最紧（肌肉前躯咬住头的动向）、中段常规、
        /// 尾段最松（鞭梢余摆）。链序按"距当前首领"计（裂躯期由调用方换算）。
        /// </summary>
        public static float StiffnessFactor(float ordinalFromLeader, int totalSegments) {
            if (ordinalFromLeader <= 3f) {
                return MathHelper.Lerp(0.34f, 0.22f, ordinalFromLeader / 3f);
            }
            if (ordinalFromLeader <= 6f) {
                return MathHelper.Lerp(0.22f, 0.12f, (ordinalFromLeader - 3f) / 3f);
            }
            float fromTail = totalSegments - 1 - ordinalFromLeader;
            if (fromTail >= 0f && fromTail < 4f) {
                return MathHelper.Lerp(0.12f, 0.08f, 1f - fromTail / 4f);
            }
            return 0.12f;
        }

        /// <summary>
        /// 颈段相邻弯角上限（弧度）：颈椎有最小弯折半径，超限的折角被圆化成弧，
        /// 从机制上杜绝出手帧的锐角甩颈。颈段外返回 π（不钳制）。
        /// 盘旋最紧圈（半径150、节距40）相邻弯角约 0.27，低于钳制值，不影响盘身。
        /// </summary>
        public static float MaxBendAngle(float ordinalFromLeader) {
            if (ordinalFromLeader <= 2f) {
                return 0.35f;
            }
            if (ordinalFromLeader <= 5f) {
                return MathHelper.Lerp(0.35f, MathHelper.Pi, (ordinalFromLeader - 2f) / 3f);
            }
            return MathHelper.Pi;
        }

        /// <summary>
        /// 行进肌肉波节距系数。波前 ~2.2 链序/帧向尾传播（与鞭链行波同速族），
        /// 单节包络 0→1→0，释放放大节距、追压压缩节距。
        /// </summary>
        public static float GapWaveFactor(float ordinalFromLeader, int kind, float age, float amp) {
            if (kind == WaveNone || amp <= 0.01f) {
                return 1f;
            }
            float local = age - ordinalFromLeader * 2.2f;
            if (local <= 0f || local >= 26f) {
                return 1f;
            }
            float env = MathF.Sin(local / 26f * MathHelper.Pi);
            float sign = kind == WavePress ? -1f : 1f;
            return 1f + sign * amp * env;
        }

        /// <summary>
        /// 蓄力聚拢节距系数：颈段收得最紧、沿身指数衰减——身体向头收拢上膛。
        /// gather 0..1 由蓄力状态逐帧声明。
        /// </summary>
        public static float GatherFactor(float ordinalFromLeader, float gather) {
            if (gather <= 0.01f) {
                return 1f;
            }
            float falloff = MathF.Exp(-ordinalFromLeader / 7f);
            return 1f - 0.20f * MathHelper.Clamp(gather, 0f, 1f) * falloff;
        }

        /// <summary>高速拉伸节距系数：身体在加速度下被拉长（弹性质量读数），刹停自然回缩</summary>
        public static float SpeedStretchFactor(float headSpeed) {
            if (headSpeed <= 18f) {
                return 1f;
            }
            return 1f + MathHelper.Clamp((headSpeed - 18f) / 34f, 0f, 1f) * 0.10f;
        }
    }
}
