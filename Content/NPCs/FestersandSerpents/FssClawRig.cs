using CalamityOverhaul.Content.NPCs.FestersandSerpents.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.FestersandSerpents
{
    /// <summary>
    /// 头尾双对变异鳌足骨架（纯表现层）：前对 = 双疮杵（7 节短粗渐膨 + 末端疮锤，
    /// 锚头部两侧，管护嘴/撕咬/夯地），后对 = 双长镰（7 节细长 + 末端镰刃，
    /// 锚尾前体节两侧，管自刈剪切/甩痰）。两对各自左右对称，前后分工。
    /// 联机契约同步足：各端从锚位姿 + 状态相位本地重建，不入网络包；夯点/甩出点
    /// 由状态直接调 <see cref="FssClawScript"/> 同一函数取得，与绘制天然咬合。
    ///
    /// 解算：常曲率弧链（割线校弦长 + 端向对齐），逐关节拖拽平滑（尖端滞后甩鞭）。
    /// 变异层：关节痉挛微颤、灵液滴漏、疮杵逆锥增粗（越到末端越肿）。
    /// 贴图共用 BSS 步足正式稿（ClawClub/ClawSickle 槽位在 FssHead，要专属稿改路径即可）。
    /// </summary>
    internal class FssClawRig
    {
        #region 解剖
        /// <summary>疮杵臂节长（短粗渐膨，总和 = ClubReach）</summary>
        private static readonly float[] ClubSegLen = { 40f, 42f, 40f, 38f, 34f, 30f, 26f };
        /// <summary>长镰臂节长（细长，总和 = SickleReach）</summary>
        private static readonly float[] SickleSegLen = { 36f, 42f, 46f, 48f, 46f, 42f, 40f };
        /// <summary>关节转角权重（和为 1；基节僵中段柔）</summary>
        private static readonly float[] TurnW = { 0.08f, 0.13f, 0.17f, 0.2f, 0.18f, 0.14f, 0.1f };
        /// <summary>逐关节拖拽率（越靠尖越滞后）</summary>
        private static readonly float[] LagRate = { 1f, 0.82f, 0.68f, 0.58f, 0.5f, 0.44f, 0.4f, 0.37f };
        /// <summary>镰刃长</summary>
        private const float BladeLen = 40f;
        private const int JointCount = 8;
        private const int LimbCount = 4;
        /// <summary>痉挛微颤振幅</summary>
        private const float TwitchAmp = 2f;
        #endregion

        /// <summary>驱动一帧的环境包（战斗端从 ctx 建，图鉴端自建）</summary>
        internal struct ClawEnv
        {
            public FssClawCommand Command;
            /// <summary>命令语义相位（夯地/切弧/甩痰 = 各自 0..1 / 护嘴 = 合拢度）</summary>
            public float Phase;
            /// <summary>猛推包络 0..1（护嘴齐射拍点燃，ctx 自衰减）</summary>
            public float Burst;
            /// <summary>目标点（撕咬 / 自刈当前囊肿位）</summary>
            public Vector2 Aim;
            public Vector2 HeadCenter;
            public float HeadRotation;
            /// <summary>后对锚（尾前体节位姿；无效时后对收拢隐藏）</summary>
            public Vector2 RearCenter;
            public float RearRotation;
            public bool RearOk;
            public float HeadScale;
            public bool AllowDust;
        }

        private struct Limb
        {
            public Vector2[] Joints;
            public Vector2 TipSmooth;
            public float CurlSmooth;
            public float BladeSmooth;
            public float SnatchRamp;
            public bool Inited;
        }

        //limbs：0=杵+1（近层）1=杵-1（远层）2=镰+1（近层）3=镰-1（远层）
        private readonly Limb[] limbs = new Limb[LimbCount];
        private FssClawCommand lastCommand = FssClawCommand.Idle;

        private static bool IsClub(int limbIndex) => limbIndex < 2;
        private static int SideOf(int limbIndex) => (limbIndex & 1) == 0 ? 1 : -1;
        private static float[] SegOf(int limbIndex) => IsClub(limbIndex) ? ClubSegLen : SickleSegLen;
        private static float ReachOf(int limbIndex) => IsClub(limbIndex)
            ? FssClawScript.ClubReach : FssClawScript.SickleReach;

        #region 驱动
        /// <summary>战斗端更新（含指令自动映射：未显式声明时跟随腿的收拢/瘫软）</summary>
        public void Update(FssStateContext ctx) {
            FssClawCommand cmd = ctx.ClawCommand;
            if (cmd == FssClawCommand.Idle) {
                if (ctx.LegCommand == FssLegCommand.Collapse) {
                    cmd = FssClawCommand.Collapse;
                }
                else if (ctx.LegCommand == FssLegCommand.Tuck) {
                    cmd = FssClawCommand.Tuck;
                }
            }

            //后对锚：尾前第 4 节（蜕变长节后自动后移，链断/未生成时后对隐藏）
            Vector2 rearCenter = Vector2.Zero;
            float rearRot = 0f;
            bool rearOk = false;
            int rearIndex = ctx.Segments.Count - 4;
            if (rearIndex >= 2 && rearIndex < ctx.Segments.Count) {
                NPC rearSeg = ctx.Segments[rearIndex];
                if (rearSeg != null && rearSeg.active) {
                    rearCenter = rearSeg.Center;
                    rearRot = rearSeg.rotation;
                    rearOk = true;
                }
            }

            ClawEnv env = new() {
                Command = cmd,
                Phase = ctx.ClawPhase,
                Burst = ctx.ClawBurst,
                Aim = ctx.ClawAim,
                HeadCenter = ctx.Npc.Center,
                HeadRotation = ctx.Npc.rotation,
                RearCenter = rearCenter,
                RearRotation = rearRot,
                RearOk = rearOk,
                HeadScale = ctx.Npc.scale,
                AllowDust = !Main.dedServ,
            };
            Advance(in env);
        }

        /// <summary>共用推进核心（图鉴端直接喂 env）</summary>
        public void Advance(in ClawEnv env) {
            for (int li = 0; li < LimbCount; li++) {
                bool club = IsClub(li);
                int side = SideOf(li);
                ref Limb limb = ref limbs[li];

                //后对锚无效：镰对不推进不绘制（Inited 拉掉防旧位姿拉丝）
                if (!club && !env.RearOk) {
                    limb.Inited = false;
                    continue;
                }
                limb.Joints ??= new Vector2[JointCount];

                Vector2 mount = club
                    ? FssClawScript.FrontMount(env.HeadCenter, env.HeadRotation, side, env.HeadScale)
                    : FssClawScript.RearMount(env.RearCenter, env.RearRotation, side, env.HeadScale);

                if (!limb.Inited) {
                    FssClawPose init = club
                        ? FssClawScript.FrontIdle(env.HeadCenter, env.HeadRotation, side, env.HeadScale)
                        : FssClawScript.RearIdle(env.RearCenter, env.RearRotation, side, env.HeadScale);
                    limb.TipSmooth = init.Tip;
                    for (int j = 0; j < JointCount; j++) {
                        limb.Joints[j] = Vector2.Lerp(mount, init.Tip, j / (float)(JointCount - 1));
                    }
                    limb.CurlSmooth = init.Curl;
                    limb.BladeSmooth = init.BladeOpen;
                    limb.Inited = true;
                }

                FssClawPose pose = ResolvePose(li, in env, ref limb);

                float snap = MathHelper.Clamp(pose.Snap + env.Burst * 0.6f, 0.05f, 0.95f);
                limb.TipSmooth = Vector2.Lerp(limb.TipSmooth, pose.Tip, snap);
                limb.CurlSmooth = MathHelper.Lerp(limb.CurlSmooth, pose.Curl, 0.2f);
                limb.BladeSmooth = MathHelper.Lerp(limb.BladeSmooth, pose.BladeOpen, 0.25f);

                Span<Vector2> solved = stackalloc Vector2[JointCount];
                SolveChain(mount, limb.TipSmooth, limb.CurlSmooth, side, li, solved);
                for (int j = 0; j < JointCount; j++) {
                    float rate = MathHelper.Lerp(LagRate[j], 1f, snap * 0.7f);
                    limb.Joints[j] = Vector2.Lerp(limb.Joints[j], solved[j], rate);
                }
                limb.Joints[0] = mount;

                //变异层：痉挛微颤（中后段关节）+ 高速尖端拖金丝 + 关节滴漏
                if (env.AllowDust) {
                    float tw = Main.GlobalTimeWrappedHourly;
                    for (int j = 3; j < JointCount; j++) {
                        limb.Joints[j] += new Vector2(
                            MathF.Sin(tw * 9.1f + j * 2.3f + li * 1.9f),
                            MathF.Sin(tw * 11.3f + j * 1.7f + li)) * (TwitchAmp * (j / (float)JointCount));
                    }
                    Vector2 tipVel = solved[JointCount - 1] - limb.Joints[JointCount - 1];
                    if (tipVel.LengthSquared() > 240f && Main.rand.NextBool(4)) {
                        Dust d = Dust.NewDustPerfect(limb.Joints[JointCount - 1], DustID.Ichor,
                            tipVel * 0.09f, 60, default, Main.rand.NextFloat(0.7f, 1f));
                        d.noGravity = true;
                    }
                    if (Main.rand.NextBool(150)) {
                        Dust drip = Dust.NewDustPerfect(limb.Joints[Main.rand.Next(2, JointCount)],
                            DustID.Ichor, new Vector2(0f, Main.rand.NextFloat(0.6f, 1.4f)),
                            40, default, Main.rand.NextFloat(0.7f, 1f));
                        drip.noGravity = false;
                    }
                }
            }
            lastCommand = env.Command;
        }

        /// <summary>
        /// 按指令取姿——前后分工路由：护嘴/撕咬/夯地归前对（后对回待机），
        /// 自刈/甩痰归后对（前对拄地稳桩）。装饰性摆动在此叠加。
        /// </summary>
        private FssClawPose ResolvePose(int li, in ClawEnv env, ref Limb limb) {
            if (env.Command != lastCommand) {
                limb.SnatchRamp = 0f;
            }
            bool club = IsClub(li);
            int side = SideOf(li);

            switch (env.Command) {
                case FssClawCommand.GuardMouth:
                    return club
                        ? FssClawScript.Guard(env.HeadCenter, env.HeadRotation, side, env.Phase, env.Burst)
                        : RearIdleSway(in env, side);

                case FssClawCommand.Snatch: {
                    if (!club) {
                        return RearIdleSway(in env, side);
                    }
                    limb.SnatchRamp = MathHelper.Clamp(limb.SnatchRamp + 0.13f, 0f, 1f);
                    return FssClawScript.Snatch(env.HeadCenter, env.HeadRotation, side, env.Aim, limb.SnatchRamp);
                }

                case FssClawCommand.Slam:
                    return club
                        ? FssClawScript.Slam(env.HeadCenter, env.HeadRotation, side, env.Phase, env.HeadScale)
                        : RearIdleSway(in env, side);

                case FssClawCommand.Reap:
                    return club
                        ? FssClawScript.FrontBrace(env.HeadCenter, env.HeadRotation, side, env.HeadScale)
                        : FssClawScript.ReapScissor(env.RearCenter, env.RearRotation, side, env.Aim, env.Phase, env.HeadScale);

                case FssClawCommand.Fling:
                    return club
                        ? FssClawScript.FrontBrace(env.HeadCenter, env.HeadRotation, side, env.HeadScale)
                        : FssClawScript.Fling(env.RearCenter, env.RearRotation, side, env.Phase, env.HeadScale);

                case FssClawCommand.Tuck:
                    return club
                        ? FssClawScript.Tuck(env.HeadCenter, env.HeadRotation, side, env.HeadScale)
                        : FssClawScript.Tuck(env.RearCenter, env.RearRotation, side, env.HeadScale);

                case FssClawCommand.Collapse: {
                    Vector2 anchor = club ? env.HeadCenter : env.RearCenter;
                    FssClawPose basePose = FssClawScript.Collapse(anchor, side, ReachOf(li), env.HeadScale);
                    Vector2 sway = new(MathF.Sin(Main.GlobalTimeWrappedHourly * 2f + li * 1.7f) * 9f, 0f);
                    return new FssClawPose(basePose.Tip + sway, basePose.Curl, basePose.BladeOpen, basePose.Snap);
                }

                default:
                    return club ? FrontIdleSway(in env, side) : RearIdleSway(in env, side);
            }
        }

        private static FssClawPose FrontIdleSway(in ClawEnv env, int side) {
            FssClawPose basePose = FssClawScript.FrontIdle(env.HeadCenter, env.HeadRotation, side, env.HeadScale);
            Vector2 sway = FssClawScript.Lateral(env.HeadRotation, side)
                * (MathF.Sin(Main.GlobalTimeWrappedHourly * 1.6f + side * 2.4f) * 5f);
            return new FssClawPose(basePose.Tip + sway, basePose.Curl,
                basePose.BladeOpen + 0.07f * MathF.Sin(Main.GlobalTimeWrappedHourly * 2.5f + side),
                basePose.Snap);
        }

        private static FssClawPose RearIdleSway(in ClawEnv env, int side) {
            FssClawPose basePose = FssClawScript.RearIdle(env.RearCenter, env.RearRotation, side, env.HeadScale);
            Vector2 sway = new(0f, MathF.Sin(Main.GlobalTimeWrappedHourly * 1.9f + side * 1.3f) * 6f);
            return new FssClawPose(basePose.Tip + sway, basePose.Curl, basePose.BladeOpen, basePose.Snap);
        }
        #endregion

        #region 弧链解算
        /// <summary>常曲率弧链（同 BSS 口径）：割线校弦长 + 端向对齐旋转</summary>
        private static void SolveChain(Vector2 mount, Vector2 tip, float curl, int side, int limbIndex, Span<Vector2> joints) {
            float[] segLen = SegOf(limbIndex);
            float reach = ReachOf(limbIndex);

            Vector2 d = tip - mount;
            float dist = MathHelper.Clamp(d.Length(), 34f, reach * 0.99f);
            float chordAng = d.ToRotation();
            float bend = (curl >= 0f ? 1f : -1f) * side;

            float slack = 1f - dist / reach;
            float theta = slack * 4.4f * (0.45f + 0.55f * Math.Abs(curl));
            theta = MathHelper.Clamp(theta, 0.02f, 5.4f);

            for (int iter = 0; iter < 3; iter++) {
                BuildArc(mount, chordAng, theta, bend, segLen, joints);
                float got = (joints[JointCount - 1] - mount).Length();
                if (Math.Abs(got - dist) < 5f) {
                    break;
                }
                theta = MathHelper.Clamp(theta * (got > dist ? 1.25f : 0.78f), 0.02f, 5.4f);
            }

            float corr = MathHelper.WrapAngle(chordAng - (joints[JointCount - 1] - mount).ToRotation());
            if (Math.Abs(corr) > 0.001f) {
                for (int j = 1; j < JointCount; j++) {
                    joints[j] = mount + (joints[j] - mount).RotatedBy(corr);
                }
            }
        }

        private static void BuildArc(Vector2 mount, float chordAng, float theta, float bend,
            float[] segLen, Span<Vector2> joints) {
            float ang = chordAng + bend * theta * 0.5f;
            joints[0] = mount;
            for (int i = 0; i < JointCount - 1; i++) {
                joints[i + 1] = joints[i] + ang.ToRotationVector2() * segLen[i];
                ang -= bend * theta * TurnW[i];
            }
        }
        #endregion

        #region 绘制
        /// <summary>远层（−1 侧的杵与镰）：压暗画在整链之前</summary>
        public void DrawBack(SpriteBatch sb, Vector2 screenPos, float fade) {
            DrawLimb(sb, ref limbs[3], 3, screenPos, fade, 0.8f);
            DrawLimb(sb, ref limbs[1], 1, screenPos, fade, 0.8f);
        }

        /// <summary>近层（+1 侧的杵与镰）：画在整链之上盖面</summary>
        public void DrawFront(SpriteBatch sb, Vector2 screenPos, float fade) {
            DrawLimb(sb, ref limbs[2], 2, screenPos, fade, 1f);
            DrawLimb(sb, ref limbs[0], 0, screenPos, fade, 1f);
        }

        /// <summary>图鉴端（场景坐标，环境色由调用方给）</summary>
        public void DrawStandalone(SpriteBatch sb, bool front, Func<float, Color> tintFor) {
            Color tint = tintFor(front ? 1f : 0.8f);
            if (front) {
                DrawStandaloneLimb(sb, 2, tint);
                DrawStandaloneLimb(sb, 0, tint);
            }
            else {
                DrawStandaloneLimb(sb, 3, tint);
                DrawStandaloneLimb(sb, 1, tint);
            }
        }

        private void DrawStandaloneLimb(SpriteBatch sb, int li, Color tint) {
            ref Limb limb = ref limbs[li];
            if (!limb.Inited) {
                return;
            }
            DrawLimbCore(sb, ref limb, li, Vector2.Zero, tint);
        }

        private static void DrawLimb(SpriteBatch sb, ref Limb limb, int li, Vector2 screenPos, float fade, float dim) {
            if (!limb.Inited || fade <= 0.03f) {
                return;
            }
            Vector2 mid = limb.Joints[3];
            Color light = Lighting.GetColor((int)(mid.X / 16f), (int)(mid.Y / 16f));
            Color tint = new Color((byte)(light.R * dim), (byte)(light.G * dim), (byte)(light.B * dim), (byte)255)
                .MultiplyRGB(FssVfx.SkinMul) * fade;
            DrawLimbCore(sb, ref limb, li, screenPos, tint);
        }

        private static void DrawLimbCore(SpriteBatch sb, ref Limb limb, int li, Vector2 screenPos, Color tint) {
            Texture2D clubTex = FssHead.ClawClubAsset?.Value;
            Texture2D sickleTex = FssHead.ClawSickleAsset?.Value;
            if (clubTex == null || sickleTex == null) {
                return;
            }
            bool club = IsClub(li);
            Texture2D segTex = club ? clubTex : sickleTex;

            for (int i = 0; i < JointCount - 1; i++) {
                float thick = club
                    ? MathHelper.Lerp(1.05f, 1.75f, i / (float)(JointCount - 2))
                    : MathHelper.Lerp(1.25f, 0.62f, i / (float)(JointCount - 2));
                DrawBone(sb, segTex, limb.Joints[i], limb.Joints[i + 1], thick, tint, screenPos);
            }

            Vector2 tip = limb.Joints[JointCount - 1];
            Vector2 dir = tip - limb.Joints[JointCount - 2];
            if (dir.LengthSquared() <= 1f) {
                return;
            }
            float baseAng = dir.ToRotation();

            if (club) {
                //疮锤：末端交叠三短骨成肿块（BladeSmooth = 鼓胀度）
                float swell = 1.2f + MathHelper.Clamp(limb.BladeSmooth, 0f, 1f) * 0.5f;
                for (int k = -1; k <= 1; k++) {
                    Vector2 knot = tip + (baseAng + k * 0.85f).ToRotationVector2() * (16f * swell);
                    DrawBone(sb, clubTex, tip, knot, 2.1f * swell, tint, screenPos);
                }
            }
            else {
                //镰刃：主刃长钩 + 反刃短须（单刃身份）
                float open = 0.35f + MathHelper.Clamp(limb.BladeSmooth, 0f, 1f) * 0.5f;
                Vector2 bladeEnd = tip + (baseAng + open).ToRotationVector2() * BladeLen;
                Vector2 bladeMid = tip + (baseAng + open * 0.45f).ToRotationVector2() * (BladeLen * 0.55f);
                DrawBone(sb, sickleTex, tip, bladeMid, 0.9f, tint, screenPos);
                DrawBone(sb, sickleTex, bladeMid, bladeEnd, 0.62f, tint, screenPos);
                Vector2 barb = tip + (baseAng - 0.5f).ToRotationVector2() * (BladeLen * 0.3f);
                DrawBone(sb, sickleTex, tip, barb, 0.5f, tint, screenPos);
            }
        }

        /// <summary>骨节拉伸绘制：贴图约定尖端朝上，底端锚在关节起点</summary>
        private static void DrawBone(SpriteBatch sb, Texture2D tex, Vector2 from, Vector2 to,
            float thickness, Color tint, Vector2 screenPos) {
            Vector2 dir = to - from;
            float len = dir.Length();
            if (len < 3f) {
                return;
            }
            float rot = dir.ToRotation() + MathHelper.PiOver2;
            Vector2 origin = new(tex.Width * 0.5f, tex.Height - 2f);
            Vector2 scale = new(thickness * 0.7f, len / (tex.Height - 4f));
            sb.Draw(tex, from - screenPos, null, tint, rot, origin, scale, SpriteEffects.None, 0f);
        }
        #endregion
    }
}
