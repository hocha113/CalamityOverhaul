using CalamityOverhaul.Content.NPCs.BloomsandSerpents.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BloomsandSerpents
{
    /// <summary>
    /// 鳌足骨架（纯表现层）：左右各一条上臂+下臂+掌，肩球锚在 0 号体节两侧缘
    /// （鳌足长在头后第一节，头只管咬）。
    /// 联机契约与步足一致：各端从已同步的头/体节位姿 + 状态相位本地重建，不入网络包。
    ///
    /// 解算沿用残酷月球领主手臂链的约束思路（<c>MLordArmIK</c>）：
    /// 肩→肘→腕永远连续，肘由"弦向角 + 带符号肘偏角"两个标量的临界阻尼弹簧驱动，
    /// 肘偏角硬限位、极向带迟滞（换侧过零连续、不镜像跳变）、小臂相对上臂折叠限位
    /// （永不反折贴臂）、掌相对小臂摆角限位；爪尖目标超出可达域时钳进可达圈，
    /// 骨长恒定不拉伸。跟手速度由姿态 Snap 调弹簧角频率，尖端滞后 = 甩鞭读数。
    ///
    /// 绘制分层：side=-1 爪压暗画在整链之前（远层），side=+1 画在头之后（近层）。
    /// 三件贴图保持源图朝向入库（像素画禁任意角旋转），每件记近端关节像素与骨轴角，
    /// 绘制时 rotation = 世界骨向 − 贴图骨轴角；side=+1 侧整肢水平镜像，两侧解剖对称。
    /// 图鉴沙盒共用本类（Advance + DrawStandalone）。
    /// </summary>
    internal class BssClawRig
    {
        #region 解剖与限位
        /// <summary>
        /// 整肢倍率（战斗端每帧取宿主 NPC.scale，图鉴端保持 1）：节长、锚点、姿态像素量、
        /// 骨件绘制尺寸同乘，编舞函数也传同一值
        /// </summary>
        public float Scale { get; set; } = 1f;

        /// <summary>上臂长（贴图近端→远端关节像素距）</summary>
        private const float UpperLen = 62f;
        /// <summary>下臂长</summary>
        private const float LowerLen = 42f;
        /// <summary>掌长（腕 → 钳口）</summary>
        private const float ChelaLen = 62f;

        /// <summary>肘偏角硬限位 rad（相对肩→腕弦向，约 72°）</summary>
        private const float MaxBend = 1.25f;
        /// <summary>小臂相对上臂折叠限位 rad（肘内角不小于约 48°）</summary>
        private const float MaxRelative = 2.3f;
        /// <summary>掌相对小臂摆角限位 rad</summary>
        private const float ChelaRelMax = 1.2f;
        /// <summary>爪尖目标可达圈（占全肢触及比例）</summary>
        private const float TipReachMin = 0.32f;
        private const float TipReachMax = 0.96f;
        /// <summary>臂链（上臂+下臂）可达圈</summary>
        private const float ArmReachMin = 0.35f;
        private const float ArmReachMax = 0.985f;
        /// <summary>极向迟滞带（|want| 低于此值不换侧）</summary>
        private const float SideHysteresis = 0.2f;
        private const float Dt = 1f / 60f;

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
        private const int JointCount = 4;
        #endregion

        /// <summary>驱动一帧的环境包（战斗端从 ctx 建，图鉴端自建）</summary>
        internal struct ClawEnv
        {
            public BssClawCommand Command;
            /// <summary>命令语义相位（护嘴 = 合拢度 / 蹲伏 = 张螯进度）</summary>
            public float Phase;
            /// <summary>猛推包络 0..1（护嘴齐射拍点燃，ctx 自衰减）</summary>
            public float Burst;
            /// <summary>目标点（撕咬）</summary>
            public Vector2 Aim;
            public Vector2 HeadCenter;
            public float HeadRotation;
            /// <summary>爪基所在体节（0 号节）位姿</summary>
            public Vector2 MountCenter;
            public float MountRotation;
            public Vector2 HeadVelocity;
            /// <summary>步态相位（待机探路划动读它）</summary>
            public float GaitPhase;
            public bool AllowDust;
        }

        private struct Limb
        {
            public Vector2[] Joints;
            /// <summary>平滑爪尖（姿态目标的一阶滞后）</summary>
            public Vector2 TipSmooth;
            public float BladeSmooth;
            /// <summary>撕咬钳合内部计时（命令持续时 0→1）</summary>
            public float SnatchRamp;
            /// <summary>弦向角（肩→腕方向，弹簧量）</summary>
            public float Chord;
            public float ChordVel;
            /// <summary>肘偏角（带符号，过零连续换侧）</summary>
            public float Bend;
            public float BendVel;
            /// <summary>期望肘向极性 ±1（迟滞锁定）</summary>
            public float DesiredSide;
            public bool Inited;
        }

        //limbs[0] = side +1（近层），limbs[1] = side -1（远层）
        private readonly Limb[] limbs = new Limb[2];
        private BssClawCommand lastCommand = BssClawCommand.Idle;

        private static int SideOf(int limbIndex) => limbIndex == 0 ? 1 : -1;

        #region 驱动
        /// <summary>战斗端更新（客户端与单人；服务端由调用方拦掉）。含指令自动映射：
        /// 状态没显式声明爪指令时，跟随腿的钻沙收拢/死亡瘫软。爪基取 0 号体节位姿，
        /// 体节尚未生成时退回头后颈距处。</summary>
        public void Update(BssStateContext ctx) {
            Scale = ctx.Npc.scale;
            BssClawCommand cmd = ctx.ClawCommand;
            if (cmd == BssClawCommand.Idle) {
                if (ctx.LegCommand == BssLegCommand.Collapse) {
                    cmd = BssClawCommand.Collapse;
                }
                else if (ctx.LegCommand == BssLegCommand.Tuck) {
                    cmd = BssClawCommand.Tuck;
                }
            }

            NPC head = ctx.Npc;
            NPC seg0 = ctx.Segments.Count > 0 ? ctx.Segments[0] : null;
            bool mountOk = seg0 != null && seg0.active;
            Vector2 mountCenter = mountOk
                ? seg0.Center
                : head.Center - BssClawScript.Forward(head.rotation) * (BssDirector.NeckGap * Scale);
            float mountRotation = mountOk ? seg0.rotation : head.rotation;

            ClawEnv env = new() {
                Command = cmd,
                Phase = ctx.ClawPhase,
                Burst = ctx.ClawBurst,
                Aim = ctx.ClawAim,
                HeadCenter = head.Center,
                HeadRotation = head.rotation,
                MountCenter = mountCenter,
                MountRotation = mountRotation,
                HeadVelocity = head.velocity,
                GaitPhase = ctx.GaitPhase,
                AllowDust = !Main.dedServ,
            };
            Advance(in env);
        }

        /// <summary>共用推进核心（图鉴端直接喂 env）</summary>
        public void Advance(in ClawEnv env) {
            BssClawFrame frame = new(env.HeadCenter, env.HeadRotation, env.MountCenter, env.MountRotation, Scale);
            for (int li = 0; li < 2; li++) {
                int side = SideOf(li);
                ref Limb limb = ref limbs[li];
                limb.Joints ??= new Vector2[JointCount];

                Vector2 mount = BssClawScript.Mount(in frame, side);
                BssClawPose pose = ResolvePose(side, in frame, in env, ref limb);

                if (!limb.Inited) {
                    limb.TipSmooth = pose.Tip;
                    limb.BladeSmooth = pose.BladeOpen;
                    limb.Inited = true;
                    //弹簧量首帧硬置（下方 snapHard）
                }

                //尖端一阶滞后：跟手速度 = 姿态 Snap，Burst 抬升到近瞬发
                float snap = MathHelper.Clamp(pose.Snap + env.Burst * 0.6f, 0.05f, 0.95f);
                limb.TipSmooth = Vector2.Lerp(limb.TipSmooth, pose.Tip, snap);
                limb.BladeSmooth = MathHelper.Lerp(limb.BladeSmooth, pose.BladeOpen, 0.25f);

                //弹簧角频率随跟手速度：慢摆 9 rad/s，瞬发 24 rad/s
                float omega = MathHelper.Lerp(9f, 24f, snap);
                bool snapHard = limb.Joints[0] == Vector2.Zero
                    || Vector2.DistanceSquared(limb.Joints[0], mount) > 260f * 260f * Scale * Scale;
                Vector2 prevTip = limb.Joints[JointCount - 1];
                SolveLimb(mount, in frame, side, pose.Curl, omega, snapHard, ref limb);

                //高速尖端拖沙丝（各端本地装饰）
                if (env.AllowDust && Main.rand.NextBool(4)) {
                    Vector2 tipVel = limb.Joints[JointCount - 1] - prevTip;
                    if (tipVel.LengthSquared() > 240f * Scale * Scale) {
                        Dust d = Dust.NewDustPerfect(limb.Joints[JointCount - 1], DustID.Sand,
                            tipVel * 0.1f, 140, default, Main.rand.NextFloat(0.7f, 1f));
                        d.noGravity = true;
                    }
                }
            }
            lastCommand = env.Command;
        }

        /// <summary>按指令取姿（装饰性摆动在此叠加，确定性主体在 <see cref="BssClawScript"/>）</summary>
        private BssClawPose ResolvePose(int side, in BssClawFrame frame, in ClawEnv env, ref Limb limb) {
            //命令切换时重置撕咬钳合斜坡
            if (env.Command != lastCommand) {
                limb.SnatchRamp = 0f;
            }

            switch (env.Command) {
                case BssClawCommand.GuardMouth:
                    return BssClawScript.Guard(in frame, side, env.Phase, env.Burst, env.GaitPhase);

                case BssClawCommand.Snatch: {
                    limb.SnatchRamp = MathHelper.Clamp(limb.SnatchRamp + 0.13f, 0f, 1f);
                    return BssClawScript.Snatch(in frame, side, env.Aim, limb.SnatchRamp);
                }

                case BssClawCommand.Brace: {
                    BssClawPose basePose = BssClawScript.Brace(in frame, side, env.Phase);
                    //蓄势绷紧微颤（装饰）
                    Vector2 quiver = new(MathF.Sin(Main.GlobalTimeWrappedHourly * 21f + side * 2.4f) * 2.5f * Scale,
                        MathF.Cos(Main.GlobalTimeWrappedHourly * 17f + side) * 2f * Scale);
                    return new BssClawPose(basePose.Tip + quiver * env.Phase, basePose.Curl, basePose.BladeOpen, basePose.Snap);
                }

                case BssClawCommand.Tuck:
                    return BssClawScript.Tuck(in frame, side);

                case BssClawCommand.Scoop: {
                    BssClawPose basePose = BssClawScript.Scoop(in frame, side, env.Phase);
                    //插沙末段发力微颤（装饰）
                    Vector2 quiver = new(MathF.Sin(Main.GlobalTimeWrappedHourly * 19f + side * 1.9f) * 1.6f * Scale, 0f);
                    return new BssClawPose(basePose.Tip + quiver * env.Phase, basePose.Curl, basePose.BladeOpen, basePose.Snap);
                }

                case BssClawCommand.Fling:
                    return BssClawScript.Fling(in frame, side, env.Phase);

                case BssClawCommand.Collapse: {
                    BssClawPose basePose = BssClawScript.Collapse(in frame, side);
                    //垂软摇晃（装饰）
                    Vector2 sway = new(MathF.Sin(Main.GlobalTimeWrappedHourly * 2f + side * 1.7f) * 8f * Scale, 0f);
                    return new BssClawPose(basePose.Tip + sway, basePose.Curl, basePose.BladeOpen, basePose.Snap);
                }

                default: {
                    BssClawPose basePose = BssClawScript.Idle(in frame, side, env.GaitPhase);
                    //待机呼吸摆（装饰）
                    Vector2 sway = BssClawScript.Lateral(env.HeadRotation, side)
                        * (MathF.Sin(Main.GlobalTimeWrappedHourly * 1.8f + side * 2.1f) * 4f * Scale);
                    float blade = basePose.BladeOpen
                        + 0.08f * MathF.Sin(Main.GlobalTimeWrappedHourly * 2.7f + side);
                    return new BssClawPose(basePose.Tip + sway, basePose.Curl, blade, basePose.Snap);
                }
            }
        }
        #endregion

        #region 约束链解算
        /// <summary>
        /// 层级链：肩带肘（弦向 + 带符号肘偏角双弹簧，硬限位）→ 肘带腕（小臂折叠限位）→
        /// 腕带掌（摆角限位）。爪尖目标先钳进可达圈，骨长恒定，链永不断开。
        /// </summary>
        private void SolveLimb(Vector2 mount, in BssClawFrame frame, int side, float curl, float omega,
            bool snapHard, ref Limb limb) {
            float s = Scale;
            float upper = UpperLen * s;
            float lower = LowerLen * s;
            float chela = ChelaLen * s;
            float reach = BssClawScript.Reach * s;

            //爪尖可达圈钳制
            Vector2 toTip = limb.TipSmooth - mount;
            float tipDist = toTip.Length();
            Vector2 dN = tipDist > 0.5f ? toTip / tipDist : frame.MountForward;
            tipDist = MathHelper.Clamp(tipDist, reach * TipReachMin, reach * TipReachMax);
            Vector2 tipTarget = mount + dN * tipDist;

            //腕目标：掌大体沿弦向铺开，腕落在爪尖后一掌长（略折）处，再钳进臂链可达圈
            float armLen = MathHelper.Clamp(tipDist - chela * 0.9f,
                (upper + lower) * ArmReachMin, (upper + lower) * ArmReachMax);

            //肘偏角幅度：双骨余弦（臂链弦长 armLen）；封顶 MaxBend
            float cosBend = MathHelper.Clamp((upper * upper + armLen * armLen - lower * lower) / (2f * upper * armLen), -1f, 1f);
            float bendMag = Math.Min(MathF.Acos(cosBend), MaxBend);

            //极向：肘朝本侧翻缘外偏并略向前（节肢前附肢的外弓），curl 幅度调收拢度；迟滞防抖
            Vector2 hint = BssClawScript.Lateral(frame.MountRotation, side) + frame.MountForward * 0.4f;
            Vector2 n = new(-dN.Y, dN.X);
            float want = Vector2.Dot(n, hint);
            if (snapHard || limb.DesiredSide == 0f) {
                limb.DesiredSide = want >= 0f ? 1f : -1f;
            }
            else if (want * limb.DesiredSide < -SideHysteresis) {
                limb.DesiredSide = -limb.DesiredSide;
            }
            //换侧临界带内收拢弯度：肘贴着弦线扫过换侧（近直臂横渡），不带深折叠跨越
            float sideBlend = MathHelper.Clamp(Math.Abs(want) / 0.45f, 0f, 1f);
            sideBlend = 0.25f + 0.75f * sideBlend * sideBlend * (3f - 2f * sideBlend);
            float curlBlend = MathHelper.Lerp(0.55f, 1f, MathHelper.Clamp(Math.Abs(curl), 0f, 1f));
            float chordTarget = dN.ToRotation();
            float bendTarget = bendMag * sideBlend * curlBlend * limb.DesiredSide;

            if (snapHard) {
                limb.Chord = chordTarget;
                limb.ChordVel = 0f;
                limb.Bend = bendTarget;
                limb.BendVel = 0f;
            }
            else {
                SpringAngle(ref limb.Chord, ref limb.ChordVel, chordTarget, omega);
                SpringScalar(ref limb.Bend, ref limb.BendVel, bendTarget, omega * 1.15f);
            }
            limb.Bend = MathHelper.Clamp(limb.Bend, -MaxBend, MaxBend);

            //肩带肘
            Vector2 upperDir = (limb.Chord + limb.Bend).ToRotationVector2();
            Vector2 elbow = mount + upperDir * upper;

            //肘带腕：小臂从肘指向弦上的腕目标，折叠限位
            Vector2 wristTarget = mount + dN * armLen;
            Vector2 toWrist = wristTarget - elbow;
            Vector2 foreDir = toWrist.LengthSquared() > 0.25f ? toWrist.SafeNormalize(upperDir) : upperDir;
            float relative = MathHelper.WrapAngle(foreDir.ToRotation() - upperDir.ToRotation());
            if (Math.Abs(relative) > MaxRelative) {
                float sign = Math.Abs(relative) > 3f ? -limb.DesiredSide : Math.Sign(relative);
                foreDir = (upperDir.ToRotation() + MaxRelative * sign).ToRotationVector2();
            }
            Vector2 wrist = elbow + foreDir * lower;

            //腕带掌：掌指向爪尖目标，相对小臂摆角限位
            Vector2 toTipFromWrist = tipTarget - wrist;
            Vector2 chelaDir = toTipFromWrist.LengthSquared() > 0.25f ? toTipFromWrist.SafeNormalize(foreDir) : foreDir;
            float chelaRel = MathHelper.WrapAngle(chelaDir.ToRotation() - foreDir.ToRotation());
            if (Math.Abs(chelaRel) > ChelaRelMax) {
                chelaDir = (foreDir.ToRotation() + ChelaRelMax * Math.Sign(chelaRel)).ToRotationVector2();
            }
            Vector2 tip = wrist + chelaDir * chela;

            limb.Joints[0] = mount;
            limb.Joints[1] = elbow;
            limb.Joints[2] = wrist;
            limb.Joints[3] = tip;
        }

        /// <summary>临界阻尼标量弹簧</summary>
        private static void SpringScalar(ref float pos, ref float vel, float target, float omega) {
            float x = pos - target;
            float temp = (vel + x * omega) * Dt;
            float decay = MathF.Exp(-omega * Dt);
            vel = (vel - temp * omega) * decay;
            pos = target + (x + temp) * decay;
        }

        /// <summary>临界阻尼角度弹簧（最短弧差）</summary>
        private static void SpringAngle(ref float pos, ref float vel, float target, float omega) {
            float x = MathHelper.WrapAngle(pos - target);
            float temp = (vel + x * omega) * Dt;
            float decay = MathF.Exp(-omega * Dt);
            vel = (vel - temp * omega) * decay;
            pos = MathHelper.WrapAngle(target + (x + temp) * decay);
        }
        #endregion

        #region 绘制
        /// <summary>远层爪（side = -1）：压暗画在整链之前</summary>
        public void DrawBack(SpriteBatch sb, Vector2 screenPos, float fade) {
            DrawLimb(sb, ref limbs[1], SideOf(1), screenPos, fade, 0.8f, Scale);
        }

        /// <summary>近层爪（side = +1）：画在头本体之后盖面</summary>
        public void DrawFront(SpriteBatch sb, Vector2 screenPos, float fade) {
            DrawLimb(sb, ref limbs[0], SideOf(0), screenPos, fade, 1f, Scale);
        }

        /// <summary>图鉴端：双爪按层序（远→近由调用方在蒙皮前后各调一次）</summary>
        public void DrawStandalone(SpriteBatch sb, bool front, Func<float, Color> tintFor) {
            int li = front ? 0 : 1;
            ref Limb limb = ref limbs[li];
            if (!limb.Inited) {
                return;
            }
            DrawLimbCore(sb, ref limb, SideOf(li), Vector2.Zero, tintFor(front ? 1f : 0.8f), Scale);
        }

        private static void DrawLimb(SpriteBatch sb, ref Limb limb, int side, Vector2 screenPos, float fade, float dim, float scale) {
            if (!limb.Inited || fade <= 0.03f) {
                return;
            }
            Vector2 mid = limb.Joints[2];
            Color light = Lighting.GetColor((int)(mid.X / 16f), (int)(mid.Y / 16f));
            Color tint = new Color((byte)(light.R * dim), (byte)(light.G * dim), (byte)(light.B * dim), (byte)255) * fade;
            DrawLimbCore(sb, ref limb, side, screenPos, tint, scale);
        }

        private static void DrawLimbCore(SpriteBatch sb, ref Limb limb, int side, Vector2 screenPos, Color tint, float scale) {
            Texture2D upper = BssHead.ClawUpperAsset?.Value;
            Texture2D lower = BssHead.ClawLowerAsset?.Value;
            Texture2D chela = BssHead.ClawChelaAsset?.Value;
            if (upper == null || lower == null) {
                return;
            }

            //side=+1 肢（前向顺时针侧）镜像：未镜像时指尖在骨轴顺时针侧，镜像后落到逆时针侧 = 朝向体中线
            bool mirror = side > 0;
            DrawPiece(sb, upper, UpperAnchor, limb.Joints[0], limb.Joints[1], 0f, mirror, tint, screenPos, scale);
            DrawPiece(sb, lower, LowerAnchor, limb.Joints[1], limb.Joints[2], 0f, mirror, tint, screenPos, scale);
            if (chela != null) {
                //张合：掌绕腕向指尖侧摆，开得越大钳口越转向中线
                float cock = 0.08f + MathHelper.Clamp(limb.BladeSmooth, 0f, 1f) * 0.35f;
                float fingerSide = mirror ? -1f : 1f;
                DrawPiece(sb, chela, ChelaAnchor, limb.Joints[2], limb.Joints[3], fingerSide * cock, mirror, tint, screenPos, scale);
            }
        }

        /// <summary>
        /// 骨件按整肢倍率等比绘制：近端关节像素钉在 from，贴图骨轴转到 from→to 方向（不按骨长拉伸，
        /// 节长已与贴图关节距一致、同乘倍率）。镜像时原点 x 翻到对称位、骨轴角取 π−a。
        /// </summary>
        private static void DrawPiece(SpriteBatch sb, Texture2D tex, in PieceAnchor anchor, Vector2 from, Vector2 to,
            float extraRot, bool mirror, Color tint, Vector2 screenPos, float scale) {
            Vector2 dir = to - from;
            if (dir.LengthSquared() < 9f) {
                return;
            }
            float axis = mirror ? MathHelper.Pi - anchor.AxisAngle : anchor.AxisAngle;
            Vector2 origin = mirror ? new Vector2(tex.Width - anchor.Proximal.X, anchor.Proximal.Y) : anchor.Proximal;
            SpriteEffects fx = mirror ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            float rot = dir.ToRotation() - axis + extraRot;
            sb.Draw(tex, from - screenPos, null, tint, rot, origin, scale, fx, 0f);
        }
        #endregion
    }
}
