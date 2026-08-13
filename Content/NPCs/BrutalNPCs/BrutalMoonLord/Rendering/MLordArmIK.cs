using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Core;
using System;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Rendering
{
    /// <summary>单侧手臂 IK 解（上臂/小臂两骨 + 超程星桥缺口）</summary>
    internal struct MLordArmSolve
    {
        /// <summary>肩锚（随核心倾斜旋转）</summary>
        public Vector2 Shoulder;
        /// <summary>上臂末端肘点</summary>
        public Vector2 ElbowUpper;
        /// <summary>小臂起点肘点（非超程时与 <see cref="ElbowUpper"/> 重合）</summary>
        public Vector2 ElbowFore;
        /// <summary>腕锚（手掌根）</summary>
        public Vector2 Wrist;
        /// <summary>上臂沿骨拉伸比（绘制 Y 缩放）</summary>
        public float UpperStretch;
        /// <summary>小臂沿骨拉伸比</summary>
        public float ForeStretch;
        /// <summary>臂段透明度（深度超程渐幻影化）</summary>
        public float ArmAlpha;
        /// <summary>星桥强度 0~1（0=两骨相接无缺口）</summary>
        public float BridgeStrength;
        /// <summary>解有效（核心在场）</summary>
        public bool Valid;
    }

    /// <summary>
    /// 自写双骨 IK，替换原版 acos 复刻（原版方案不解算肘点、弯向极性焊死，
    /// 手掌跨侧/超程时两骨错接、缠绕穿模）。
    /// 要点：显式解析肘点（垂直平分线偏置）；极向量约束弯向躯干外侧下方，
    /// 带迟滞防逐帧翻转；肘位过临界阻尼弹簧做时域平滑（掌击 47px/f 不硬跳）；
    /// 超程先均匀拉伸骨段到 1.22 倍（肘角连续过零无跳变），再拉断成星桥缺口
    /// （强度随缺口宽度连续升起，交接帧缺口为零）。
    /// 纯本地确定性求解，只消费已同步的实体位置，不产生网络流量
    /// </summary>
    internal static class MLordArmIK
    {
        /// <summary>单骨长 px（与 Extra14/15 贴图关节距一致）</summary>
        private const float Bone = 340f;
        /// <summary>骨段最大均匀拉伸比，超出后拉断成星桥</summary>
        private const float StretchCap = 1.22f;
        /// <summary>肘位弹簧角频率 rad/s（越大跟随越紧）</summary>
        private const float SpringOmega = 19f;
        /// <summary>腕锚相对手心偏移（原版口径）</summary>
        private static readonly Vector2 WristOffset = new(0f, 76f);

        private struct ArmState
        {
            public Vector2 ElbowPos;
            public Vector2 ElbowVel;
            /// <summary>肘向极性 +1/-1（迟滞锁定）</summary>
            public float BendSide;
            public uint LastTick;
            public bool Init;
        }

        private static readonly ArmState[] states = new ArmState[Main.maxNPCs];
        private static readonly MLordArmSolve[] cache = new MLordArmSolve[Main.maxNPCs];
        private static readonly uint[] cacheStamp = new uint[Main.maxNPCs];

        /// <summary>模块卸载/世界离开时清空平滑状态</summary>
        public static void Reset() {
            Array.Clear(states);
            Array.Clear(cacheStamp);
        }

        /// <summary>
        /// 求解该手的臂链。同一游戏帧内多次调用（核心画上臂、手画小臂）返回同一解，
        /// 保证两段消费一致的肘点
        /// </summary>
        public static MLordArmSolve Solve(NPC core, NPC hand) {
            int slot = hand.whoAmI;
            uint stamp = Main.GameUpdateCount + 1u;    //+1 避开数组默认 0
            if (cacheStamp[slot] == stamp) {
                return cache[slot];
            }
            MLordArmSolve solve = SolveInner(core, hand, ref states[slot], stamp);
            cache[slot] = solve;
            cacheStamp[slot] = stamp;
            return solve;
        }

        private static MLordArmSolve SolveInner(NPC core, NPC hand, ref ArmState st, uint stamp) {
            MLordArmSolve s = default;
            if (core == null || !core.active) {
                return s;
            }

            float dir = (int)hand.ai[MLordAiSlots.HandSide] == 0 ? -1f : 1f;
            Vector2 shoulder = core.Center + new Vector2(MLordDirector.ShoulderOffset.X * dir,
                MLordDirector.ShoulderOffset.Y).RotatedBy(core.rotation);
            Vector2 wrist = hand.Center + WristOffset;

            Vector2 d = wrist - shoulder;
            float len = d.Length();
            Vector2 dN = len < 1f ? Vector2.UnitY : d / len;
            len = Math.Max(len, 1f);

            //超程均匀拉伸：骨长随距离升到帽值，肘角在 len=2*Bone 处连续过零
            float boneEff = Bone * MathHelper.Clamp(len / (Bone * 2f), 1f, StretchCap);
            float reach = boneEff * 2f;

            //久未求解视作全新臂链（槽位复用/重新入场），极性与弹簧一并重置，不带前世状态
            bool stale = !st.Init || stamp - st.LastTick > 20u;
            if (stale) {
                st.BendSide = 0f;
            }

            //解析肘点：垂直平分线偏置 h；弯向由极向量（躯干外侧偏下）+ 迟滞决定
            float halfL = Math.Min(len * 0.5f, boneEff);
            float h = (float)Math.Sqrt(Math.Max(boneEff * boneEff - halfL * halfL, 0f));
            Vector2 n = new(-dN.Y, dN.X);
            Vector2 hint = new Vector2(dir, 0.42f).RotatedBy(core.rotation);
            float want = Vector2.Dot(n, hint);
            if (st.BendSide == 0f) {
                st.BendSide = want >= 0f ? 1f : -1f;
            }
            else if (want * st.BendSide < -0.22f) {
                //跨侧且明确反向才翻转极性，其后由弹簧把肘位过渡抹平
                st.BendSide = -st.BendSide;
            }
            Vector2 elbowTarget = shoulder + dN * halfL + n * (h * st.BendSide);

            //临界阻尼弹簧：高速手掌下肩肘平滑跟随；久未求解硬置防陈旧回弹
            const float dt = 1f / 60f;
            if (stale) {
                st.ElbowPos = elbowTarget;
                st.ElbowVel = Vector2.Zero;
                st.Init = true;
            }
            else {
                Vector2 x = st.ElbowPos - elbowTarget;
                Vector2 temp = (st.ElbowVel + x * SpringOmega) * dt;
                float decay = (float)Math.Exp(-SpringOmega * dt);
                st.ElbowVel = (st.ElbowVel - temp * SpringOmega) * decay;
                st.ElbowPos = elbowTarget + (x + temp) * decay;
            }
            st.LastTick = stamp;

            //平滑肘点回投骨长约束（弹簧管时域，比例由两轮交替投影兜底）
            Vector2 elbow = st.ElbowPos;
            for (int i = 0; i < 2; i++) {
                elbow = wrist + (elbow - wrist).SafeNormalize(-dN) * boneEff;
                elbow = shoulder + (elbow - shoulder).SafeNormalize(dN) * boneEff;
            }

            s.Shoulder = shoulder;
            s.Wrist = wrist;
            s.Valid = true;

            if (len <= reach) {
                s.ElbowUpper = elbow;
                s.ElbowFore = elbow;
                s.BridgeStrength = 0f;
                s.ArmAlpha = 1f;
            }
            else {
                //拉断段：两骨各自贴住己端收直，中段缺口由星桥续接（交接帧缺口=0 无硬跳）
                s.ElbowUpper = shoulder + dN * boneEff;
                s.ElbowFore = wrist - dN * boneEff;
                s.BridgeStrength = MathHelper.Clamp((len - reach) / 240f, 0f, 1f);
                s.ArmAlpha = MathHelper.Lerp(1f, 0.6f, MathHelper.Clamp((len - reach) / 520f, 0f, 1f));
            }

            s.UpperStretch = Vector2.Distance(s.ElbowUpper, shoulder) / Bone;
            //肘向翻转的过渡帧里小臂距可能短暂偏离骨长，钳住防橡胶臂（微小缺口由肘部星辉盖住）
            s.ForeStretch = Math.Min(Vector2.Distance(s.ElbowFore, wrist) / Bone, 1.5f);
            return s;
        }
    }
}
