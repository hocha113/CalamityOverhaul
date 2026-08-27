using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Rendering;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord
{
    /// <summary>
    /// 脱出真眼：部件破坏后的独立威胁集群，协同技随集群规模进化
    /// 1 眼：三技轮转（波弹连射/星球短扇/预兆冲撞）；
    /// 2 眼：剪式弧光合拢（封存拍冻结锚点，高位浅角交叉起手后向内收拢，脚下口袋恒安全）；
    /// 3 眼：三角闭环（缓旋三角阵 + 链式死光沿边接力传递）；
    /// 4~5 眼：星芒大阵（环阵驻位→星芒弦依次贯通→环阵收缩+中心交叉爆）。
    /// 全部编排由核心编队时钟确定性推导，核心指令可召其锚定或退避
    /// （弦月重演的锚定窗见 <see cref="UpdateAnchorFormation"/>）。
    /// 保持原版不可伤身份，随核心死亡消散
    /// </summary>
    internal class MoonLordFreeEyeAI : BrutalNPCOverride
    {
        public override int TargetID => NPCID.MoonLordFreeEye;

        /// <summary>单眼出手轮换周期帧</summary>
        internal const int StrikePeriod = 150;
        internal const int RamTelegraph = 30;
        internal const int RamDash = 9;
        /// <summary>接触伤速度门控（契约2）：编队弹簧限速 ≤21，只有冲撞点火(30+)越线——
        /// 巡游换位擦身无伤，带预告的冲撞才是接触威胁</summary>
        internal const float ContactDamageSpeedGate = 22f;
        /// <summary>剪式对扫周期（2 眼）</summary>
        internal const int ScissorPeriod = 430;
        /// <summary>剪式封存拍：此帧冻结锚点与全套束几何，此后眼与束不再回应玩家走位——
        /// 追踪发射器会作废一切涌现缺口（契约3），锁死缺口才真实存在</summary>
        internal const int ScissorCommitBeat = 54;
        /// <summary>剪式出束拍：封存到出束 70 帧引导 + 束自身 20 帧成束，
        /// 合计超出光束类预警预算 <see cref="MLordDirector.BeamTelegraphFrames"/></summary>
        internal const int ScissorFireBeat = 124;
        /// <summary>剪式驻位终拍（束燃尽即散阵回巡游）</summary>
        internal const int ScissorStationEnd = ScissorFireBeat + MLordArcRayProj.TotalLife + 6;
        /// <summary>剪式高位栖角：相对封存点的驻位偏移（水平半距/垂直高度）</summary>
        internal const float ScissorPerchX = 300f;
        internal const float ScissorPerchY = -560f;
        /// <summary>剪式收针陡角：收拢的终点角。终拍两交叉落点收至封存点两侧
        /// ±(|PerchY|/tan(角)−PerchX) ≈ 192px，落点之间的地面口袋（扣除束斜切半宽 ≈83px 后
        /// 净空半宽 ≈109px、口袋顶高 ≈124px）全程无束——束只收不进，所见楔口即安全区</summary>
        internal const float ScissorSteepAngle = 0.85f;
        /// <summary>
        /// 剪式收拢扫角（公平阀，契约3）：两束自浅角（0.30 rad，落点 ±1510px）向内收拢此弧度，
        /// 收针于陡角。旧开扇版自 ±192px 向外加速逃逸，口袋起步的玩家永远追不上束，
        /// 整招对锚点高度及以下零威胁，被判"完全打不到下面的玩家"（2026-08-28 反转扫向）——
        /// 现口袋外的低空与地面各承受一次向心收扫（落点峰值速 ≈13px/f），退入口袋或
        /// 提前穿越已扫区即解；高空 X 交叉自 467px 压至 219px，悬空玩家被向下逼角
        /// </summary>
        internal const float ScissorCloseSweep = 0.55f;
        /// <summary>三角闭环周期（3 眼）</summary>
        internal const int TrianglePeriod = 440;
        /// <summary>星芒大阵周期（4~5 眼）</summary>
        internal const int StarPeriod = 560;
        /// <summary>锚定阵位横向席距与垂直栖高（弦月重演底部声部的交叉角表据此反推）</summary>
        internal const float AnchorSpreadX = 240f;
        internal const float AnchorPerchY = -430f;

        private MLordEyePose pose;
        private int bodyFrameTick;
        private int bodyFrame;
        private float scalePulse = 1f;
        private Player targetPlayer;
        /// <summary>冲撞锁定方向（出手窗内各端确定性推导）</summary>
        private Vector2 ramDir;
        /// <summary>剪式封存锚：封存拍那一帧的玩家位置（各端确定性推导，眼位由服务端同步兜底）</summary>
        private Vector2 scissorAnchor;
        /// <summary>已封存的剪式轮次（-1=未封存），防跨轮沿用陈旧锚点</summary>
        private int scissorRound = -1;
        /// <summary>剪式引导线亮度（仅绘制消费，出束或离开剪式即归零）</summary>
        private float scissorGuideStrength;
        /// <summary>剪式当前束向（引导线与瞳孔姿态共用，与束的缓动扫角同式推导）</summary>
        private float scissorBeamAngle;
        /// <summary>剪式收针终角（引导暗线消费：预告束将收到哪，两暗线之间即口袋）</summary>
        private float scissorGuideEndAngle;
        /// <summary>弦月重演引导线亮度与角组（仅绘制消费，蓄势窗外逐帧归零）</summary>
        private float crescentGuideStrength;
        private int crescentGuideCount;
        private float crescentGuideAngleA;
        private float crescentGuideAngleB;

        public override bool? CanBrutalOverride() {
            return null;
        }

        public override void SetProperty() {
            npc.aiStyle = -1;
            npc.knockBackResist = 0f;
            npc.dontTakeDamage = true;
        }

        public override bool AI() {
            npc.aiStyle = -1;
            npc.netOffset = Vector2.Zero;
            npc.dontTakeDamage = true;

            NPC core = MLordFacts.GetCore(npc);
            if (core == null) {
                if (!VaultUtils.isClient) {
                    npc.life = 0;
                    npc.HitEffect();
                    npc.active = false;
                    npc.netUpdate = true;
                }
                return false;
            }

            for (int i = 0; i < npc.buffImmune.Length; i++) {
                npc.buffImmune[i] = true;
            }

            targetPlayer = Main.player[Math.Clamp(core.target, 0, Main.maxPlayers - 1)];
            //槽位复用等异常取不到覆写时保持null，下游已有空值回退（精确索引缺键会抛出）
            core.TryGetOverride(out MoonLordCoreAI coreAI);
            bool hold = coreAI?.Context?.HoldAllParts ?? false;
            int command = (int)MLordFacts.ReadCoreOverrideAi(core, MLordAiSlots.OvEyeCommand);
            float clock = MLordFacts.ReadCoreOverrideAi(core, MLordAiSlots.OvFormationClock);

            //出生演出
            if (npc.ai[MLordAiSlots.EyeBirthTimer] < 40f) {
                npc.ai[MLordAiSlots.EyeBirthTimer]++;
                UpdateBirth(clock);
                UpdatePresentation(hold: true);
                return false;
            }

            //接触伤速度门控：环绕巡游不带伤（无预告的常驻接触伤=暗亏），冲撞高速窗才开
            bool contactHot = npc.velocity.LengthSquared() > ContactDamageSpeedGate * ContactDamageSpeedGate;
            npc.damage = command == MLordEyeCommand.Retreat || hold || !contactHot ? 0 : 60;

            //剪式/弦月引导线逐帧重申：只有对应蓄势窗把它点亮，离开立即熄灭
            scissorGuideStrength = 0f;
            crescentGuideStrength = 0f;
            crescentGuideCount = 0;

            if (hold || command == MLordEyeCommand.Retreat) {
                //退避收拢：贴核心哀鸣漂浮
                Vector2 tuck = core.Center + new Vector2(
                    (float)Math.Sin(clock * 0.03f + npc.whoAmI) * 130f,
                    -220f + (float)Math.Cos(clock * 0.025f + npc.whoAmI) * 40f);
                SpringTo(tuck, 0.07f, 16f);
            }
            else if (command == MLordEyeCommand.Anchor) {
                UpdateAnchorFormation(core, coreAI);
            }
            else if (IsClaimedByTidal(core, npc.whoAmI)) {
                //掌击态被征作冲撞执行者：速度由核心状态服务端直控，本地只随动姿态
                if (npc.velocity.LengthSquared() > 4f) {
                    pose.PupilAngle = npc.velocity.ToRotation();
                }
                pose.PupilOut = 1f;
                pose.Glow = 1f;
            }
            else {
                UpdateSoloCycle(core, clock);
            }

            UpdatePresentation(hold);

            if (!VaultUtils.isClient && (Main.GameUpdateCount + (uint)npc.whoAmI) % 2 == 0) {
                npc.netUpdate = true;
            }
            return false;
        }

        /// <summary>该 NPC 当前是否被掌击态征作冲撞执行者（无手期真眼代打，正被服务端直控瞬移/冲线）</summary>
        internal static bool IsClaimedByTidal(NPC core, int npcIndex) {
            if (MLordFacts.GetCoreState(core) != MLordStateIndex.TidalPalms
                || !core.TryGetOverride(out MoonLordCoreAI coreAI) || coreAI?.Context == null) {
                return false;
            }
            if (!States.MLordTidalPalmsState.TryGetBeat(coreAI.Context, coreAI.StateTimer, out int slamIndex, out _)) {
                return false;
            }
            Span<int> performers = stackalloc int[States.MLordTidalPalmsState.MaxPerformers];
            int performerCount = States.MLordTidalPalmsState.ResolvePerformers(coreAI.Context, slamIndex, performers);
            for (int i = 0; i < performerCount; i++) {
                if (performers[i] == npcIndex) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 锚定阵位：为核心攻击站桩（弦月重演等），按扫描席位横向散开防叠站。
        /// 弦月核心裸露版自冻结拍起改钉服务端封存的锚点槽——移动中的发射器会作废
        /// 一切涌现缺口（契约3），底部声部的交叉落点必须与玩家走位解耦才可读
        /// </summary>
        private void UpdateAnchorFormation(NPC core, MoonLordCoreAI coreAI) {
            int[] anchorEyes = new int[MLordFacts.MaxFreeEyes];
            int anchorCount = MLordFacts.ScanFreeEyes(core, anchorEyes);
            int anchorOrdinal = 0;
            for (int i = 0; i < anchorCount; i++) {
                if (anchorEyes[i] == npc.whoAmI) {
                    anchorOrdinal = i;
                    break;
                }
            }
            Vector2 anchorBase = targetPlayer.Alives() ? targetPlayer.Center : core.Center;

            bool crescentReplay = MLordFacts.GetCoreState(core) == MLordStateIndex.CrescentClose
                && coreAI?.Context?.CoreExposed == true;
            if (crescentReplay && coreAI.StateTimer >= States.MLordCrescentCloseState.EyeFreezeBeat) {
                //冻结窗：钉死在封存拍写入的锚点槽上（各端共读，远端封存包未到的一两帧
                //沿用旧槽值，由弹簧进给自然抹平）
                anchorBase = new Vector2(
                    MLordFacts.ReadCoreOverrideAi(core, MLordAiSlots.OvAnchorX, anchorBase.X),
                    MLordFacts.ReadCoreOverrideAi(core, MLordAiSlots.OvAnchorY, anchorBase.Y));
            }

            Vector2 anchorPos = anchorBase + new Vector2(
                (anchorOrdinal - (anchorCount - 1) * 0.5f) * AnchorSpreadX, AnchorPerchY);
            SpringTo(anchorPos, 0.08f, 18f);
            pose.PupilAngle = pose.PupilAngle.AngleLerp((anchorBase - npc.Center).ToRotation(), 0.25f);

            if (crescentReplay) {
                UpdateCrescentPose(coreAI.StateTimer, anchorOrdinal, anchorCount);
            }
        }

        /// <summary>
        /// 弦月重演的蓄势与出束姿态：整个蓄势窗瞳孔盯死本眼首道弧的起始扫向
        /// （角度自进态即由角表锁死，罗盘预告 70f+成束 20f 满足光束预警预算），
        /// 冻结拍后亮起本眼弧组的引导线（线在哪、束就在哪，落点几何 42f 读秒），
        /// 出束期眼随刃转
        /// </summary>
        private void UpdateCrescentPose(int stateTimer, int ordinal, int eyeCount) {
            Span<float> starts = stackalloc float[2];
            Span<float> sweeps = stackalloc float[2];
            int arcCount = States.MLordCrescentCloseState.GetEyeReplayArcs(ordinal, eyeCount, starts, sweeps);
            if (arcCount <= 0) {
                return;
            }
            int fireBeat = States.MLordCrescentCloseState.WindupEnd;
            if (stateTimer < fireBeat) {
                pose.PupilAngle = pose.PupilAngle.AngleLerp(starts[0], 0.3f);
                if (stateTimer < States.MLordCrescentCloseState.EyeFreezeBeat) {
                    return;
                }
                pose.Glow = 1f;
                pose.PupilOut = 1f;
                crescentGuideStrength = MathHelper.Clamp(
                    (stateTimer - States.MLordCrescentCloseState.EyeFreezeBeat - 6) / 16f, 0f, 1f);
                if (stateTimer >= fireBeat - 10) {
                    //末拍收细定格（出束前不熄灭），与剪式/虚空撕裂引导同式
                    crescentGuideStrength = 0.6f;
                }
                crescentGuideCount = arcCount;
                crescentGuideAngleA = starts[0];
                crescentGuideAngleB = arcCount > 1 ? starts[1] : 0f;
                return;
            }
            if (stateTimer < fireBeat + MLordArcRayProj.TotalLife) {
                //出束驻位：眼随首道弧的缓动扫角转（与束的角进给同式推导）
                float sweepT = MathHelper.Clamp(
                    (stateTimer - fireBeat - MLordArcRayProj.ExpandTime) / (float)MLordArcRayProj.SweepFrames, 0f, 1f);
                pose.PupilAngle = pose.PupilAngle.AngleLerp(
                    starts[0] + sweeps[0] * VaultUtils.EaseInOutCubic(sweepT), 0.25f);
                pose.Glow = 1f;
                pose.PupilOut = 1f;
            }
        }

        #region 集群自主循环

        /// <summary>集群调度：按规模选协同技，1 眼回落三技轮转</summary>
        private void UpdateSoloCycle(NPC core, float clock) {
            //目标失效退避贴核心
            if (!targetPlayer.Alives()) {
                Vector2 tuck = core.Center + new Vector2((float)Math.Sin(clock * 0.03f + npc.whoAmI) * 130f, -220f);
                SpringTo(tuck, 0.07f, 16f);
                return;
            }
            int[] eyes = new int[MLordFacts.MaxFreeEyes];
            int eyeCount = MLordFacts.ScanFreeEyes(core, eyes);
            if (eyeCount <= 0) {
                return;
            }
            //本眼在扫描序中的席位（whoAmI 升序，各端一致）
            int ordinal = 0;
            for (int i = 0; i < eyeCount && i < eyes.Length; i++) {
                if (eyes[i] == npc.whoAmI) {
                    ordinal = i;
                    break;
                }
            }

            if (eyeCount >= 4) {
                UpdateStarArray(core, clock, eyes, eyeCount, ordinal);
            }
            else if (eyeCount == 3) {
                UpdateTriangleRelay(clock, eyes, ordinal);
            }
            else if (eyeCount == 2) {
                UpdateScissorSweep(clock, ordinal);
            }
            else {
                UpdateLoneCycle(clock, eyeCount, ordinal);
            }
        }

        /// <summary>单眼：编队环绕 + 轮流出手（三技轮转）</summary>
        private void UpdateLoneCycle(float clock, int eyeCount, int ordinal) {
            int strikeRound = (int)(clock / StrikePeriod);
            int strikerOrdinal = strikeRound % eyeCount;
            int strikePhase = (int)(clock % StrikePeriod);

            //非出手位：绕玩家编队环绕（相位按席位均分）
            if (strikerOrdinal != ordinal || strikePhase < 24) {
                OrbitRest(clock, ordinal, eyeCount);
                return;
            }

            //出手窗：技型 =（轮次+席位）% 3 轮转
            int strikeKind = (strikeRound + ordinal) % 3;
            switch (strikeKind) {
                case 0:
                    StrikeBoltBurst(strikePhase);
                    break;
                case 1:
                    StrikeOrbFan(strikePhase);
                    break;
                default:
                    StrikeRam(strikePhase);
                    break;
            }
        }

        /// <summary>休整轨道：绕玩家椭圆环绕（相位按席位均分）</summary>
        private void OrbitRest(float clock, int ordinal, int eyeCount) {
            float angle = clock * 0.014f + MathHelper.TwoPi / Math.Max(eyeCount, 1) * ordinal;
            Vector2 orbit = targetPlayer.Center + angle.ToRotationVector2() * new Vector2(430f, 330f);
            SpringTo(orbit, 0.06f, 15f);
            pose.PupilAngle = pose.PupilAngle.AngleLerp((targetPlayer.Center - npc.Center).ToRotation(), 0.2f);
        }

        /// <summary>
        /// 2 眼·剪式弧光合拢：封存拍冻结锚点，两眼锁上封存点头顶两翼的高位栖角，
        /// 远端浅角交叉起手（亮引导线即起手承诺，暗线预告收针终点），随后向内收拢——
        /// 把口袋外的低空与地面逐段扫净，收针于口袋两缘 ±192px。
        /// 封存点脚下的地面口袋全程无束（束只收不进，缺口即所见），退入口袋即解。
        /// 旧开扇版（陡角起手向外张开）落点自 ±192px 向外加速逃逸，追不上任何口袋
        /// 起步的玩家，对锚点高度以下零威胁，2026-08-28 反转为收拢；更旧的追踪合拢版
        /// （束随眼追玩家）仍然禁止——追踪发射器作废一切涌现缺口（契约3）
        /// </summary>
        private void UpdateScissorSweep(float clock, int ordinal) {
            int phase = (int)(clock % ScissorPeriod);
            float side = ordinal == 0 ? -1f : 1f;

            if (phase >= ScissorStationEnd) {
                scissorRound = -1;
                OrbitRest(clock, ordinal, 2);
                return;
            }

            //封存拍：冻结锚点（预告即承诺，契约2.2）。此后眼与束的几何与玩家走位彻底解耦
            int round = (int)(clock / ScissorPeriod);
            bool committed = phase >= ScissorCommitBeat;
            if (committed && scissorRound != round) {
                scissorRound = round;
                scissorAnchor = targetPlayer.Center;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item15 with { Volume = 0.6f, Pitch = 0.15f, MaxInstances = 3 }, npc.Center);
                }
            }

            //栖位：封存前追随玩家头顶两翼（束未承诺，允许跟位），封存后钉死在冻结锚上
            Vector2 anchorBase = committed ? scissorAnchor : targetPlayer.Center;
            SpringTo(anchorBase + new Vector2(side * ScissorPerchX, ScissorPerchY), 0.085f, 19f);

            //束角表：绝对角，与玩家无关。左眼 0.30→0.85 rad（向内收拢），右眼镜像
            float shallowAngle = ScissorSteepAngle - ScissorCloseSweep;
            float startAngle = side < 0f ? shallowAngle : MathHelper.Pi - shallowAngle;
            float sweep = -side * ScissorCloseSweep;
            float sweepT = MathHelper.Clamp(
                (phase - ScissorFireBeat - MLordArcRayProj.ExpandTime) / (float)MLordArcRayProj.SweepFrames, 0f, 1f);
            scissorBeamAngle = startAngle + sweep * VaultUtils.EaseInOutCubic(sweepT);
            scissorGuideEndAngle = startAngle + sweep;

            if (!committed) {
                pose.PupilAngle = pose.PupilAngle.AngleLerp((targetPlayer.Center - npc.Center).ToRotation(), 0.3f);
                return;
            }

            if (phase < ScissorFireBeat) {
                //蓄势窗：提亮 + 向心星流 + 引导 X 渐亮（线在哪、束就在哪）
                pose.Glow = 1f;
                pose.PupilOut = 1f;
                pose.PupilAngle = pose.PupilAngle.AngleLerp(scissorBeamAngle, 0.3f);
                if (!VaultUtils.isServer) {
                    MLordScreenFX.ConvergeStreak(npc.Center, 210f,
                        (phase - ScissorCommitBeat) / (float)(ScissorFireBeat - ScissorCommitBeat));
                }
                scissorGuideStrength = MathHelper.Clamp((phase - ScissorCommitBeat - 28) / 18f, 0f, 1f);
                if (phase >= ScissorFireBeat - 10) {
                    //末拍收细定格（出束前不熄灭），与虚空撕裂引导同式
                    scissorGuideStrength = 0.6f;
                }
                return;
            }

            if (phase == ScissorFireBeat && !VaultUtils.isClient) {
                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                    ModContent.ProjectileType<MLordArcRayProj>(), MLordDirector.EyeScissorDamage, 0f, Main.myPlayer,
                    npc.whoAmI, startAngle, sweep);
            }

            //出束驻位：眼随刃转
            pose.PupilAngle = pose.PupilAngle.AngleLerp(scissorBeamAngle, 0.25f);
        }

        /// <summary>
        /// 3 眼·三角闭环：等相位缓旋三角阵困住玩家，链式死光沿三边依次接力
        /// 点亮（眼0→眼1→眼2→眼0），玩家须踩着未亮的边换位
        /// </summary>
        private void UpdateTriangleRelay(float clock, int[] eyes, int ordinal) {
            int phase = (int)(clock % TrianglePeriod);

            if (phase < 320) {
                //三角阵位：绕玩家缓旋（阵整体旋转，边的走向随之变化）
                float stationAngle = clock * 0.006f + MathHelper.TwoPi / 3f * ordinal - MathHelper.PiOver2;
                Vector2 station = targetPlayer.Center + stationAngle.ToRotationVector2() * 430f;
                SpringTo(station, 0.09f, 18f);
                pose.PupilAngle = pose.PupilAngle.AngleLerp((targetPlayer.Center - npc.Center).ToRotation(), 0.25f);

                //本眼的出边亮起前提亮（接力预告：光沿三角流动）
                int myEdgeFire = 90 + ordinal * 62;
                if (phase >= myEdgeFire - 30 && phase < myEdgeFire + 34) {
                    pose.Glow = 1f;
                    pose.PupilOut = 1f;
                }

                //接力边生成（席位 0 统一裁定，避免多眼重复放束）
                if (!VaultUtils.isClient && ordinal == 0) {
                    for (int i = 0; i < 3; i++) {
                        if (phase == 90 + i * 62) {
                            Projectile.NewProjectile(npc.GetSource_FromAI(), Main.npc[eyes[i]].Center, Vector2.Zero,
                                ModContent.ProjectileType<MLordEyeLinkProj>(), MLordDirector.EyeLinkDamage, 0f, Main.myPlayer,
                                eyes[i], eyes[(i + 1) % 3], 34);
                        }
                    }
                }
            }
            else {
                OrbitRest(clock, ordinal, 3);
            }
        }

        /// <summary>
        /// 4~5 眼·星芒大阵：环阵驻位缓旋→星芒弦依次贯通（5 眼走五芒星 i→i+2，
        /// 4 眼走对角十字+对边）→环阵收缩压向玩家→阵心交叉爆（各眼隔心对射）→散阵
        /// </summary>
        private void UpdateStarArray(NPC core, float clock, int[] eyes, int eyeCount, int ordinal) {
            int phase = (int)(clock % StarPeriod);

            //阵位半径：380~440 收缩段压向玩家，440~500 弹回
            float radius = 470f;
            if (phase >= 380 && phase < 440) {
                radius = MathHelper.Lerp(470f, 180f, VaultUtils.EaseInOutCubic((phase - 380) / 60f));
            }
            else if (phase >= 440 && phase < 500) {
                radius = MathHelper.Lerp(180f, 470f, (phase - 440) / 60f);
            }

            float slotAngle = clock * 0.004f + MathHelper.TwoPi / eyeCount * ordinal - MathHelper.PiOver2;
            Vector2 station = targetPlayer.Center + slotAngle.ToRotationVector2() * radius;
            SpringTo(station, 0.1f, 21f);
            pose.PupilAngle = pose.PupilAngle.AngleLerp((targetPlayer.Center - npc.Center).ToRotation(), 0.3f);

            //收缩段全员提亮（中心爆预告：环在收拢=离开阵心）
            if (phase >= 380 && phase < 452) {
                pose.Glow = 1f;
                pose.PupilOut = 1f;
            }

            if (VaultUtils.isClient || ordinal != 0) {
                return;
            }

            //星芒弦依次贯通
            for (int k = 0; k < eyeCount; k++) {
                if (phase != 100 + k * 44) {
                    continue;
                }
                int src, dst;
                if (eyeCount >= 5) {
                    //五芒星链序：0→2→4→1→3→0
                    src = 2 * k % 5;
                    dst = (2 * k + 2) % 5;
                }
                else {
                    //4 眼：对角十字先行，再补两条对边
                    src = k switch { 0 => 0, 1 => 1, 2 => 0, _ => 2 };
                    dst = k switch { 0 => 2, 1 => 3, 2 => 1, _ => 3 };
                }
                //端点正被掌击态征用（服务端直控瞬移/冲线）就弃掉这条弦，防活束随执行者横甩
                if (IsClaimedByTidal(core, eyes[src]) || IsClaimedByTidal(core, eyes[dst])) {
                    continue;
                }
                Projectile.NewProjectile(npc.GetSource_FromAI(), Main.npc[eyes[src]].Center, Vector2.Zero,
                    ModContent.ProjectileType<MLordEyeLinkProj>(), MLordDirector.EyeLinkDamage, 0f, Main.myPlayer,
                    eyes[src], eyes[dst], 30);
            }

            //阵心交叉爆：收缩到底一拍，各眼向阵心（玩家当时位）对射双弹，
            //惩罚蹲在阵心不动，收缩本身即预告，移动即可让弹道交叉扑空
            if (phase == 446) {
                foreach (int eyeIndex in eyes.AsSpan(0, eyeCount)) {
                    NPC eye = Main.npc[eyeIndex];
                    Vector2 aim = (targetPlayer.Center - eye.Center).SafeNormalize(Vector2.UnitY);
                    for (int j = -1; j <= 1; j += 2) {
                        Projectile.NewProjectile(eye.GetSource_FromAI(), eye.Center + aim * 30f,
                            aim.RotatedBy(j * 0.12f) * 7.4f, ModContent.ProjectileType<MLordBoltProj>(),
                            MLordDirector.BoltDamage, 0f, Main.myPlayer);
                    }
                }
            }
        }

        /// <summary>技一：三连波弹（36f 节拍，预摆后连射）</summary>
        private void StrikeBoltBurst(int strikePhase) {
            npc.velocity *= 0.93f;
            pose.PupilAngle = pose.PupilAngle.AngleLerp((targetPlayer.Center - npc.Center).ToRotation(), 0.4f);
            pose.Glow = 1f;
            if (!VaultUtils.isClient && (strikePhase == 52 || strikePhase == 72 || strikePhase == 92)) {
                Vector2 aim = (targetPlayer.Center + targetPlayer.velocity * 14f - npc.Center).SafeNormalize(Vector2.UnitY);
                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center + aim * 26f, aim * 8.2f,
                    ModContent.ProjectileType<MLordBoltProj>(), MLordDirector.BoltDamage, 0f, Main.myPlayer);
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item125 with { Volume = 0.5f, Pitch = 0.4f, MaxInstances = 5 }, npc.Center);
                }
            }
        }

        /// <summary>技二：星球短扇（一次持握三球）</summary>
        private void StrikeOrbFan(int strikePhase) {
            npc.velocity *= 0.93f;
            pose.Glow = 1f;
            if (!VaultUtils.isClient && strikePhase == 60) {
                Vector2 aim = (targetPlayer.Center - npc.Center).SafeNormalize(Vector2.UnitY);
                for (int i = -1; i <= 1; i++) {
                    Vector2 offset = aim.RotatedBy(i * 0.4f) * 56f;
                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center + offset, Vector2.Zero,
                        ModContent.ProjectileType<MLordOrbProj>(), MLordDirector.OrbDamage, 0f, Main.myPlayer,
                        npc.whoAmI, 0f, 34 + (i + 1) * 4);
                }
            }
        }

        /// <summary>技三：预兆冲撞（锁向抖动预警→直线贯穿→硬刹）</summary>
        private void StrikeRam(int strikePhase) {
            int ramStart = 60;
            if (strikePhase < ramStart) {
                //预警：锁向 + 高频颤抖
                npc.velocity *= 0.9f;
                if (strikePhase > ramStart - RamTelegraph) {
                    ramDir = (targetPlayer.Center + targetPlayer.velocity * 10f - npc.Center).SafeNormalize(Vector2.UnitY);
                    npc.position += Main.rand.NextVector2Circular(1.6f, 1.6f);
                    pose.PupilAngle = ramDir.ToRotation();
                    pose.PupilOut = 1f;
                    pose.Glow = 1f;
                    if (strikePhase == ramStart - RamTelegraph + 2 && !VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.Zombie102 with { Volume = 0.8f, Pitch = 0.2f, MaxInstances = 4 }, npc.Center);
                    }
                }
            }
            else if (strikePhase == ramStart) {
                npc.velocity = ramDir * 30f;
                if (!VaultUtils.isServer) {
                    MLordScreenFX.Punch(npc.Center, 4f, 8, ramDir);
                }
            }
            else if (strikePhase < ramStart + RamDash) {
                npc.velocity *= 1.015f;
            }
            else {
                npc.velocity *= 0.88f;
            }
        }

        #endregion

        #region 出生与表现

        /// <summary>出生：残口升起 + 甩落星屑</summary>
        private void UpdateBirth(float clock) {
            npc.velocity = Vector2.Lerp(npc.velocity, new Vector2(0f, -3.2f), 0.1f);
            npc.position += Main.rand.NextVector2Circular(1.2f, 1.2f);
            scalePulse = MathHelper.Lerp(scalePulse, 1.15f, 0.1f);
            if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                MLordScreenFX.StarBurst(npc.Center + Main.rand.NextVector2Circular(30f, 30f), 0.3f, 2);
            }
            _ = clock;
        }

        private void UpdatePresentation(bool hold) {
            //本体帧循环
            if (++bodyFrameTick >= 5) {
                bodyFrameTick = 0;
                if (++bodyFrame > 3) {
                    bodyFrame = 0;
                }
            }
            scalePulse = MathHelper.Lerp(scalePulse, 1f + 0.12f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 5f + npc.whoAmI), 0.2f);
            pose.PupilOut = MathHelper.Clamp(MathHelper.Lerp(pose.PupilOut, hold ? 0.3f : 0.7f, 0.06f), 0f, 1f);
            pose.Glow = MathHelper.Lerp(pose.Glow, hold ? 0.1f : 0.4f, 0.08f);
            pose.Broken = false;
            Lighting.AddLight(npc.Center, MLordDirector.Phantasmal.ToVector3() * (0.3f + pose.Glow * 0.4f));
        }

        /// <summary>朝目标点弹簧进给</summary>
        private void SpringTo(Vector2 goal, float gain, float maxSpeed) {
            Vector2 want = (goal - npc.Center) * gain;
            if (want.Length() > maxSpeed) {
                want = want.SafeNormalize(Vector2.Zero) * maxSpeed;
            }
            npc.velocity = Vector2.Lerp(npc.velocity, want, 0.14f);
        }

        #endregion

        #region 死亡与绘制

        public override bool CheckActive() => false;

        public override bool FindFrame(int frameHeight) => false;

        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            MLordDrawHelper.DrawFreeEyeAssembly(spriteBatch, npc, screenPos, in pose, bodyFrame, scalePulse);
            //剪式引导：亮线=起手位（与虚空撕裂引导同式，NPC 默认批次，内部走 A=0 加色），
            //暗线=收针终点，两眼的暗线之间即口袋
            if (scissorGuideStrength > 0.01f) {
                MLordRayRender.DrawGuideLine(npc.Center, scissorBeamAngle, MLordArcRayProj.BeamLength, scissorGuideStrength);
                MLordRayRender.DrawGuideLine(npc.Center, scissorGuideEndAngle, MLordArcRayProj.BeamLength, scissorGuideStrength * 0.35f);
            }
            //弦月重演引导：本眼弧组的起始扫向（至多两道：天穹/底部声部）
            if (crescentGuideCount > 0 && crescentGuideStrength > 0.01f) {
                MLordRayRender.DrawGuideLine(npc.Center, crescentGuideAngleA, MLordArcRayProj.BeamLength, crescentGuideStrength);
                if (crescentGuideCount > 1) {
                    MLordRayRender.DrawGuideLine(npc.Center, crescentGuideAngleB, MLordArcRayProj.BeamLength, crescentGuideStrength);
                }
            }
            return false;
        }

        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            return false;
        }

        #endregion
    }
}
