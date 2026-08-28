using CalamityOverhaul.Content.NPCs.SeaShrimp.Core;
using System;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.SeaShrimp.Kinematics
{
    /// <summary>
    /// 海虾骨架总装：脊链（头+3体节+尾扇）+ 双螯二骨 IK + 六足步态 + verlet 触角 + 尾扇张合。
    /// 每帧由主体 AI 在状态机之后驱动；只消费已同步的头位与各端一致的姿态通道，
    /// 全部输出为本地确定性表现量（联机安全：不发包、不掷 Main.rand 做决策）
    /// </summary>
    internal class ShrimpSkeleton
    {
        /// <summary>脊链节点：位置 + 前向角</summary>
        internal struct Node
        {
            public Vector2 Pos;
            /// <summary>前向角（指向行进方向）</summary>
            public float Dir;
            public readonly Vector2 Forward => Dir.ToRotationVector2();
        }

        /// <summary>0=头 1..3=体节 4=尾扇</summary>
        public readonly Node[] Nodes = new Node[5];

        /// <summary>侧向符号约定：+1 = 前向顺转 90°（自由悬浮体不再随地形翻转）</summary>
        public float DownSign => 1f;

        /// <summary>双螯：0=近侧 1=远侧</summary>
        public readonly TwoBoneIK[] Arms = [
            new(SeaShrimpDirector.ArmBone1, SeaShrimpDirector.ArmBone2),
            new(SeaShrimpDirector.ArmBone1, SeaShrimpDirector.ArmBone2),
        ];
        public readonly TwoBoneSolve[] ArmSolves = new TwoBoneSolve[2];
        /// <summary>螯体世界旋转（平滑）</summary>
        public readonly float[] ClawRot = new float[2];
        /// <summary>螯钳开合 0..1（平滑）</summary>
        public readonly float[] ClawOpen = new float[2];

        /// <summary>触角：0=近侧 1=远侧</summary>
        public readonly ShrimpVerletStrand[] Antennae = [new(6, 116f), new(6, 108f)];

        public readonly ShrimpLegGait Gait = new();

        /// <summary>尾扇张合 0..1（平滑）</summary>
        public float TailFlare { get; private set; } = 0.35f;

        /// <summary>游波相位（按路程推进）</summary>
        private float wavePhase;
        private bool built;

        //==================== 双螯空间抓握（NightmareReaper 式：手撑在屏幕平面上交替抓行）====================

        /// <summary>当前抓点（世界坐标，抓住后固定不动，身体被拖着走的读感来源）</summary>
        private readonly Vector2[] gripPos = new Vector2[2];
        private readonly Vector2[] gripFrom = new Vector2[2];
        private readonly Vector2[] gripTo = new Vector2[2];
        /// <summary>&lt;0 已抓稳；0..1 挪抓中</summary>
        private readonly float[] gripT = [-1f, -1f];
        private readonly bool[] gripInit = new bool[2];
        /// <summary>抓握节拍计时（两手错半拍）</summary>
        private int gripTick;

        /// <summary>确定性相位种子（各端一致）</summary>
        private float seed;

        public void BindSeed(float value) => seed = value;

        /// <summary>髋站节点：0=头 1=体节1 2=体节2</summary>
        public Node StationNode(int station) => Nodes[station];

        /// <summary>节点腹侧方向</summary>
        public Vector2 LocalDown(in Node node) => node.Dir.ToRotationVector2().RotatedBy(MathHelper.PiOver2 * DownSign);

        /// <summary>头节腹侧方向</summary>
        public Vector2 HeadDown => LocalDown(in Nodes[0]);

        /// <summary>臂的体侧方向：0=右列（前向顺转 90°）1=左列</summary>
        public Vector2 Lateral(int armIndex)
            => Nodes[0].Forward.RotatedBy(MathHelper.PiOver2 * (armIndex == 0 ? 1f : -1f));

        /// <summary>肩锚：头前部两侧对称（双手撑屏的出臂位）</summary>
        public Vector2 ShoulderWorld(int armIndex) {
            Vector2 forward = Nodes[0].Forward;
            return Nodes[0].Pos + forward * SeaShrimpDirector.ShoulderForward
                + Lateral(armIndex) * SeaShrimpDirector.ShoulderSide;
        }

        /// <summary>螯尖世界位（腕位再沿螯姿态探出，判定与打点共用）</summary>
        public Vector2 ClawTip(int armIndex)
            => ArmSolves[armIndex].Wrist + ClawRot[armIndex].ToRotationVector2() * 46f;

        /// <summary>硬重建：沿朝向反向铺直整条链，臂足触角全部归位</summary>
        public void Rebuild(Vector2 headPos, float heading, float _ = 1f) {
            built = true;
            Nodes[0].Pos = headPos;
            Nodes[0].Dir = heading;
            for (int i = 1; i < Nodes.Length; i++) {
                Nodes[i].Dir = heading;
                Nodes[i].Pos = Nodes[i - 1].Pos - heading.ToRotationVector2() * SeaShrimpDirector.SpineGaps[i - 1];
            }
            for (int a = 0; a < 2; a++) {
                Arms[a].Snap(GuardWristWant(a));
                ArmSolves[a] = Arms[a].Solve(ShoulderWorld(a), GuardWristWant(a),
                    SeaShrimpDirector.ArmSpring, SeaShrimpDirector.ArmDamping, a == 0 ? 1f : -1f);
                ClawRot[a] = Nodes[0].Dir;
                gripInit[a] = false;
                gripT[a] = -1f;
            }
            Gait.SnapAll(this, HeadDown);
            for (int s = 0; s < 2; s++) {
                Antennae[s].WarmStart(AntennaAnchor(s), AntennaRestDir(s));
            }
        }

        /// <summary>抓握不可用时的收拢腕点：折叠在头前两侧 + 呼吸微摆</summary>
        private Vector2 GuardWristWant(int armIndex) {
            Vector2 forward = Nodes[0].Forward;
            float breathe = MathF.Sin(Main.GlobalTimeWrappedHourly * 1.4f + seed + armIndex * 2.3f) * 5f;
            float alongBreathe = MathF.Sin(Main.GlobalTimeWrappedHourly * 0.9f + seed * 1.7f + armIndex) * 4f;
            return ShoulderWorld(armIndex)
                + forward * (58f + alongBreathe)
                + Lateral(armIndex) * (22f + breathe);
        }

        private Vector2 AntennaAnchor(int side) {
            Vector2 forward = Nodes[0].Forward;
            return Nodes[0].Pos + forward * 82f
                + Lateral(side) * (side == 0 ? 7f : 8f);
        }

        private Vector2 AntennaRestDir(int side) {
            Vector2 forward = Nodes[0].Forward;
            return Vector2.Normalize(forward + Lateral(side) * 0.55f);
        }

        /// <summary>
        /// 每帧总装。headPos/heading 来自运动学层；normal 为贴附面外法线
        /// （游泳时传上方向）；attached 决定步态模式
        /// </summary>
        public void Update(SeaShrimpStateContext ctx, Vector2 headPos, float heading,
            Vector2 tangentMove, float speed, bool wet) {
            NPC npc = ctx.Npc;
            if (!built || Vector2.Distance(Nodes[0].Pos, headPos) > 340f) {
                //初建或同步包把头拽走半屏：整链重建防抽搐
                Rebuild(headPos, heading, 1f);
            }

            Nodes[0].Pos = headPos;
            Nodes[0].Dir = heading;

            //游波相位按路程推进（停住波也停，蠕动与位移强绑定）
            wavePhase += speed * 0.05f * ctx.WaveGain;

            SolveSpine(ctx, speed);
            SolveArms(ctx);
            Gait.Update(this, false, HeadDown, tangentMove, speed);
            UpdateAntennae(wet);

            TailFlare = MathHelper.Lerp(TailFlare, MathHelper.Clamp(ctx.TailFlare, 0f, 1f), 0.14f);

            //尾弹级高速给触角一记甩尾冲量（本地表现）
            if (speed > 24f && Main.GameUpdateCount % 6 == 0) {
                for (int s = 0; s < 2; s++) {
                    Antennae[s].Nudge(npc.velocity * 0.22f);
                }
            }
        }

        /// <summary>
        /// 脊链求解：位置跟随（自然拖拽）与姿态期望（卷曲/波浪）按卷曲强度混合，
        /// 相邻关节相对角硬钳制防折叠，节向角平滑趋近
        /// </summary>
        private void SolveSpine(SeaShrimpStateContext ctx, float speed) {
            float curl = MathHelper.Clamp(ctx.SpineCurl, -1f, 1f);
            float poseWeight = 0.3f + 0.6f * MathF.Min(1f, MathF.Abs(curl) * 1.6f);
            float speedFactor = MathHelper.Clamp(speed / 9f, 0.15f, 1.4f);

            for (int i = 1; i < Nodes.Length; i++) {
                ref Node node = ref Nodes[i];
                Node front = Nodes[i - 1];
                float gap = SeaShrimpDirector.SpineGaps[i - 1];

                //位置跟随的自然方向
                Vector2 toFront = front.Pos - node.Pos;
                float natural = toFront.LengthSquared() < 0.01f ? front.Dir : toFront.ToRotation();

                //姿态期望：前节方向 + 卷曲偏置 + 行波偏置
                float curlOff = curl * SeaShrimpDirector.CurlPerJoint * DownSign;
                float waveOff = MathF.Sin(wavePhase - i * SeaShrimpDirector.CrawlWaveStep)
                    * SeaShrimpDirector.CrawlWaveAmp * ctx.WaveGain * speedFactor;
                float posed = front.Dir + curlOff + waveOff;

                //混合后相对前节钳制
                float blended = natural + MathHelper.WrapAngle(posed - natural) * poseWeight;
                float rel = MathHelper.Clamp(MathHelper.WrapAngle(blended - front.Dir),
                    -SeaShrimpDirector.SpineMaxBend, SeaShrimpDirector.SpineMaxBend);
                float wantDir = front.Dir + rel;

                node.Dir = node.Dir.AngleLerp(wantDir, SeaShrimpDirector.SpineTurnRate);
                node.Pos = front.Pos - node.Dir.ToRotationVector2() * gap;
            }
        }

        /// <summary>
        /// 双螯求解（NightmareReaper 手部文法）：守位=空间抓握——双手撑在屏幕平面上，
        /// 每 30 帧左右手错半拍向新的休息抓点猛挪 12 帧然后钉死（抓住空间、身体被拖行的读感）；
        /// 任何攻击指令一来自动松手出招，打完回抓
        /// </summary>
        private void SolveArms(SeaShrimpStateContext ctx) {
            gripTick = (gripTick + 1) % SeaShrimpDirector.GripCycleFrames;
            for (int a = 0; a < 2; a++) {
                ClawDirective d = ctx.Claws[a];
                Vector2 shoulder = ShoulderWorld(a);
                bool gripping = d.Mode == ClawMode.Guard;
                Vector2 want;
                float open = d.ClawOpen;
                if (gripping) {
                    want = UpdateGrip(a, shoulder, out bool lurching);
                    //挪抓张钳伸够，落点合拢咬紧
                    open = lurching ? 0.85f : 0.06f;
                }
                else {
                    gripInit[a] = false;
                    gripT[a] = -1f;
                    want = d.Target;
                }
                float spring = d.Spring > 0f ? d.Spring : SeaShrimpDirector.ArmSpring;
                float damping = d.Damping > 0f ? d.Damping : SeaShrimpDirector.ArmDamping;
                //抓握段用更硬的弹簧：手要钉得住空间
                if (gripping) {
                    spring = 0.34f;
                    damping = 0.72f;
                }
                //肘极性：向体外侧弓（离线装配实测 -1/+1 会内拐成反关节）
                ArmSolves[a] = Arms[a].Solve(shoulder, want, spring, damping, a == 0 ? 1f : -1f);

                //螯体姿态：抓握时螯尖指向抓点（扒住平面的读数），出招沿前臂方向+指令偏置
                float wantRot;
                if (gripping && gripInit[a]) {
                    wantRot = (gripPos[a] - ArmSolves[a].Wrist)
                        .SafeNormalize(Nodes[0].Forward).ToRotation();
                }
                else {
                    wantRot = ArmSolves[a].ForeDir.ToRotation() + d.ClawPoseOffset;
                }
                ClawRot[a] = ClawRot[a].AngleLerp(wantRot, 0.22f);
                ClawOpen[a] = MathHelper.Lerp(ClawOpen[a], MathHelper.Clamp(open, 0f, 1f), 0.24f);
            }
        }

        /// <summary>本臂的休息抓点：头前两侧（朝向目标的屏幕平面），带确定性微偏</summary>
        private Vector2 RestGrip(int armIndex) {
            Vector2 forward = Nodes[0].Forward;
            float wob = MathF.Sin(seed * 3.1f + armIndex * 2.7f + wavePhase * 0.5f) * 10f;
            return Nodes[0].Pos + forward * (SeaShrimpDirector.GripForward + wob)
                + Lateral(armIndex) * SeaShrimpDirector.GripSide;
        }

        /// <summary>
        /// 抓握推进：到本手节拍帧就向新的休息抓点猛挪（smooth 12f），
        /// 其余时间抓点世界坐标钉死。返回腕期望位（腕悬在抓点后方，螯尖压在抓点上）
        /// </summary>
        private Vector2 UpdateGrip(int armIndex, Vector2 shoulder, out bool lurching) {
            if (!gripInit[armIndex]) {
                gripInit[armIndex] = true;
                gripPos[armIndex] = RestGrip(armIndex);
                gripT[armIndex] = -1f;
            }

            //节拍帧：0 号手在 0，1 号手在半拍
            int myBeat = armIndex * (SeaShrimpDirector.GripCycleFrames / 2);
            if (gripTick == myBeat && gripT[armIndex] < 0f) {
                gripFrom[armIndex] = gripPos[armIndex];
                gripTo[armIndex] = RestGrip(armIndex);
                //挪距太小不值得抬手（驻停时手保持钉死）
                if (Vector2.DistanceSquared(gripFrom[armIndex], gripTo[armIndex]) > 18f * 18f) {
                    gripT[armIndex] = 0f;
                }
            }

            if (gripT[armIndex] >= 0f) {
                gripT[armIndex] += 1f / SeaShrimpDirector.GripLurchFrames;
                if (gripT[armIndex] >= 1f) {
                    gripT[armIndex] = -1f;
                    gripPos[armIndex] = gripTo[armIndex];
                }
                else {
                    float t = gripT[armIndex];
                    float ease = t * t * (3f - 2f * t);
                    gripPos[armIndex] = Vector2.Lerp(gripFrom[armIndex], gripTo[armIndex], ease);
                }
                lurching = gripT[armIndex] >= 0f;
            }
            else {
                lurching = false;
            }

            //抓点失效判定：被甩超臂展一截，或落到头的后侧（臂不许反拖向身后）→ 立刻换抓新位
            float maxReach = SeaShrimpDirector.ArmBone1 + SeaShrimpDirector.ArmBone2;
            bool tooFar = Vector2.Distance(gripPos[armIndex], shoulder) > maxReach * 1.15f;
            bool behindHead = Vector2.Dot(gripPos[armIndex] - Nodes[0].Pos, Nodes[0].Forward) < 12f;
            if (tooFar || behindHead) {
                gripPos[armIndex] = RestGrip(armIndex);
                gripT[armIndex] = -1f;
            }

            //腕缩在抓点后方，螯体（锚→尖 ~46px）恰好压在抓点上
            Vector2 toGrip = (gripPos[armIndex] - shoulder).SafeNormalize(Nodes[0].Forward);
            return gripPos[armIndex] - toGrip * 38f;
        }

        private void UpdateAntennae(bool wet) {
            float time = Main.GlobalTimeWrappedHourly;
            for (int s = 0; s < 2; s++) {
                Antennae[s].Update(AntennaAnchor(s), AntennaRestDir(s), time, seed + s * 2.61f, wet);
            }
        }
    }
}
