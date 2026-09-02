using CalamityOverhaul.Content.NPCs.BloomsandSerpents.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BloomsandSerpents
{
    /// <summary>
    /// 鳌足骨架（纯表现层）：左右各一条上臂+下臂+掌，锚在头两侧。
    /// 联机契约与步足一致：各端从已同步的头位姿 + 状态相位本地重建，不入网络包；
    /// 弹幕出生点由状态直接调 <see cref="BssClawScript"/> 同一函数取得，与绘制天然咬合。
    ///
    /// 解算：常曲率弧链——欠伸展的余量按关节权重分配成总转角（基节僵、中段柔），
    /// 3 步割线校弦长 + 端向对齐旋转，读出节肢的整弧弯曲而非绳索；
    /// 逐关节拖拽平滑（尖端滞后 = 甩鞭），跟手速度由姿态 Snap 与 Burst 抬升。
    /// 鳌足画上臂+下臂+掌，掌绕腕微转读作张合。
    ///
    /// 绘制分层：side=-1 爪压暗画在头本体之前（远层），side=+1 画在头之后（近层）。
    /// 三件贴图保持源图朝向入库（像素画禁任意角旋转），每件记近端关节像素与骨轴角，
    /// 绘制时 rotation = 世界骨向 − 贴图骨轴角；side=+1 侧整肢水平镜像，两侧解剖对称。
    /// 图鉴沙盒共用本类（AdvanceStandalone + DrawStandalone）。
    /// </summary>
    internal class BssClawRig
    {
        #region 解剖
        /// <summary>节长表（上臂、下臂、掌；= 各件贴图近端→远端关节像素距，总和 = <see cref="BssClawScript.Reach"/>）</summary>
        private static readonly float[] SegLen = { 62f, 42f, 62f };

        /// <summary>骨件贴图锚：近端关节像素（2x 贴图坐标）与近端→远端骨轴角</summary>
        private readonly struct PieceAnchor
        {
            public readonly Vector2 Proximal;
            public readonly float AxisAngle;

            public PieceAnchor(Vector2 proximal, Vector2 distal) {
                Proximal = proximal;
                AxisAngle = (distal - proximal).ToRotation();
            }
        }

        /// <summary>上臂：肩球在左下 (7,21)，细端在右上 (67,4)</summary>
        private static readonly PieceAnchor UpperAnchor = new(new Vector2(7f, 21f), new Vector2(67f, 4f));
        /// <summary>下臂：肘端在左 (3,11)，腕球在右 (45,5)</summary>
        private static readonly PieceAnchor LowerAnchor = new(new Vector2(3f, 11f), new Vector2(45f, 5f));
        /// <summary>掌：腕在柄底左下 (5,61)，钳口在右上指间 (43,11)；指尖朝贴图 +x，即骨轴顺时针侧</summary>
        private static readonly PieceAnchor ChelaAnchor = new(new Vector2(5f, 61f), new Vector2(43f, 11f));
        /// <summary>关节转角权重（和为 1；基节僵、中段柔）</summary>
        private static readonly float[] TurnW = { 0.22f, 0.45f, 0.33f };
        /// <summary>逐关节拖拽率（越靠尖越滞后 = 甩鞭读数）</summary>
        private static readonly float[] LagRate = { 1f, 0.7f, 0.5f, 0.42f };
        private const int JointCount = 4;
        #endregion

        /// <summary>驱动一帧的环境包（战斗端从 ctx 建，图鉴端自建）</summary>
        internal struct ClawEnv
        {
            public BssClawCommand Command;
            /// <summary>命令语义相位（挥掷 = 本记 0..1 / 祭舞 = 全程 0..1 / 护嘴 = 合拢度）</summary>
            public float Phase;
            /// <summary>猛推包络 0..1（护嘴齐射拍点燃，ctx 自衰减）</summary>
            public float Burst;
            /// <summary>目标点（撕咬）</summary>
            public Vector2 Aim;
            /// <summary>挥掷主甩侧（±1；0 = 双爪同姿）</summary>
            public int ActiveSide;
            public Vector2 HeadCenter;
            public float HeadRotation;
            public Vector2 HeadVelocity;
            public bool AllowDust;
        }

        private struct Limb
        {
            public Vector2[] Joints;
            /// <summary>平滑爪尖（姿态目标的一阶滞后）</summary>
            public Vector2 TipSmooth;
            public float CurlSmooth;
            public float BladeSmooth;
            /// <summary>撕咬钳合内部计时（命令持续时 0→1）</summary>
            public float SnatchRamp;
            public bool Inited;
        }

        //limbs[0] = side +1（近层），limbs[1] = side -1（远层）
        private readonly Limb[] limbs = new Limb[2];
        private BssClawCommand lastCommand = BssClawCommand.Idle;

        private static int SideOf(int limbIndex) => limbIndex == 0 ? 1 : -1;

        #region 驱动
        /// <summary>战斗端更新（客户端与单人；服务端由调用方拦掉）。含指令自动映射：
        /// 状态没显式声明爪指令时，跟随腿的钻沙收拢/死亡瘫软。</summary>
        public void Update(BssStateContext ctx) {
            BssClawCommand cmd = ctx.ClawCommand;
            if (cmd == BssClawCommand.Idle) {
                if (ctx.LegCommand == BssLegCommand.Collapse) {
                    cmd = BssClawCommand.Collapse;
                }
                else if (ctx.LegCommand == BssLegCommand.Tuck) {
                    cmd = BssClawCommand.Tuck;
                }
            }

            ClawEnv env = new() {
                Command = cmd,
                Phase = ctx.ClawPhase,
                Burst = ctx.ClawBurst,
                Aim = ctx.ClawAim,
                ActiveSide = ctx.ClawActiveSide,
                HeadCenter = ctx.Npc.Center,
                HeadRotation = ctx.Npc.rotation,
                HeadVelocity = ctx.Npc.velocity,
                AllowDust = !Main.dedServ,
            };
            Advance(in env);
        }

        /// <summary>共用推进核心（图鉴端直接喂 env）</summary>
        public void Advance(in ClawEnv env) {
            for (int li = 0; li < 2; li++) {
                int side = SideOf(li);
                ref Limb limb = ref limbs[li];
                limb.Joints ??= new Vector2[JointCount];

                Vector2 mount = BssClawScript.Mount(env.HeadCenter, env.HeadRotation, side);
                if (!limb.Inited) {
                    BssClawPose init = BssClawScript.Idle(env.HeadCenter, env.HeadRotation, side);
                    limb.TipSmooth = init.Tip;
                    for (int j = 0; j < JointCount; j++) {
                        limb.Joints[j] = Vector2.Lerp(mount, init.Tip, j / (float)(JointCount - 1));
                    }
                    limb.CurlSmooth = init.Curl;
                    limb.BladeSmooth = init.BladeOpen;
                    limb.Inited = true;
                }

                BssClawPose pose = ResolvePose(li, side, in env, ref limb);

                //尖端一阶滞后：跟手速度 = 姿态 Snap，Burst 抬升到近瞬发
                float snap = MathHelper.Clamp(pose.Snap + env.Burst * 0.6f, 0.05f, 0.95f);
                limb.TipSmooth = Vector2.Lerp(limb.TipSmooth, pose.Tip, snap);
                limb.CurlSmooth = MathHelper.Lerp(limb.CurlSmooth, pose.Curl, 0.2f);
                limb.BladeSmooth = MathHelper.Lerp(limb.BladeSmooth, pose.BladeOpen, 0.25f);

                //弧链解算 + 逐关节拖拽（尖端滞后甩鞭；snap 高时全链跟紧）
                Span<Vector2> solved = stackalloc Vector2[JointCount];
                SolveChain(mount, limb.TipSmooth, limb.CurlSmooth, side, solved);
                for (int j = 0; j < JointCount; j++) {
                    float rate = MathHelper.Lerp(LagRate[j], 1f, snap * 0.7f);
                    limb.Joints[j] = Vector2.Lerp(limb.Joints[j], solved[j], rate);
                }
                //锚点硬钉（拖拽不许把基节拽离头）
                limb.Joints[0] = mount;

                //高速尖端拖沙丝（各端本地装饰）
                if (env.AllowDust && Main.rand.NextBool(4)) {
                    Vector2 tipVel = solved[JointCount - 1] - limb.Joints[JointCount - 1];
                    if (tipVel.LengthSquared() > 240f) {
                        Dust d = Dust.NewDustPerfect(limb.Joints[JointCount - 1], DustID.Sand,
                            tipVel * 0.1f, 140, default, Main.rand.NextFloat(0.7f, 1f));
                        d.noGravity = true;
                    }
                }
            }
            lastCommand = env.Command;
        }

        /// <summary>按指令取姿（装饰性摆动在此叠加，确定性主体在 <see cref="BssClawScript"/>）</summary>
        private BssClawPose ResolvePose(int li, int side, in ClawEnv env, ref Limb limb) {
            //命令切换时重置撕咬钳合斜坡
            if (env.Command != lastCommand) {
                limb.SnatchRamp = 0f;
            }

            switch (env.Command) {
                case BssClawCommand.GuardMouth:
                    return BssClawScript.Guard(env.HeadCenter, env.HeadRotation, side, env.Phase, env.Burst);

                case BssClawCommand.Snatch: {
                    limb.SnatchRamp = MathHelper.Clamp(limb.SnatchRamp + 0.13f, 0f, 1f);
                    return BssClawScript.Snatch(env.HeadCenter, env.HeadRotation, side, env.Aim, limb.SnatchRamp);
                }

                case BssClawCommand.RainFlick:
                    return env.ActiveSide == side
                        ? BssClawScript.Flick(env.HeadCenter, env.HeadRotation, side, env.Phase)
                        : BssClawScript.FlickReady(env.HeadCenter, env.HeadRotation, side);

                case BssClawCommand.Rite:
                    return BssClawScript.Rite(env.HeadCenter, env.HeadRotation, side, env.Phase);

                case BssClawCommand.Tuck:
                    return BssClawScript.Tuck(env.HeadCenter, env.HeadRotation, side);

                case BssClawCommand.Collapse: {
                    BssClawPose basePose = BssClawScript.Collapse(env.HeadCenter, side);
                    //垂软摇晃（装饰）
                    Vector2 sway = new(MathF.Sin(Main.GlobalTimeWrappedHourly * 2f + side * 1.7f) * 8f, 0f);
                    return new BssClawPose(basePose.Tip + sway, basePose.Curl, basePose.BladeOpen, basePose.Snap);
                }

                default: {
                    BssClawPose basePose = BssClawScript.Idle(env.HeadCenter, env.HeadRotation, side);
                    //待机呼吸摆（装饰）
                    Vector2 sway = BssClawScript.Lateral(env.HeadRotation, side)
                        * (MathF.Sin(Main.GlobalTimeWrappedHourly * 1.8f + side * 2.1f) * 5f);
                    float blade = basePose.BladeOpen
                        + 0.08f * MathF.Sin(Main.GlobalTimeWrappedHourly * 2.7f + side);
                    return new BssClawPose(basePose.Tip + sway, basePose.Curl, blade, basePose.Snap);
                }
            }
        }
        #endregion

        #region 弧链解算
        /// <summary>
        /// 常曲率弧链：总转角由欠伸展余量决定（3 步割线校弦长），弯向 = 卷曲符号
        /// x 侧位镜像；末了整链对齐旋转把端点转正到弦向。直臂时退化为直线。
        /// </summary>
        private static void SolveChain(Vector2 mount, Vector2 tip, float curl, int side, Span<Vector2> joints) {
            Vector2 d = tip - mount;
            float dist = MathHelper.Clamp(d.Length(), 30f, BssClawScript.Reach * 0.99f);
            float chordAng = d.ToRotation();
            float bend = (curl >= 0f ? 1f : -1f) * side;

            float slack = 1f - dist / BssClawScript.Reach;
            float theta = slack * 4.4f * (0.45f + 0.55f * Math.Abs(curl));
            theta = MathHelper.Clamp(theta, 0.02f, 5.4f);

            //割线迭代：转角越大弦越短，按实测弦长收敛
            for (int iter = 0; iter < 3; iter++) {
                BuildArc(mount, chordAng, theta, bend, joints);
                float got = (joints[JointCount - 1] - mount).Length();
                if (Math.Abs(got - dist) < 5f) {
                    break;
                }
                theta = MathHelper.Clamp(theta * (got > dist ? 1.25f : 0.78f), 0.02f, 5.4f);
            }

            //端向对齐：整链绕锚点旋转，端点方向压到弦向上
            float corr = MathHelper.WrapAngle(chordAng - (joints[JointCount - 1] - mount).ToRotation());
            if (Math.Abs(corr) > 0.001f) {
                for (int j = 1; j < JointCount; j++) {
                    joints[j] = mount + (joints[j] - mount).RotatedBy(corr);
                }
            }
        }

        /// <summary>铺一条弧链：起始外偏半总转角，逐关节按权重回转</summary>
        private static void BuildArc(Vector2 mount, float chordAng, float theta, float bend, Span<Vector2> joints) {
            float ang = chordAng + bend * theta * 0.5f;
            joints[0] = mount;
            for (int i = 0; i < JointCount - 1; i++) {
                joints[i + 1] = joints[i] + ang.ToRotationVector2() * SegLen[i];
                ang -= bend * theta * TurnW[i];
            }
        }
        #endregion

        #region 绘制
        /// <summary>远层爪（side = -1）：压暗画在头本体之前</summary>
        public void DrawBack(SpriteBatch sb, Vector2 screenPos, float fade) {
            DrawLimb(sb, ref limbs[1], SideOf(1), screenPos, fade, 0.8f);
        }

        /// <summary>近层爪（side = +1）：画在头本体之后盖面</summary>
        public void DrawFront(SpriteBatch sb, Vector2 screenPos, float fade) {
            DrawLimb(sb, ref limbs[0], SideOf(0), screenPos, fade, 1f);
        }

        /// <summary>图鉴端：双爪按层序（远→近由调用方在蒙皮前后各调一次）</summary>
        public void DrawStandalone(SpriteBatch sb, bool front, Func<float, Color> tintFor) {
            int li = front ? 0 : 1;
            ref Limb limb = ref limbs[li];
            if (!limb.Inited) {
                return;
            }
            DrawLimbCore(sb, ref limb, SideOf(li), Vector2.Zero, tintFor(front ? 1f : 0.8f));
        }

        private static void DrawLimb(SpriteBatch sb, ref Limb limb, int side, Vector2 screenPos, float fade, float dim) {
            if (!limb.Inited || fade <= 0.03f) {
                return;
            }
            Vector2 mid = limb.Joints[3];
            Color light = Lighting.GetColor((int)(mid.X / 16f), (int)(mid.Y / 16f));
            Color tint = new Color((byte)(light.R * dim), (byte)(light.G * dim), (byte)(light.B * dim), (byte)255) * fade;
            DrawLimbCore(sb, ref limb, side, screenPos, tint);
        }

        private static void DrawLimbCore(SpriteBatch sb, ref Limb limb, int side, Vector2 screenPos, Color tint) {
            Texture2D upper = BssHead.ClawUpperAsset?.Value;
            Texture2D lower = BssHead.ClawLowerAsset?.Value;
            Texture2D chela = BssHead.ClawChelaAsset?.Value;
            if (upper == null || lower == null) {
                return;
            }

            //side=+1 肢（前向顺时针侧）镜像：未镜像时指尖在骨轴顺时针侧，镜像后落到逆时针侧 = 朝向体中线
            bool mirror = side > 0;
            DrawPiece(sb, upper, UpperAnchor, limb.Joints[0], limb.Joints[1], 0f, mirror, tint, screenPos);
            DrawPiece(sb, lower, LowerAnchor, limb.Joints[1], limb.Joints[2], 0f, mirror, tint, screenPos);
            if (chela != null) {
                //张合：掌绕腕向指尖侧摆，开得越大钳口越转向中线
                float cock = 0.08f + MathHelper.Clamp(limb.BladeSmooth, 0f, 1f) * 0.35f;
                float fingerSide = mirror ? -1f : 1f;
                DrawPiece(sb, chela, ChelaAnchor, limb.Joints[2], limb.Joints[3], fingerSide * cock, mirror, tint, screenPos);
            }
        }

        /// <summary>
        /// 原尺寸骨件：近端关节像素钉在 from，贴图骨轴转到 from→to 方向（不按骨长拉伸，
        /// 节长已与贴图关节距一致）。镜像时原点 x 翻到对称位、骨轴角取 π−a。
        /// </summary>
        private static void DrawPiece(SpriteBatch sb, Texture2D tex, in PieceAnchor anchor, Vector2 from, Vector2 to,
            float extraRot, bool mirror, Color tint, Vector2 screenPos) {
            Vector2 dir = to - from;
            if (dir.LengthSquared() < 9f) {
                return;
            }
            float axis = mirror ? MathHelper.Pi - anchor.AxisAngle : anchor.AxisAngle;
            Vector2 origin = mirror ? new Vector2(tex.Width - anchor.Proximal.X, anchor.Proximal.Y) : anchor.Proximal;
            SpriteEffects fx = mirror ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            float rot = dir.ToRotation() - axis + extraRot;
            sb.Draw(tex, from - screenPos, null, tint, rot, origin, 1f, fx, 0f);
        }
        #endregion
    }
}
