using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Rendering;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.States;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord
{
    /// <summary>
    /// 月总之手：核心状态的忠实提线木偶。编队/姿态各端确定性推导，
    /// 掌击等直控动作由服务端状态写速度、客户端积分跟随。
    /// 接触伤害按速度门控（各端一致），破坏后转蠕动残口
    /// </summary>
    internal class MoonLordHandAI : BrutalNPCOverride
    {
        public override int TargetID => NPCID.MoonLordHand;

        private MLordEyePose pose;
        private float gripFrame;
        private float wriggleTimer;
        private Player targetPlayer;
        /// <summary>掌击预警线强度（蓄势后半段亮起，各端本地推导）</summary>
        private float slamTelegraph;

        //―――― 瘫臂（眼被打爆，手臂本身完好）――――
        /// <summary>吊臂链长 px：近乎伸直的自然下垂，仍在肩部可达环带内</summary>
        private const float LimpChainLength = 570f;
        /// <summary>静止吊角相对正下方的外偏 rad：臂挂在躯干外侧，不折进剪影</summary>
        private const float LimpRestTilt = 0.6f;
        /// <summary>重力回正刚度（摆周期约 0.7 秒，读作沉重的大臂）</summary>
        private const float LimpGravityGain = 0.02f;
        /// <summary>阻力驱动系数：本体越快，瘫臂拖得越靠后</summary>
        private const float LimpDragGain = 0.000636f;
        /// <summary>摆幅上限 rad：完好的手臂不会甩过肩线</summary>
        private const float LimpMaxSwing = 1.2f;
        /// <summary>痉挛周期与单次发作帧长（抽一阵、僵一阵）</summary>
        private const float SpasmPeriod = 110f;
        private const float SpasmBurst = 22f;

        /// <summary>吊臂角（肩→手方向），绕肩定长角摆的唯一自由度</summary>
        private float limpAngle;
        private float limpAngleVel;
        private bool limpInit;

        public override bool? CanBrutalOverride() {
            return null;
        }

        public override void SetProperty() {
            npc.aiStyle = -1;
            npc.knockBackResist = 0f;
            int newMaxLife = (int)(npc.lifeMax * MLordDirector.HandLifeFactor);
            npc.life = npc.lifeMax = newMaxLife;
        }

        public override bool AI() {
            npc.aiStyle = -1;
            npc.netOffset = Vector2.Zero;
            npc.knockBackResist = 0f;

            NPC core = MLordFacts.GetCore(npc);
            //核心失效：坠毁（服务端裁定）
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
            MLordStateIndex coreState = MLordFacts.GetCoreState(core);
            int stateTimer = coreAI?.StateTimer ?? 0;
            bool hold = coreAI?.Context?.HoldAllParts ?? false;
            bool broken = npc.ai[MLordAiSlots.PartBroken] == MLordAiSlots.BrokenMark;

            //速度门控接触伤害（各端同式）；抓捕合掌与掌中处刑期清零，威胁是抓取判定，不叠撞击；
            //掌击入位划线同样免伤：翼位起手是位移不是攻击（伤害窗只在冲线，契约2.3）；
            //爬行探爪同理免伤：抓点是移动不是攻击
            bool graspWindow = coreState == MLordStateIndex.PalmExecution
                || (coreState == MLordStateIndex.MoonBite && MLordMoonBiteState.InClapWindow(stateTimer));
            bool entryDart = coreState == MLordStateIndex.TidalPalms && MLordTidalPalmsState.InBlink(stateTimer);
            bool crawlClaimed = MLordLocomotion.IsClaimed(npc);
            npc.damage = !broken && !graspWindow && !entryDart && !crawlClaimed && npc.velocity.Length() > 24f
                ? MLordDirector.PalmContactDamage : 0;

            if (broken) {
                UpdateBroken(core, stateTimer);
            }
            else {
                UpdateAlive(core, coreAI, coreState, stateTimer, hold);
            }

            //服务端隔帧广播（部件多，错相减半流量）
            if (!VaultUtils.isClient && (Main.GameUpdateCount + (uint)npc.whoAmI) % 2 == 0) {
                npc.netUpdate = true;
            }
            return false;
        }

        #region 存活行为

        private void UpdateAlive(NPC core, MoonLordCoreAI coreAI, MLordStateIndex coreState, int stateTimer, bool hold) {
            bool eyeOpen = ComputeEyeOpen(coreState, stateTimer);
            npc.dontTakeDamage = !eyeOpen;

            //掌击拍：执行者由状态直控（服务端写速度，客户端积分），非执行者走编队
            bool claimedByState = false;
            if (coreState == MLordStateIndex.TidalPalms && coreAI?.Context != null
                && MLordTidalPalmsState.TryGetBeat(coreAI.Context, stateTimer, out int slamIndex, out int sub)) {
                Span<int> performers = stackalloc int[MLordTidalPalmsState.MaxPerformers];
                int performerCount = MLordTidalPalmsState.ResolvePerformers(coreAI.Context, slamIndex, performers);
                for (int i = 0; i < performerCount; i++) {
                    if (performers[i] == npc.whoAmI) {
                        claimedByState = true;
                        UpdateSlamPose(sub);
                        break;
                    }
                }
            }
            //抓捕合掌拍：全体存活掌被征用（判据与服务端一致：窗口内且存活手≥2且目标有效）
            else if (coreState == MLordStateIndex.MoonBite && coreAI?.Context != null
                && MLordMoonBiteState.InClapWindow(stateTimer)
                && coreAI.Context.Parts.AliveHandCount >= 2
                && targetPlayer.Alives()) {
                claimedByState = true;
                UpdateClapPose(core, stateTimer);
            }
            //掌中处刑：抓握手锁死攥握姿态，其余掌回巢旁观
            else if (coreState == MLordStateIndex.PalmExecution
                && (int)MLordFacts.ReadCoreOverrideAi(core, MLordAiSlots.OvGrabHand) - 1 == npc.whoAmI) {
                claimedByState = true;
                UpdateGripPose(core);
            }
            //爬行征用：探爪抓点拽动本体（状态直控优先，爬行系统已避让）
            else if (MLordLocomotion.TryGetClaim(npc, out int crawlPhase, out Vector2 crawlAnchor)) {
                claimedByState = true;
                MLordLocomotion.ApplyHandMotion(npc, crawlPhase, crawlAnchor);
                UpdateCrawlPose(crawlPhase, crawlAnchor);
            }

            if (!claimedByState) {
                slamTelegraph = 0f;
                //编队弹簧（目标钳到肩部可达环带：不拥挤不脱链）；
                //跛行肢常驻下垂：不出爪的时候那条腿也吊不住
                Vector2 rawGoal = ComputeFormationGoal(core, coreAI, coreState, stateTimer, hold);
                if (MLordLocomotion.IsLameLimb(core, npc)) {
                    rawGoal.Y += 26f;
                }
                Vector2 goal = MLordLocomotion.ClampFormationGoal(core, npc, rawGoal);
                Vector2 want = (goal - npc.Center) * 0.06f;
                if (want.Length() > 14f) {
                    want = want.SafeNormalize(Vector2.Zero) * 14f;
                }
                npc.velocity = Vector2.Lerp(npc.velocity, want, 0.16f);

                //每技能一种眼相（看眼即知下一拍），协奏预备窗在其上叠加
                ApplyIdleEyeCue(core, coreAI, coreState, stateTimer, eyeOpen);

                //协奏声部预备：出弹前抬手亮眼张掌（预备动作即弹幕预告，契约2）
                if (coreState == MLordStateIndex.Concerto && coreAI?.Context != null) {
                    int slot = ((int)npc.ai[MLordAiSlots.HandRow] == 1 ? 2 : 0)
                        + ((int)npc.ai[MLordAiSlots.HandSide] == 0 ? 0 : 1);
                    float windup = MLordConcertoState.BeatWindup(coreAI.Context, stateTimer, slot);
                    if (windup > 0f) {
                        pose.Glow = Math.Max(pose.Glow, windup);
                        pose.PupilOut = Math.Max(pose.PupilOut, 0.85f * windup);
                        gripFrame = MathHelper.Lerp(gripFrame, 0f, 0.3f * windup);
                        if (!VaultUtils.isServer) {
                            MLordScreenFX.ConvergeStreak(npc.Center, 130f, windup * 0.6f);
                        }
                    }
                }
            }

            pose.Broken = false;
            Lighting.AddLight(npc.Center, MLordDirector.Phantasmal.ToVector3() * (0.25f + pose.Glow * 0.4f));
        }

        /// <summary>
        /// 编队待机的掌眼预备语言：每门技能一种眼相——扫描入定望头、弦月各盯本弧扫向、
        /// 坍缩五目共盯井位、星陨仰望星图、噬咬饿相明灭、掌击旁观追执行者。
        /// 弱点开阖仍主导亮度（睁眼亮、闭眼暗），技能语言只在各自区间内做文章
        /// </summary>
        private void ApplyIdleEyeCue(NPC core, MoonLordCoreAI coreAI, MLordStateIndex coreState,
            int stateTimer, bool eyeOpen) {
            int slot = ((int)npc.ai[MLordAiSlots.HandRow] == 1 ? 2 : 0)
                + ((int)npc.ai[MLordAiSlots.HandSide] == 0 ? 0 : 1);
            float clock = MLordFacts.ReadCoreOverrideAi(core, MLordAiSlots.OvFormationClock);

            float wantAngle = (targetPlayer.Center - npc.Center).ToRotation();
            float wantOut = eyeOpen ? 0.75f : 0.3f;
            float wantGlow = eyeOpen ? 0.65f : 0.1f;
            float targetGrip = 0.6f;
            float angleGain = 0.25f;

            switch (coreState) {
                case MLordStateIndex.DeathrayScan: {
                    //扫描仪式：火力让渡给头颅——四掌半合入定，瞳孔黯淡地齐望焊接位上的头，
                    //眼暗即打不动（本态弱点全程在头上）
                    wantAngle = (core.Center + MLordDirector.HeadWeldOffset - npc.Center).ToRotation();
                    wantOut = 0.55f;
                    wantGlow = 0.15f + 0.05f * (float)Math.Sin(clock * 0.05f + slot * 1.7f);
                    targetGrip = 1.4f;
                    angleGain = 0.1f;
                    break;
                }
                case MLordStateIndex.CrescentClose: {
                    //弧光支点：出弧前各掌眼盯死自己那道弧的起始扫向（四眼四向的罗盘预告），
                    //蓄力渐亮；出弧后眼随刃转
                    wantAngle = MLordCrescentCloseState.ArcAimAngle(slot, stateTimer);
                    wantOut = 0.95f;
                    float charge = MathHelper.Clamp(stateTimer / (float)MLordCrescentCloseState.WindupEnd, 0f, 1f);
                    wantGlow = 0.3f + 0.6f * charge;
                    targetGrip = 0f;
                    angleGain = 0.3f;
                    break;
                }
                case MLordStateIndex.GravityCollapse: {
                    //引力坍缩：五目共盯一点——井还没开就盯着它将要出现的位置，视线汇聚处即危险处
                    if (coreAI?.Context != null) {
                        Vector2 focus = MLordGravityCollapseState.WellFocusPoint(coreAI.Context, stateTimer);
                        wantAngle = (focus - npc.Center).ToRotation();
                    }
                    wantOut = 0.9f;
                    wantGlow = 0.5f + 0.3f * (float)Math.Sin(clock * 0.09f);
                    targetGrip = 0.3f;
                    angleGain = 0.3f;
                    break;
                }
                case MLordStateIndex.Starfall: {
                    //星陨颂唱：随头仰望天穹星图（全体抬眼=玩家该抬头了），辉光随颂唱轻微明灭
                    Vector2 sky = npc.Center - Vector2.UnitY;
                    if (stateTimer >= MLordStarfallState.WaveOneReveal) {
                        sky = new Vector2(
                            MLordFacts.ReadCoreOverrideAi(core, MLordAiSlots.OvAnchorX, npc.Center.X),
                            MLordFacts.ReadCoreOverrideAi(core, MLordAiSlots.OvAnchorY, npc.Center.Y - 600f));
                    }
                    wantAngle = (sky - npc.Center).ToRotation();
                    wantOut = 0.85f;
                    wantGlow = 0.14f + 0.1f * (float)Math.Sin(clock * 0.11f + slot * 0.8f);
                    targetGrip = 1.6f;
                    angleGain = 0.16f;
                    break;
                }
                case MLordStateIndex.MoonBite: {
                    //噬咬合围：饿相——瞳孔全张死盯猎物，高频明灭像咽口水
                    wantOut = 0.95f;
                    wantGlow = 0.55f + 0.3f * (float)Math.Sin(clock * 0.37f + slot * 1.3f);
                    targetGrip = 2.6f;
                    angleGain = 0.35f;
                    break;
                }
                case MLordStateIndex.TidalPalms: {
                    //掌击非执行位：收拳戒备压暗——出手的那两只才是亮的，明暗即分工；
                    //旁观眼追当前执行者，看眼即知这拍谁在打
                    wantOut = 0.6f;
                    wantGlow = 0.3f;
                    targetGrip = 1.8f;
                    if (coreAI?.Context != null
                        && MLordTidalPalmsState.TryGetBeat(coreAI.Context, stateTimer, out int slamIndex, out _)) {
                        Span<int> performers = stackalloc int[MLordTidalPalmsState.MaxPerformers];
                        int count = MLordTidalPalmsState.ResolvePerformers(coreAI.Context, slamIndex, performers);
                        if (count > 0) {
                            wantAngle = (Main.npc[performers[0]].Center - npc.Center).ToRotation();
                        }
                    }
                    break;
                }
                case MLordStateIndex.PalmExecution: {
                    //处刑旁观位：全暴露地盯着掌中猎物（救援窗口，眼亮可打）
                    int victimIndex = (int)MLordFacts.ReadCoreOverrideAi(core, MLordAiSlots.OvGrabTarget) - 1;
                    if (victimIndex >= 0 && victimIndex < Main.maxPlayers && Main.player[victimIndex].active) {
                        wantAngle = (Main.player[victimIndex].Center - npc.Center).ToRotation();
                    }
                    wantOut = 0.85f;
                    wantGlow = 0.75f;
                    break;
                }
            }

            gripFrame = MathHelper.Lerp(gripFrame, targetGrip, 0.1f);
            pose.PupilAngle = pose.PupilAngle.AngleLerp(wantAngle, angleGain);
            pose.PupilOut = MathHelper.Lerp(pose.PupilOut, wantOut, 0.08f);
            pose.Glow = MathHelper.Lerp(pose.Glow, wantGlow, 0.1f);
        }

        /// <summary>掌击子相位姿态（入位划线/预警张掌/冲线握拳/硬直摊开）</summary>
        private void UpdateSlamPose(int sub) {
            if (sub < MLordTidalPalmsState.BlinkLen) {
                //入位划线：面朝行进向，起步与落位各一记星尘（位移可见，不做廉价闪现）
                gripFrame = MathHelper.Lerp(gripFrame, 1f, 0.2f);
                if (npc.velocity.LengthSquared() > 9f) {
                    pose.PupilAngle = npc.velocity.ToRotation();
                }
                pose.PupilOut = MathHelper.Lerp(pose.PupilOut, 0.8f, 0.15f);
                pose.Glow = MathHelper.Lerp(pose.Glow, 0.7f, 0.15f);
                slamTelegraph = 0f;
                if (!VaultUtils.isServer && (sub == 1 || sub == MLordTidalPalmsState.BlinkLen - 1)) {
                    MLordScreenFX.StarBurst(npc.Center, 0.55f, 6);
                }
            }
            else if (MLordTidalPalmsState.InWindup(sub)) {
                gripFrame = MathHelper.Lerp(gripFrame, 0f, 0.3f);
                pose.PupilAngle = pose.PupilAngle.AngleLerp((targetPlayer.Center - npc.Center).ToRotation(), 0.4f);
                pose.PupilOut = MathHelper.Lerp(pose.PupilOut, 1f, 0.2f);
                pose.Glow = MathHelper.Lerp(pose.Glow, 1f, 0.2f);
                //蓄势后半段亮起冲线预警
                float windupT = (sub - MLordTidalPalmsState.BlinkLen) / (float)MLordTidalPalmsState.WindupLen;
                slamTelegraph = MathHelper.Clamp((windupT - 0.45f) / 0.4f, 0f, 1f);
            }
            else if (MLordTidalPalmsState.InDash(sub)) {
                gripFrame = MathHelper.Lerp(gripFrame, 3f, 0.5f);
                pose.PupilAngle = npc.velocity.ToRotation();
                pose.PupilOut = 1f;
                pose.Glow = 1f;
                slamTelegraph = 0f;
                //冲线星尘剥落
                if (!VaultUtils.isServer && npc.velocity.Length() > 20f) {
                    PRTLoader.NewParticle<PRT_HeavenfallStar>(
                        npc.Center + Main.rand.NextVector2Circular(40f, 40f),
                        -npc.velocity * Main.rand.NextFloat(0.08f, 0.2f),
                        MLordDirector.Phantasmal, Main.rand.NextFloat(0.5f, 1f))?.Configure(false, Main.rand.Next(12, 20));
                }
            }
            else if (MLordTidalPalmsState.InRecover(sub)) {
                gripFrame = MathHelper.Lerp(gripFrame, 1f, 0.15f);
                pose.PupilOut = MathHelper.Lerp(pose.PupilOut, 0.6f, 0.1f);
                pose.Glow = MathHelper.Lerp(pose.Glow, 0.8f, 0.1f);
                slamTelegraph = 0f;
            }
        }

        /// <summary>抓捕合掌姿态：张掌追踪→锁定导线定格→合拢冲线（速度由状态服务端直控）</summary>
        private void UpdateClapPose(NPC core, int stateTimer) {
            if (MLordMoonBiteState.InClapTelegraph(stateTimer)) {
                //追踪期瞄活目标，锁定期瞄锚点（导线沿瞳孔方向绘制）
                Vector2 aimPoint = targetPlayer.Center;
                if (MLordMoonBiteState.InClapLock(stateTimer)) {
                    aimPoint = new Vector2(
                        MLordFacts.ReadCoreOverrideAi(core, MLordAiSlots.OvAnchorX, aimPoint.X),
                        MLordFacts.ReadCoreOverrideAi(core, MLordAiSlots.OvAnchorY, aimPoint.Y));
                }
                gripFrame = MathHelper.Lerp(gripFrame, 0.2f, 0.25f);
                pose.PupilAngle = pose.PupilAngle.AngleLerp((aimPoint - npc.Center).ToRotation(), 0.4f);
                pose.PupilOut = MathHelper.Lerp(pose.PupilOut, 1f, 0.2f);
                pose.Glow = MathHelper.Lerp(pose.Glow, 1f, 0.16f);
                //导线亮度：追踪期渐亮，锁定期满亮定格
                int sub = stateTimer - MLordMoonBiteState.ClapStart;
                float t = sub / (float)MLordMoonBiteState.ClapTrackLen;
                slamTelegraph = MLordMoonBiteState.InClapLock(stateTimer)
                    ? 1f : MathHelper.Clamp((t - 0.3f) / 0.6f, 0f, 1f);
            }
            else if (MLordMoonBiteState.InClapLunge(stateTimer)) {
                //合拢冲线：全开掌型扑抓，星尘剥落
                gripFrame = MathHelper.Lerp(gripFrame, 0f, 0.5f);
                if (npc.velocity.LengthSquared() > 16f) {
                    pose.PupilAngle = npc.velocity.ToRotation();
                }
                pose.PupilOut = 1f;
                pose.Glow = 1f;
                slamTelegraph = 0f;
                if (!VaultUtils.isServer && npc.velocity.Length() > 20f) {
                    PRTLoader.NewParticle<PRT_HeavenfallStar>(
                        npc.Center + Main.rand.NextVector2Circular(40f, 40f),
                        -npc.velocity * Main.rand.NextFloat(0.08f, 0.2f),
                        MLordDirector.Phantasmal, Main.rand.NextFloat(0.5f, 1f))?.Configure(false, Main.rand.Next(12, 20));
                }
            }
            else {
                //硬刹收势
                gripFrame = MathHelper.Lerp(gripFrame, 1f, 0.12f);
                pose.PupilOut = MathHelper.Lerp(pose.PupilOut, 0.6f, 0.1f);
                pose.Glow = MathHelper.Lerp(pose.Glow, 0.7f, 0.1f);
                slamTelegraph = 0f;
            }
        }

        /// <summary>
        /// 爬行姿态：探爪张掌盯锚点；抓牢攥拳钉死、瞳孔却回头盯死玩家（诡异感的落点）；
        /// 松爪半握回收。跛行肢探爪时眼光黯淡——那条腿连眼都是弱的
        /// </summary>
        private void UpdateCrawlPose(int crawlPhase, Vector2 anchor) {
            slamTelegraph = 0f;
            switch (crawlPhase) {
                case MLordCrawlPhase.Reach: {
                    bool lame = MLordLocomotion.IsLameLimb(MLordFacts.GetCore(npc), npc);
                    gripFrame = MathHelper.Lerp(gripFrame, 0f, 0.35f);
                    pose.PupilAngle = pose.PupilAngle.AngleLerp((anchor - npc.Center).ToRotation(), 0.4f);
                    pose.PupilOut = MathHelper.Lerp(pose.PupilOut, 0.95f, 0.2f);
                    pose.Glow = MathHelper.Lerp(pose.Glow, lame ? 0.42f : 0.7f, 0.18f);
                    break;
                }
                case MLordCrawlPhase.Planted: {
                    gripFrame = MathHelper.Lerp(gripFrame, 3f, 0.45f);
                    pose.PupilAngle = pose.PupilAngle.AngleLerp((targetPlayer.Center - npc.Center).ToRotation(), 0.18f);
                    pose.PupilOut = MathHelper.Lerp(pose.PupilOut, 0.8f, 0.1f);
                    //收缩发力的那只手亮一拍：与本体被拽动同拍，"是它在拖"一眼可读
                    float surge = MLordLocomotion.GripSurge(npc);
                    pose.Glow = Math.Max(MathHelper.Lerp(pose.Glow, 0.4f, 0.08f), 0.4f + 0.55f * surge);
                    break;
                }
                default:
                    gripFrame = MathHelper.Lerp(gripFrame, 1f, 0.2f);
                    pose.PupilOut = MathHelper.Lerp(pose.PupilOut, 0.5f, 0.1f);
                    pose.Glow = MathHelper.Lerp(pose.Glow, 0.25f, 0.1f);
                    break;
            }
        }

        /// <summary>掌中处刑攥握姿态：紧握拳型，瞳孔盯死被抓者</summary>
        private void UpdateGripPose(NPC core) {
            gripFrame = MathHelper.Lerp(gripFrame, 3f, 0.4f);
            slamTelegraph = 0f;
            int victimIndex = (int)MLordFacts.ReadCoreOverrideAi(core, MLordAiSlots.OvGrabTarget) - 1;
            Vector2 aim = victimIndex >= 0 && victimIndex < Main.maxPlayers && Main.player[victimIndex].active
                ? Main.player[victimIndex].Center : targetPlayer.Center;
            pose.PupilAngle = pose.PupilAngle.AngleLerp((aim - npc.Center).ToRotation(), 0.35f);
            pose.PupilOut = MathHelper.Lerp(pose.PupilOut, 0.9f, 0.15f);
            pose.Glow = MathHelper.Lerp(pose.Glow, 1f, 0.12f);
        }

        /// <summary>硬直期（掌击收势）眼开可打；其余按状态表</summary>
        private bool ComputeEyeOpen(MLordStateIndex coreState, int stateTimer) {
            switch (coreState) {
                case MLordStateIndex.Concerto:
                case MLordStateIndex.CrescentClose:
                case MLordStateIndex.GravityCollapse:
                case MLordStateIndex.MoonBite:
                //掌中处刑全程可打：队友击破抓握之手即提前救人
                case MLordStateIndex.PalmExecution:
                    return true;
                case MLordStateIndex.TidalPalms: {
                    int sub = stateTimer % MLordTidalPalmsState.CycleLen;
                    return MLordTidalPalmsState.InRecover(sub)
                        || stateTimer >= MLordTidalPalmsState.SlamCount * MLordTidalPalmsState.CycleLen;
                }
                default:
                    return false;
            }
        }

        /// <summary>编队目标点：按核心状态与行位（上对/下对）取阵位，四臂分声部不同位</summary>
        private Vector2 ComputeFormationGoal(NPC core, MoonLordCoreAI coreAI, MLordStateIndex coreState,
            int stateTimer, bool hold) {
            float dir = (int)npc.ai[MLordAiSlots.HandSide] == 0 ? -1f : 1f;
            int row = (int)npc.ai[MLordAiSlots.HandRow] == 1 ? 1 : 0;
            int slot = row * 2 + ((int)npc.ai[MLordAiSlots.HandSide] == 0 ? 0 : 1);
            float clock = MLordFacts.ReadCoreOverrideAi(core, MLordAiSlots.OvFormationClock);

            //常态巢位（上对高展、下对外张，X 形构图）+ 同相呼吸：
            //四臂共享一个呼吸相位（上→下 0.15rad 级联），X 分量沿外向展开——
            //一个生物的胸腔起伏，而非四只手各漂各的
            Vector2 homeOffset = row == 0 ? MLordDirector.HandHomeOffset : MLordDirector.LowerHandHomeOffset;
            Vector2 home = core.Center + new Vector2(homeOffset.X * dir, homeOffset.Y);
            Vector2 breath = new((float)Math.Sin(clock * 0.021f - row * 0.15f) * 20f * dir,
                (float)Math.Cos(clock * 0.017f - row * 0.15f) * 22f);

            //目标失效或全体僵直：一律回巢
            if (hold || !targetPlayer.Alives()) {
                return home + breath * 0.3f;
            }

            switch (coreState) {
                case MLordStateIndex.TidalPalms:
                    //非执行手收拢护体（分声部：出手的打、其余的架），冲线的手由状态直控不走此处
                    return core.Center + new Vector2((homeOffset.X - 96f) * dir, homeOffset.Y + 26f)
                        + breath * 0.5f;
                case MLordStateIndex.Concerto: {
                    //即将出手的声部预备抬起（预备动作兼弹幕预告）
                    float windup = coreAI?.Context != null
                        ? MLordConcertoState.BeatWindup(coreAI.Context, stateTimer, slot) : 0f;
                    Vector2 lift = new(26f * dir * windup, -48f * windup);
                    return home + breath + lift;
                }
                case MLordStateIndex.CrescentClose:
                    //上对高位支点持弧，下对低位外张支点（放出封底弧后原位持握）
                    return row == 0
                        ? core.Center + new Vector2(430f * dir, -300f) + breath * 0.4f
                        : core.Center + new Vector2(380f * dir, 142f) + breath * 0.4f;
                case MLordStateIndex.GravityCollapse:
                    //贴核心投掷位：上对肩前，下对腋下
                    return row == 0
                        ? core.Center + new Vector2(260f * dir, -30f) + breath * 0.5f
                        : core.Center + new Vector2(320f * dir, 52f) + breath * 0.5f;
                case MLordStateIndex.MoonBite: {
                    //四臂合围：四掌踞于绕玩家缓旋的方阵四角，半径随呼吸收放。
                    //公平声明：环绕弹簧限速 14 低于接触伤门控 24，本阵只是压迫走位、
                    //不构成伤害环（豁免缺口契约）；伤害窗只在预告完整的合掌冲线
                    float ringAngle = clock * 0.012f + slot * MathHelper.PiOver2;
                    float radius = 470f - (float)Math.Sin(clock * 0.024f) * 90f;
                    return targetPlayer.Center + ringAngle.ToRotationVector2() * radius;
                }
                case MLordStateIndex.DeathrayScan:
                    //扫描期四臂持握转向的仪式阵：上对高举、下对低捧，环拱头部
                    return row == 0
                        ? core.Center + new Vector2(470f * dir, -170f) + breath
                        : core.Center + new Vector2(430f * dir, 112f) + breath;
                default:
                    return home + breath;
            }
        }

        #endregion

        #region 瘫臂行为（眼窝已爆，手臂完好）

        private void UpdateBroken(NPC core, int stateTimer) {
            npc.dontTakeDamage = true;
            npc.damage = 0;
            slamTelegraph = 0f;
            float reanimate = MLordCoreExposureState.ReanimateProgress(core, stateTimer);
            TickWriggle(core, reanimate);
            pose.Broken = true;

            //终局爬行征用：眼窝已爆的手臂原样充当爬行肢（手壳握型随相位）
            if (MLordLocomotion.TryGetClaim(npc, out int crawlPhase, out Vector2 crawlAnchor)) {
                MLordLocomotion.ApplyHandMotion(npc, crawlPhase, crawlAnchor);
                gripFrame = MathHelper.Lerp(gripFrame,
                    crawlPhase == MLordCrawlPhase.Planted ? 3f
                    : crawlPhase == MLordCrawlPhase.Reach ? 0f : 1f, 0.35f);
                pose.WriggleTimer = wriggleTimer;
                //下次回到吊态时按当时的实际位形重新起摆
                limpInit = false;
                return;
            }

            UpdateLimpHang(core, reanimate);
            pose.WriggleTimer = wriggleTimer;
            //松弛半握；复活期收紧成待抓的张掌
            gripFrame = MathHelper.Lerp(gripFrame, MathHelper.Lerp(2f, 0f, reanimate), 0.08f);
        }

        /// <summary>
        /// 瘫臂垂挂：眼是这条手臂的控制器官，眼被打爆手就失去指挥垂下来——
        /// 但手臂本身完好，骨长一点没变，所以走的是绕肩定长角摆而不是自由拖尾：
        /// 手只能沿以肩为心、链长为半径的圆弧荡，天然不可能过伸脱链。
        /// 阻力项让它随本体速度往后飘（奔袭时几条瘫臂在身后拖成一串），
        /// 重力项把它拉回外下方的静止吊位，欠阻尼所以急停后还会晃两下
        /// </summary>
        private void UpdateLimpHang(NPC core, float reanimate) {
            float dir = (int)npc.ai[MLordAiSlots.HandSide] == 0 ? -1f : 1f;
            Vector2 shoulder = MLordLocomotion.ShoulderOf(core, npc);
            float restAngle = new Vector2(dir * (float)Math.Sin(LimpRestTilt),
                (float)Math.Cos(LimpRestTilt)).ToRotation();

            if (!limpInit) {
                limpAngle = (npc.Center - shoulder).SafeNormalize(Vector2.UnitY).ToRotation();
                limpAngleVel = 0f;
                limpInit = true;
            }

            Vector2 tangent = (limpAngle + MathHelper.PiOver2).ToRotationVector2();
            limpAngleVel += Vector2.Dot(-core.velocity, tangent) * LimpDragGain;
            limpAngleVel -= MathHelper.WrapAngle(limpAngle - restAngle) * LimpGravityGain;
            limpAngleVel = MathHelper.Clamp(limpAngleVel * 0.93f, -0.14f, 0.14f);
            limpAngle = MathHelper.WrapAngle(limpAngle + limpAngleVel);
            float swing = MathHelper.WrapAngle(limpAngle - restAngle);
            if (Math.Abs(swing) > LimpMaxSwing) {
                limpAngle = MathHelper.WrapAngle(restAngle + LimpMaxSwing * Math.Sign(swing));
                limpAngleVel *= 0.4f;
            }

            Vector2 hang = shoulder + limpAngle.ToRotationVector2() * LimpChainLength;
            //复活：中枢越过坏掉的眼亲自接管，手臂被强行自吊位提回待抓巢位
            if (reanimate > 0f) {
                Vector2 homeOffset = (int)npc.ai[MLordAiSlots.HandRow] == 1
                    ? MLordDirector.LowerHandHomeOffset : MLordDirector.HandHomeOffset;
                Vector2 home = core.Center + new Vector2(homeOffset.X * dir, homeOffset.Y);
                hang = Vector2.Lerp(hang, home, VaultUtils.EaseOutCubic(reanimate));
            }

            Vector2 goal = MLordLocomotion.ClampFormationGoal(core, npc, hang);
            //角摆本身已给出平滑，这里只做贴合；复活期跟得更紧（抽起来要有劲）
            Vector2 want = (goal - npc.Center) * MathHelper.Lerp(0.12f, 0.3f, reanimate);
            float capSpeed = MathHelper.Lerp(16f, 30f, reanimate);
            if (want.Length() > capSpeed) {
                want = want.SafeNormalize(Vector2.Zero) * capSpeed;
            }
            npc.velocity = Vector2.Lerp(npc.velocity, want, 0.2f);
        }

        /// <summary>
        /// 残口抽搐：不做匀速循环（那是待机动画的节奏），改成偶发痉挛——
        /// 抽一阵、僵一阵，读作神经乱放电。相位取共享编队时钟并按 whoAmI 错开，
        /// 各端一致且四臂不同时抽；复活拍全程剧抖（中枢在强行接线）
        /// </summary>
        private void TickWriggle(NPC core, float reanimate) {
            if (reanimate > 0f) {
                wriggleTimer += 3f;
                return;
            }
            float clock = MLordFacts.ReadCoreOverrideAi(core, MLordAiSlots.OvFormationClock);
            float phase = (clock + npc.whoAmI * 37f) % SpasmPeriod;
            if (phase < SpasmBurst) {
                wriggleTimer += 2f;
            }
        }

        #endregion

        #region 死亡与绘制

        /// <summary>
        /// 防御性兜底：原版 checkDead 的 397 特判先于本钩子执行并自行转破（写 -2 + 生成真眼），
        /// 常规路径走不到这里。若钩子先行（顺序变动），这里镜像原版完成同一件事
        /// </summary>
        public override bool? CheckDead() {
            if (npc.ai[MLordAiSlots.PartBroken] != MLordAiSlots.BrokenMark) {
                npc.ai[MLordAiSlots.PartBroken] = MLordAiSlots.BrokenMark;
                npc.life = npc.lifeMax;
                npc.dontTakeDamage = true;
                npc.netUpdate = true;
                if (!VaultUtils.isClient) {
                    int eye = NPC.NewNPC(npc.GetSource_FromAI(), (int)npc.Center.X, (int)npc.Center.Y,
                        NPCID.MoonLordFreeEye);
                    if (eye < Main.maxNPCs) {
                        Main.npc[eye].ai[MLordAiSlots.PartCoreIndex] = npc.ai[MLordAiSlots.PartCoreIndex];
                        Main.npc[eye].netUpdate = true;
                    }
                }
            }
            return false;
        }

        public override bool CheckActive() => false;

        public override bool FindFrame(int frameHeight) => false;

        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            //掌击冲线预警（沿瞳孔锁向的细光线，各端近似一致）
            if (slamTelegraph > 0.02f) {
                MLordRayRender.DrawGuideLine(npc.Center, pose.PupilAngle, 1100f, slamTelegraph);
            }
            MLordDrawHelper.DrawHandAssembly(spriteBatch, npc, screenPos, in pose, (int)Math.Round(gripFrame));
            return false;
        }

        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            return false;
        }

        #endregion
    }
}
