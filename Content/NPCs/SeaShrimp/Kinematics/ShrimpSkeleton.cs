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

        /// <summary>腹侧符号：+1 表示"前向顺时针转 90°"指向地面，随贴附面更新</summary>
        public float DownSign { get; private set; } = 1f;

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

        /// <summary>爬行 S 波相位（按路程推进）</summary>
        private float wavePhase;
        private bool built;
        /// <summary>本帧是否贴附（Update 记录，螯撑地据此启停）</summary>
        private bool attached;

        //==================== 双螯撑地（守位=拄地承重，攻击指令一来即自动脱撑）====================

        /// <summary>当前撑点（世界）</summary>
        private readonly Vector2[] plantPos = new Vector2[2];
        private readonly Vector2[] plantFrom = new Vector2[2];
        private readonly Vector2[] plantTo = new Vector2[2];
        /// <summary>&lt;0 撑稳；0..1 迈撑中</summary>
        private readonly float[] plantStepT = [-1f, -1f];
        private readonly bool[] plantValid = new bool[2];

        /// <summary>确定性相位种子（各端一致）</summary>
        private float seed;

        public void BindSeed(float value) => seed = value;

        /// <summary>髋站节点：0=头 1=体节1 2=体节2</summary>
        public Node StationNode(int station) => Nodes[station];

        /// <summary>节点腹侧方向</summary>
        public Vector2 LocalDown(in Node node) => node.Dir.ToRotationVector2().RotatedBy(MathHelper.PiOver2 * DownSign);

        /// <summary>头节腹侧方向</summary>
        public Vector2 HeadDown => LocalDown(in Nodes[0]);

        /// <summary>近侧肩锚（螯击状态取出手起点用）</summary>
        public Vector2 ShoulderWorld(int armIndex) {
            Vector2 forward = Nodes[0].Forward;
            Vector2 down = HeadDown;
            return armIndex == 0
                ? Nodes[0].Pos + forward * SeaShrimpDirector.ShoulderForward + down * SeaShrimpDirector.ShoulderSide
                : Nodes[0].Pos + forward * (SeaShrimpDirector.ShoulderForward - 8f) + down * (SeaShrimpDirector.ShoulderSide - 12f);
        }

        /// <summary>螯尖世界位（腕位再沿螯姿态探出，判定与打点共用）</summary>
        public Vector2 ClawTip(int armIndex)
            => ArmSolves[armIndex].Wrist + ClawRot[armIndex].ToRotationVector2() * 46f;

        /// <summary>硬重建：沿朝向反向铺直整条链，臂足触角全部归位</summary>
        public void Rebuild(Vector2 headPos, float heading, float downSign) {
            built = true;
            DownSign = downSign;
            Nodes[0].Pos = headPos;
            Nodes[0].Dir = heading;
            for (int i = 1; i < Nodes.Length; i++) {
                Nodes[i].Dir = heading;
                Nodes[i].Pos = Nodes[i - 1].Pos - heading.ToRotationVector2() * SeaShrimpDirector.SpineGaps[i - 1];
            }
            for (int a = 0; a < 2; a++) {
                Arms[a].Snap(GuardWristWant(a));
                ArmSolves[a] = Arms[a].Solve(ShoulderWorld(a), GuardWristWant(a),
                    SeaShrimpDirector.ArmSpring, SeaShrimpDirector.ArmDamping, -DownSign);
                ClawRot[a] = Nodes[0].Dir;
                plantValid[a] = false;
                plantStepT[a] = -1f;
            }
            Vector2 groundDir = HeadDown;
            Gait.SnapAll(this, groundDir);
            for (int s = 0; s < 2; s++) {
                Antennae[s].WarmStart(AntennaAnchor(s), AntennaRestDir());
            }
        }

        /// <summary>守位腕点：折叠在头前下方 + 呼吸微摆</summary>
        private Vector2 GuardWristWant(int armIndex) {
            Vector2 forward = Nodes[0].Forward;
            Vector2 down = HeadDown;
            float breathe = MathF.Sin(Main.GlobalTimeWrappedHourly * 1.4f + seed + armIndex * 2.3f) * 5f;
            float alongBreathe = MathF.Sin(Main.GlobalTimeWrappedHourly * 0.9f + seed * 1.7f + armIndex) * 4f;
            return ShoulderWorld(armIndex)
                + forward * (56f + alongBreathe)
                + down * (26f + breathe)
                + forward * (armIndex == 1 ? -10f : 0f);
        }

        private Vector2 AntennaAnchor(int side) {
            Vector2 forward = Nodes[0].Forward;
            Vector2 down = HeadDown;
            return Nodes[0].Pos + forward * 82f + down * (side == 0 ? 6f : -8f);
        }

        private Vector2 AntennaRestDir() {
            Vector2 forward = Nodes[0].Forward;
            Vector2 up = -HeadDown;
            return Vector2.Normalize(forward + up * 0.62f);
        }

        /// <summary>
        /// 每帧总装。headPos/heading 来自运动学层；normal 为贴附面外法线
        /// （游泳时传上方向）；attached 决定步态模式
        /// </summary>
        public void Update(SeaShrimpStateContext ctx, Vector2 headPos, float heading,
            Vector2 normal, bool attached, Vector2 tangentMove, float speed, bool wet) {
            NPC npc = ctx.Npc;
            if (!built || Vector2.Distance(Nodes[0].Pos, headPos) > 340f) {
                //初建或同步包把头拽走半屏：整链重建防抽搐
                float ds = MathF.Sign(Vector2.Dot(heading.ToRotationVector2().RotatedBy(MathHelper.PiOver2), -normal));
                Rebuild(headPos, heading, ds == 0f ? DownSign : ds);
            }

            //腹侧符号跟随贴附面（法线几乎平行体轴时保持旧值防抖）
            float dot = Vector2.Dot(Nodes[0].Forward.RotatedBy(MathHelper.PiOver2), -normal);
            if (MathF.Abs(dot) > 0.35f) {
                DownSign = MathF.Sign(dot);
            }

            Nodes[0].Pos = headPos;
            Nodes[0].Dir = heading;
            this.attached = attached;

            //S 波相位按路程推进（停住波也停，蠕动与位移强绑定）
            wavePhase += speed * 0.05f * ctx.WaveGain;

            SolveSpine(ctx, speed);
            SolveArms(ctx);
            Gait.Update(this, attached, -normal, tangentMove, speed);
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
            float speedFactor = MathHelper.Clamp(speed / SeaShrimpDirector.CrawlSpeed, 0.15f, 1.4f);

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

        /// <summary>双螯求解：守位=拄地撑身（虾用双螯撑在地上），攻击指令一来自动脱撑</summary>
        private void SolveArms(SeaShrimpStateContext ctx) {
            float bendSign = -DownSign;
            for (int a = 0; a < 2; a++) {
                ClawDirective d = ctx.Claws[a];
                Vector2 shoulder = ShoulderWorld(a);
                bool planted = false;
                Vector2 want;
                if (d.Mode == ClawMode.Guard && attached) {
                    planted = UpdatePlant(a, out want);
                }
                else {
                    plantValid[a] = false;
                    plantStepT[a] = -1f;
                    want = d.Mode == ClawMode.Guard ? GuardWristWant(a) : d.Target;
                }
                float spring = d.Spring > 0f ? d.Spring : SeaShrimpDirector.ArmSpring;
                float damping = d.Damping > 0f ? d.Damping : SeaShrimpDirector.ArmDamping;
                ArmSolves[a] = Arms[a].Solve(shoulder, want, spring, damping, bendSign);

                //螯体姿态：撑地时螯尖指向撑点（拄地承重读数），
                //其余情形沿前臂方向 + 指令偏置，守位钳口微垂
                float wantRot;
                if (planted) {
                    wantRot = (plantPos[a] - ArmSolves[a].Wrist)
                        .SafeNormalize(HeadDown).ToRotation();
                }
                else {
                    wantRot = ArmSolves[a].ForeDir.ToRotation() + d.ClawPoseOffset
                        + (d.Mode == ClawMode.Guard ? 0.22f * DownSign : 0f);
                }
                ClawRot[a] = ClawRot[a].AngleLerp(wantRot, 0.2f);
                ClawOpen[a] = MathHelper.Lerp(ClawOpen[a], MathHelper.Clamp(d.ClawOpen, 0f, 1f), 0.24f);
            }
        }

        /// <summary>
        /// 撑地更新：头前下方探地作撑点，偏离过远或超出可及就迈撑（双螯交替，带抬弧），
        /// 返回腕的期望位置（螯体悬在撑点上方，尖端拄地）。探不到地退回折叠守位
        /// </summary>
        private bool UpdatePlant(int armIndex, out Vector2 wristWant) {
            Vector2 down = HeadDown;
            Vector2 forward = Nodes[0].Forward;
            Vector2 shoulder = ShoulderWorld(armIndex);
            //近臂撑得靠前，远臂靠后错开
            Vector2 probeFrom = Nodes[0].Pos + forward * (92f - armIndex * 34f) + down * 8f;
            bool hit = ShrimpTerrain.RaycastSurface(probeFrom, down, 175f, out Vector2 desired);
            if (!hit) {
                plantValid[armIndex] = false;
                plantStepT[armIndex] = -1f;
                wristWant = GuardWristWant(armIndex);
                return false;
            }

            if (!plantValid[armIndex]) {
                plantValid[armIndex] = true;
                plantPos[armIndex] = desired;
                plantStepT[armIndex] = -1f;
            }

            if (plantStepT[armIndex] >= 0f) {
                //迈撑中：抬弧挪向新撑点
                plantStepT[armIndex] += 1f / 9f;
                if (plantStepT[armIndex] >= 1f) {
                    plantStepT[armIndex] = -1f;
                    plantPos[armIndex] = plantTo[armIndex];
                }
                else {
                    float t = plantStepT[armIndex];
                    float ease = t * t * (3f - 2f * t);
                    plantPos[armIndex] = Vector2.Lerp(plantFrom[armIndex], plantTo[armIndex], ease)
                        - down * (MathF.Sin(t * MathHelper.Pi) * 20f);
                }
            }
            else {
                float drift = Vector2.Distance(plantPos[armIndex], desired);
                bool overReach = Vector2.Distance(plantPos[armIndex], shoulder)
                    > (SeaShrimpDirector.ArmBone1 + SeaShrimpDirector.ArmBone2) * 0.96f;
                bool otherStepping = plantStepT[1 - armIndex] >= 0f;
                if ((drift > 64f || overReach) && (!otherStepping || overReach)) {
                    plantFrom[armIndex] = plantPos[armIndex];
                    plantTo[armIndex] = desired;
                    plantStepT[armIndex] = 0f;
                }
            }

            //腕悬在撑点上方，螯尖（腕沿姿态角探出 ~46px）恰好落在撑点
            wristWant = plantPos[armIndex] - down * 42f;
            return true;
        }

        private void UpdateAntennae(bool wet) {
            float time = Main.GlobalTimeWrappedHourly;
            for (int s = 0; s < 2; s++) {
                Antennae[s].Update(AntennaAnchor(s), AntennaRestDir(), time, seed + s * 2.61f, wet);
            }
        }
    }
}
