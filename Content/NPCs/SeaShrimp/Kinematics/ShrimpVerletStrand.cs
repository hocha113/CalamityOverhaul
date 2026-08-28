using System;

namespace CalamityOverhaul.Content.NPCs.SeaShrimp.Kinematics
{
    /// <summary>
    /// 世界坐标 verlet 触角（OniRope 的世界空间改造）：
    /// 质点积分 + 距离约束迭代，首端钉在头部锚点。
    /// 水下弱重力 + 水流谐波 + 头部运动自然甩尾。纯本地表现
    /// </summary>
    internal sealed class ShrimpVerletStrand
    {
        private readonly Vector2[] pos;
        private readonly Vector2[] old;
        private readonly float segLen;
        private bool warmed;

        public int Count => pos.Length;
        public Vector2 this[int i] => pos[i];

        public ShrimpVerletStrand(int points, float totalLen) {
            points = Math.Max(points, 3);
            pos = new Vector2[points];
            old = new Vector2[points];
            segLen = totalLen / (points - 1);
        }

        /// <summary>沿给定方向摆好初始落位，避免首帧从原点甩来</summary>
        public void WarmStart(Vector2 anchor, Vector2 restDir) {
            for (int i = 0; i < pos.Length; i++) {
                pos[i] = old[i] = anchor + restDir * (segLen * i);
            }
            warmed = true;
        }

        /// <summary>
        /// 推进一帧。anchor 钉首；restDir 静息伸展方向（触角自然前扬）；
        /// wet 时弱重力+水流摆，干时正常重力
        /// </summary>
        public void Update(Vector2 anchor, Vector2 restDir, float time, float phase, bool wet) {
            if (!warmed || Vector2.DistanceSquared(pos[0], anchor) > 400f * 400f) {
                WarmStart(anchor, restDir);
            }

            int n = pos.Length;
            float gravity = wet ? 0.028f : 0.24f;
            float damping = wet ? 0.90f : 0.96f;
            for (int i = 1; i < n; i++) {
                Vector2 vel = (pos[i] - old[i]) * damping;
                old[i] = pos[i];
                pos[i] += vel;
                pos[i].Y += gravity;
                //静息伸展力：让触角保持前扬而不是全程下垂
                float reach = i / (float)(n - 1);
                pos[i] += restDir * (0.16f * (1f - reach));
                //水流谐波：两个频率叠加，越靠末端摆幅越大
                if (wet) {
                    float sway = MathF.Sin(time * 1.7f + phase + i * 0.8f)
                        + MathF.Sin(time * 0.61f + phase * 1.3f + i * 0.42f) * 0.5f;
                    pos[i] += new Vector2(-restDir.Y, restDir.X) * (sway * 0.22f * reach);
                }
            }

            for (int k = 0; k < 3; k++) {
                pos[0] = anchor;
                for (int i = 0; i < n - 1; i++) {
                    Vector2 delta = pos[i + 1] - pos[i];
                    float len = delta.Length();
                    if (len < 0.0001f) {
                        continue;
                    }
                    float diff = (len - segLen) / len;
                    if (i == 0) {
                        pos[i + 1] -= delta * diff;
                    }
                    else {
                        Vector2 corr = delta * (diff * 0.5f);
                        pos[i] += corr;
                        pos[i + 1] -= corr;
                    }
                }
            }
            pos[0] = anchor;
        }

        /// <summary>末端横向冲量（尾弹/受击甩动）</summary>
        public void Nudge(Vector2 impulse) {
            if (!warmed) {
                return;
            }
            old[^1] -= impulse;
            if (pos.Length > 2) {
                old[^2] -= impulse * 0.5f;
            }
        }
    }
}
