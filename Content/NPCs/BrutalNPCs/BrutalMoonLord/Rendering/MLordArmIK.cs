using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Core;
using System;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Rendering
{
    /// <summary>单侧手臂链解：肩→肘→腕链尖永远连续，只有手掌允许脱链</summary>
    internal struct MLordArmSolve
    {
        /// <summary>肩锚（随核心倾斜旋转）</summary>
        public Vector2 Shoulder;
        /// <summary>肘点（上臂末端=小臂起点，唯一）</summary>
        public Vector2 Elbow;
        /// <summary>小臂末端腕链尖（臂链可达点，与肘必然相连）</summary>
        public Vector2 WristChain;
        /// <summary>手掌绘制腕锚（超程钳制/过渡混合后；判定位置不动）</summary>
        public Vector2 WristDraw;
        /// <summary>手掌贴图绘制中心 = WristDraw - 腕偏移</summary>
        public Vector2 HandDrawCenter;
        /// <summary>上臂沿骨缩放（贴身受压 / 远伸拉伸）</summary>
        public float UpperStretch;
        /// <summary>小臂沿骨缩放</summary>
        public float ForeStretch;
        /// <summary>手掌脱链星桥强度 0~1（0=手贴在腕链尖上）</summary>
        public float BridgeStrength;
        /// <summary>解有效（核心在场）</summary>
        public bool Valid;
    }

    /// <summary>
    /// 层级链 IK：身带肩→肩带肘→肘带掌。
    /// 肩锚 = 躯干姿态的确定性函数；肘 = 肩 + 上臂方向×骨长，方向由
    /// 弦向角与肘偏角两个**标量**临界阻尼弹簧驱动，肘偏角带符号过零连续
    /// （换侧无镜像跳变）、极向侧由迟滞决定、幅度硬限位（永不折叠反悖）；
    /// 小臂永远从肘出发指向手掌真实位置，臂链从肩到腕链尖**不可能断开**。
    /// 手掌超出可达域时钳制其绘制锚到腕链尖（判定位置不动），
    /// 更远则连续混合回真实位置并以星桥续接（强度带下限，可见即有意）。
    /// 贴身时骨段受压缩，保住肘内角不出现反折。
    /// 纯本地确定性求解，只消费已同步的实体位置
    /// </summary>
    internal static class MLordArmIK
    {
        /// <summary>单骨长 px（与 Extra14/15 贴图关节距一致，原版口径）</summary>
        private const float Bone = 340f;
        /// <summary>远伸骨段最大拉伸</summary>
        private const float StretchCap = 1.3f;
        /// <summary>贴身骨段最小压缩</summary>
        private const float SquashFloor = 0.6f;
        /// <summary>肘偏角硬限位 rad（相对弦向，~66°）</summary>
        private const float MaxBend = 1.15f;
        /// <summary>小臂-上臂相对角上限 rad（折叠限位，肘内角不小于 ~45°）</summary>
        private const float MaxRelative = 2.35f;
        /// <summary>手掌钳制带：超程 ≤ 此值时手绘制位贴在腕链尖</summary>
        private const float ClampBand = 140f;
        /// <summary>混合带终点：超程达此值后手回真实位置走星桥</summary>
        private const float BlendBand = 340f;
        /// <summary>弦向角弹簧角频率 rad/s</summary>
        private const float ChordOmega = 13f;
        /// <summary>肘偏角弹簧角频率 rad/s</summary>
        private const float BendOmega = 15f;
        private const float Dt = 1f / 60f;
        /// <summary>腕锚相对手心偏移（原版口径）</summary>
        private static readonly Vector2 WristOffset = new(0f, 76f);

        private struct ArmState
        {
            /// <summary>弦向角（肩→腕方向，平滑）</summary>
            public float Chord;
            public float ChordVel;
            /// <summary>肘偏角（带符号，过零连续换侧）</summary>
            public float Bend;
            public float BendVel;
            /// <summary>期望肘向极性 +1/-1（迟滞锁定）</summary>
            public float DesiredSide;
            public Vector2 LastWrist;
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
        /// 求解该手的臂链。同一游戏帧内多次调用（核心画上臂、手画小臂与手壳）
        /// 返回同一解，两段永远消费同一个肘点
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

            //―――― 身带肩：肩锚是躯干姿态的确定性函数（上下对分锚点）――――
            float dir = (int)hand.ai[MLordAiSlots.HandSide] == 0 ? -1f : 1f;
            bool lowerRow = (int)hand.ai[MLordAiSlots.HandRow] == 1;
            Vector2 shoulderOffset = lowerRow ? MLordDirector.LowerShoulderOffset : MLordDirector.ShoulderOffset;
            Vector2 shoulder = core.Center + new Vector2(shoulderOffset.X * dir,
                shoulderOffset.Y).RotatedBy(core.rotation);
            Vector2 wrist = hand.Center + WristOffset;

            Vector2 d = wrist - shoulder;
            float len = d.Length();
            Vector2 dN = len < 1f ? Vector2.UnitY : d / len;
            len = Math.Max(len, 1f);

            //骨长：远伸均匀拉伸；贴身受压缩，保肘内角不反折
            float boneEff = Bone * MathHelper.Clamp(len / (Bone * 2f), 1f, StretchCap);
            boneEff = Math.Min(boneEff, Math.Max(len * 1.06f, Bone * SquashFloor));

            //―――― 肩带肘：弦向角 + 带符号肘偏角，双标量弹簧 ――――
            float chordTarget = dN.ToRotation();
            float halfL = Math.Min(len * 0.5f, boneEff);
            float bendMag = Math.Min((float)Math.Acos(Math.Clamp(halfL / boneEff, 0f, 1f)), MaxBend);

            //瞬移（闪现拍/远距回归）或久未求解：硬置，臂随手瞬移不甩鞭
            bool snap = !st.Init || stamp - st.LastTick > 20u
                || Vector2.DistanceSquared(wrist, st.LastWrist) > 260f * 260f;

            //极向：躯干外侧偏下，迟滞防抖；带符号目标令换侧过零连续（无镜像跳变）。
            //下偏权重须足够大，令休息位/各编队驻留弦向都远离极性死区
            //权重过小时驻留位落在迟滞带内：残留错侧数秒不自愈（肘上翻）、
            //且 sideBlend 常驻收拢弯度导致小臂骨段长期压瘪（仿真：0.42 时休息位错侧
            //自愈 193 tick、小臂/上臂比 0.42；0.85 时上对全驻留位 0 tick 自愈、比 0.98）。
            //下对肘向更低垂，四臂同侧时上下肘各归其位不扎堆。下对权重 1.35 时
            //弦月合拢驻留位 |want|≈0.36，叠加核心倾斜(≤0.06)与呼吸浮动后最低跌到 0.19，
            //落回 ±0.22 迟滞带内；取 1.8 后同一位形最差 |want|≈0.42，全部驻留位安全
            float hintDown = lowerRow ? 1.8f : 0.85f;
            Vector2 hint = new Vector2(dir, hintDown).RotatedBy(core.rotation);
            Vector2 n = new(-dN.Y, dN.X);
            float want = Vector2.Dot(n, hint);
            if (snap || st.DesiredSide == 0f) {
                //臂链硬重建：极性一并按当前位形取，不带前世残留（槽位复用/离屏回归）
                st.DesiredSide = want >= 0f ? 1f : -1f;
            }
            else if (want * st.DesiredSide < -0.22f) {
                st.DesiredSide = -st.DesiredSide;
            }
            //换侧临界带内收拢弯度：肘贴着弦线扫过换侧（近直臂横渡），不带深折叠跨越
            float sideBlend = MathHelper.Clamp(Math.Abs(want) / 0.45f, 0f, 1f);
            sideBlend = 0.25f + 0.75f * sideBlend * sideBlend * (3f - 2f * sideBlend);
            float bendTarget = bendMag * sideBlend * st.DesiredSide;

            if (snap) {
                st.Chord = chordTarget;
                st.ChordVel = 0f;
                st.Bend = bendTarget;
                st.BendVel = 0f;
                st.Init = true;
            }
            else {
                SpringAngle(ref st.Chord, ref st.ChordVel, chordTarget, ChordOmega);
                SpringScalar(ref st.Bend, ref st.BendVel, bendTarget, BendOmega);
            }
            //硬限位：肘偏角封顶，弹簧输出也不许越界（过渡期同样受约束）
            st.Bend = MathHelper.Clamp(st.Bend, -MaxBend, MaxBend);
            st.LastWrist = wrist;
            st.LastTick = stamp;

            Vector2 upperDir = (st.Chord + st.Bend).ToRotationVector2();
            Vector2 elbow = shoulder + upperDir * boneEff;

            //―――― 肘带掌：小臂从肘出发指向手掌真实位置 ――――
            Vector2 toWrist = wrist - elbow;
            float distWE = toWrist.Length();
            Vector2 foreDir = distWE < 0.5f ? upperDir : toWrist / distWE;

            //折叠限位：小臂相对上臂夹角封顶（肘弯曲方向唯一、不许反折贴臂）。
            //腕近乎正对肘后方时 ±π 缠绕符号会逐帧翻转，取解剖开侧（-极向）防镜像抖动
            float relative = MathHelper.WrapAngle(foreDir.ToRotation() - upperDir.ToRotation());
            if (Math.Abs(relative) > MaxRelative) {
                float sign = Math.Abs(relative) > 3f ? -st.DesiredSide : Math.Sign(relative);
                foreDir = (upperDir.ToRotation() + MaxRelative * sign).ToRotationVector2();
            }
            float foreLen = Math.Min(distWE, boneEff);
            Vector2 wristChain = elbow + foreDir * foreLen;

            //―――― 超程：钳手位→混合→脱链星桥，三段 C0 连续 ――――
            float over = distWE - boneEff;
            Vector2 wristDraw;
            float bridge;
            if (over <= ClampBand) {
                //可达或轻微超程：手的视觉迁就臂形，贴在腕链尖（判定不动）
                wristDraw = wristChain;
                bridge = 0f;
            }
            else if (over <= BlendBand) {
                float t = MathHelper.SmoothStep(0f, 1f, (over - ClampBand) / (BlendBand - ClampBand));
                wristDraw = Vector2.Lerp(wristChain, wrist, t);
                bridge = t * 0.55f;
            }
            else {
                //深度超程（掌击冲线等）：手掌以幻影形态归位真实判定点，星桥续接
                wristDraw = wrist;
                bridge = Math.Min(0.55f + (over - BlendBand) / 400f * 0.45f, 1f);
            }

            s.Shoulder = shoulder;
            s.Elbow = elbow;
            s.WristChain = wristChain;
            s.WristDraw = wristDraw;
            s.HandDrawCenter = wristDraw - WristOffset;
            s.UpperStretch = boneEff / Bone;
            s.ForeStretch = foreLen / Bone;
            s.BridgeStrength = bridge;
            s.Valid = true;
            return s;
        }

        /// <summary>临界阻尼标量弹簧</summary>
        private static void SpringScalar(ref float pos, ref float vel, float target, float omega) {
            float x = pos - target;
            float temp = (vel + x * omega) * Dt;
            float decay = (float)Math.Exp(-omega * Dt);
            vel = (vel - temp * omega) * decay;
            pos = target + (x + temp) * decay;
        }

        /// <summary>临界阻尼角度弹簧（最短弧差）</summary>
        private static void SpringAngle(ref float pos, ref float vel, float target, float omega) {
            float x = MathHelper.WrapAngle(pos - target);
            float temp = (vel + x * omega) * Dt;
            float decay = (float)Math.Exp(-omega * Dt);
            vel = (vel - temp * omega) * decay;
            pos = MathHelper.WrapAngle(target + (x + temp) * decay);
        }
    }
}
