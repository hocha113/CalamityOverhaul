using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.Rendering;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.States
{
    /// <summary>
    /// 投技·蜜牢收网(二阶段)：蜂蜜滞留满标后收网——蜂群拉成收缩旋涡环困住猎物，<br/>
    /// 成茧裹人垂直抬升，女王绕茧穿刺三轮，蜂茧爆散把人抛落<br/>
    /// npc.ai[0/1]=收网锚点 npc.ai[3]=阶段旗(0收网 1已抓 2空挥 3中断)<br/>
    /// 网络形状：判定全在服务端；被抓者位移/受伤/运镜由其客户端(QueenBeeGrabPlayer)读本状态施加
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)QueenBeeStateIndex.SwarmLift, typeof(QueenBeeStateContext))]
    internal class QBSwarmLiftState : QueenBeeStateBase
    {
        public override string StateName => "SwarmLift";
        public override QueenBeeStateIndex StateIndex => QueenBeeStateIndex.SwarmLift;

        #region 节奏常量(玩家侧 QueenBeeGrabPlayer 共用)
        /// <summary>收网 telegraph 时长，也是逃出判定帧</summary>
        internal const int CloseEnd = 52;
        /// <summary>成茧顿帧段末</summary>
        internal const int SeizeEnd = 66;
        /// <summary>抬升段末(茧到顶)</summary>
        internal const int LiftEnd = 116;
        /// <summary>穿刺单轮时长</summary>
        internal const int PassCycle = 44;
        /// <summary>穿刺轮数</summary>
        internal const int PassCount = 3;
        /// <summary>穿刺段末</summary>
        internal const int PassEnd = LiftEnd + PassCycle * PassCount;
        /// <summary>爆散帧(终结拍，释放玩家)；与末轮命中拍(235)隔47帧，不被受击无敌帧(约40)吞终结击</summary>
        internal const int DetonateTick = PassEnd + 34;
        /// <summary>恢复段末，状态退出</summary>
        internal const int RecoverEnd = DetonateTick + 42;
        /// <summary>空挥退出帧</summary>
        internal const int WhiffEnd = CloseEnd + 26;
        /// <summary>保底硬超时</summary>
        internal const int HardTimeout = RecoverEnd + 60;

        /// <summary>收网起始/终末半径</summary>
        private const float RingStartRadius = 380f;
        private const float RingEndRadius = 160f;
        /// <summary>抓取判定半径(略小于终末环半径，判定从宽给玩家)</summary>
        internal const float SeizeRadius = 145f;
        /// <summary>猎物脱离茧心过远视为逃逸(传送/魔镜)，立刻断投</summary>
        internal const float EscapeBreakDist = 420f;
        /// <summary>基准抬升高度，顶部撞天花板时自动压低</summary>
        private const float LiftHeightMax = 400f;

        /// <summary>
        /// 三轮穿刺的命中拍(状态Timer空间)：穿刺段从 LiftEnd+1 起算，轮内 22蓄+8穿=30，<br/>
        /// 即 Timer = LiftEnd + 1 + 轮序*PassCycle + 30，与状态演出拍(tc==30)严格同帧
        /// </summary>
        internal static readonly int[] HurtBeats = [
            LiftEnd + 31,
            LiftEnd + PassCycle + 31,
            LiftEnd + PassCycle * 2 + 31,
        ];
        /// <summary>单轮穿刺伤害系数(×defDamage)</summary>
        internal const float PassDamageScale = 0.5f;
        /// <summary>终结爆散伤害系数(×defDamage)</summary>
        internal const float FinaleDamageScale = 0.85f;
        /// <summary>连段总伤预算(×玩家maxHP)，满血必活的硬上限</summary>
        internal const float TotalDamageBudgetScale = 0.6f;
        #endregion

        #region 跨端暴露(每帧OnUpdate刷新，GrabPlayer/BrutalBeeAI消费)
        /// <summary>茧心当前位置(各端由同步锚点+本地Timer确定性推演)</summary>
        internal Vector2 CocoonCenter { get; private set; }
        /// <summary>猎物whoAmI，-1无</summary>
        internal int VictimWho { get; private set; } = -1;
        /// <summary>已抓住(ai[3]==1)</summary>
        internal bool Seized { get; private set; }
        /// <summary>被抓者当前应被钉在茧心(成茧起到爆散止，中断即失效)</summary>
        internal bool HoldActive { get; private set; }
        /// <summary>爆散已到(终结拍，GrabPlayer在释放沿结算终结伤+下抛)</summary>
        internal bool DetonationReached { get; private set; }
        /// <summary>编队蜂无害窗(收网/茧期不许接触伤蹭死人，爆散恢复)</summary>
        internal bool BeesHarmless { get; private set; } = true;
        #endregion

        //抬升高度(锚点上方天花板探测决定，各端由同步瓦片确定性一致)
        private float liftHeight = LiftHeightMax;
        private bool liftProbed;
        //成茧/空挥重拍的边沿旗标：客户端判定包可能迟到数帧，精确帧相等会漏拍
        private bool seizeCueFired;
        private bool whiffCueFired;

        private Vector2 Anchor(QueenBeeStateContext context)
            => new(context.Npc.ai[0], context.Npc.ai[1]);

        private static float SpinDir(NPC npc) => npc.whoAmI % 2 == 0 ? 1f : -1f;

        public override void OnEnter(QueenBeeStateContext context) {
            base.OnEnter(context);
            NPC npc = context.Npc;
            liftProbed = false;
            seizeCueFired = false;
            whiffCueFired = false;

            if (!VaultUtils.isClient) {
                //锚点定死在猎物当前位置：收网不追踪，逃出去就是空挥
                int victim = context.MarkedPlayerWhoAmI;
                Vector2 anchor = victim >= 0 && victim < Main.maxPlayers && Main.player[victim].Alives()
                    ? Main.player[victim].Center
                    : context.Target.Center;
                npc.ai[0] = anchor.X;
                npc.ai[1] = anchor.Y;
                npc.ai[3] = 0f;
                npc.netUpdate = true;
            }

            QueenBeeMotion.RoarBurst(npc.Center, 1.05f);
        }

        public override IQueenBeeState OnUpdate(QueenBeeStateContext context) {
            NPC npc = context.Npc;
            Timer++;

            Vector2 anchor = Anchor(context);
            int phaseFlag = (int)npc.ai[3];
            VictimWho = context.MarkedPlayerWhoAmI;
            Player victim = VictimWho >= 0 && VictimWho < Main.maxPlayers ? Main.player[VictimWho] : null;

            //天花板探测：确定抬升高度(瓦片全端一致，无需同步)
            if (!liftProbed) {
                liftProbed = true;
                liftHeight = ProbeLiftHeight(anchor);
            }

            //每帧刷新跨端暴露量
            Seized = phaseFlag == 1;
            bool aborted = phaseFlag == 3;
            bool whiffed = phaseFlag == 2;
            CocoonCenter = ComputeCocoonCenter(anchor);
            DetonationReached = Seized && Timer >= DetonateTick;
            HoldActive = Seized && !aborted && Timer >= CloseEnd && Timer < DetonateTick;
            BeesHarmless = !(Seized && Timer >= DetonateTick);

            //服务端裁定：收网判定+异常断投
            if (!VaultUtils.isClient) {
                if (Timer == CloseEnd && phaseFlag == 0) {
                    bool caught = victim.Alives() && victim.Distance(anchor) <= SeizeRadius;
                    npc.ai[3] = caught ? 1f : 2f;
                    npc.netUpdate = true;
                }
                //抓持期猎物死亡/逃逸(魔镜传送等)→立刻断投
                if (Seized && Timer < DetonateTick
                    && (!victim.Alives() || victim.Distance(CocoonCenter) > EscapeBreakDist)) {
                    npc.ai[3] = 3f;
                    npc.netUpdate = true;
                    aborted = true;
                }
            }

            //阶段分发
            if (aborted) {
                UpdateAbort(context, npc);
            }
            else if (Timer <= CloseEnd) {
                UpdateNetClose(context, npc, anchor);
            }
            else if (whiffed) {
                UpdateWhiff(context, npc, anchor);
            }
            else if (Seized) {
                UpdateSeized(context, npc, victim);
            }
            else {
                //客户端等待判定包的空窗：保持终末收网姿态，避免编队闪回
                UpdateVerdictPending(context, npc, anchor);
            }

            //出口：空挥短退/正常演完/保底超时
            if (whiffed && Timer >= WhiffEnd) {
                return new QBRepositionState();
            }
            if (aborted && Timer >= Math.Min(Counter + 20, HardTimeout)) {
                return new QBRepositionState();
            }
            if (Timer >= RecoverEnd || Timer >= HardTimeout) {
                return new QBRepositionState();
            }
            return null;
        }

        #region 幕一 收网
        /// <summary>收缩旋涡环困场：环即telegraph，缺口即活路</summary>
        private void UpdateNetClose(QueenBeeStateContext context, NPC npc, Vector2 anchor) {
            float t = Timer / (float)CloseEnd;
            float radius = MathHelper.SmoothStep(RingStartRadius, RingEndRadius, t);

            context.Swarm.Declare(SwarmFormation.Vortex, anchor, Vector2.UnitX, radius, SpinDir(npc) * 0.05f);
            context.Swarm.PushRibbon(0.9f);
            if (Timer == 1) {
                context.Swarm.PushSnap(2.8f);
            }

            //女王在环外高位督阵，蓄力渐紧
            int side = npc.Center.X < anchor.X ? -1 : 1;
            Vector2 holdPos = anchor + new Vector2(side * 330f, -300f);
            QueenBeeMotion.SpringHover(npc, holdPos, 0.03f, 0.12f, 30f);
            FaceTarget(npc, anchor);
            context.SetChargeState(1, t);

            //收网急鸣，音高随环收紧上行
            if (Timer % 12 == 0) {
                QueenBeeMotion.WingHum(anchor, 0.4f + t * 0.25f, -0.3f + t * 0.7f);
            }
            //补员：茧要裹得够厚
            if (!VaultUtils.isClient && Timer % 12 == 0) {
                context.Swarm.ServerTopUp(24, 3);
            }
        }
        #endregion

        #region 幕二 空挥
        /// <summary>扑空戏：蜂群收拢成空茧散作蜜雾，女王泄劲下沉</summary>
        private void UpdateWhiff(QueenBeeStateContext context, NPC npc, Vector2 anchor) {
            context.Swarm.Declare(SwarmFormation.Absorb, anchor, Vector2.UnitX);
            context.Swarm.PushSnap(3f);

            //空茧散拍：边沿触发防客户端漏拍
            if (!whiffCueFired && Timer >= CloseEnd + 10) {
                whiffCueFired = true;
                SoundEngine.PlaySound(SoundID.NPCHit18 with { Volume = 0.6f, Pitch = -0.45f }, anchor);
                if (!VaultUtils.isServer) {
                    for (int i = 0; i < 6; i++) {
                        PRTLoader.NewParticle<PRT_HoneyMist>(anchor + Main.rand.NextVector2Circular(30f, 30f),
                            Main.rand.NextVector2Circular(1.4f, 1f), QueenBeeMotion.HoneyGold * 0.4f,
                            Main.rand.NextFloat(0.7f, 1.1f));
                    }
                }
            }

            //女王泄劲：轻微下坠飘摆
            npc.velocity *= 0.93f;
            npc.velocity.Y += 0.05f;
            FaceTarget(npc, anchor);
        }

        /// <summary>判定空窗(仅客户端会进)：环保持终末半径，女王原位悬停</summary>
        private void UpdateVerdictPending(QueenBeeStateContext context, NPC npc, Vector2 anchor) {
            context.Swarm.Declare(SwarmFormation.Vortex, anchor, Vector2.UnitX, RingEndRadius, SpinDir(npc) * 0.05f);
            context.Swarm.PushRibbon(0.9f);
            npc.velocity *= 0.9f;
            FaceTarget(npc, anchor);
        }
        #endregion

        #region 幕三 成茧→抬升→穿刺→爆散→恢复
        private void UpdateSeized(QueenBeeStateContext context, NPC npc, Player victim) {
            if (Timer <= SeizeEnd) {
                UpdateSeizeClutch(context, npc);
            }
            else if (Timer <= LiftEnd) {
                UpdateLift(context, npc);
            }
            else if (Timer <= PassEnd) {
                UpdateSkewerPasses(context, npc);
            }
            else if (Timer < DetonateTick) {
                UpdatePreSilence(context, npc);
            }
            else {
                UpdateDetonateAndRecover(context, npc, victim);
            }
        }

        /// <summary>成茧顿帧：蜂群全速砸向猎物裹茧，女王与世界一拍静止</summary>
        private void UpdateSeizeClutch(QueenBeeStateContext context, NPC npc) {
            context.Swarm.Declare(SwarmFormation.Absorb, CocoonCenter, Vector2.UnitX);
            context.Swarm.PushSnap(3.4f);
            context.Swarm.PushRibbon(1f);

            //顿帧感：女王骤停
            npc.velocity *= 0.55f;
            FaceTarget(npc, CocoonCenter);

            //成茧重拍：边沿触发，判定包迟到的客户端也不漏拍
            if (!seizeCueFired) {
                seizeCueFired = true;
                SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.9f, Pitch = 0.15f }, CocoonCenter);
                SoundEngine.PlaySound(SoundID.Zombie125 with { Volume = 0.7f, Pitch = -0.3f, MaxInstances = 2 }, npc.Center);
                QueenBeeMotion.HoneyBurst(CocoonCenter, 1.4f, 12);
                QueenBeeMotion.Shake(CocoonCenter, 5f, 10);
            }
        }

        /// <summary>垂直抬升：茧升女王螺旋伴飞</summary>
        private void UpdateLift(QueenBeeStateContext context, NPC npc) {
            DeclareCocoon(context, 0.38f);

            //女王绕茧螺旋上升
            float angle = SpinDir(npc) * (Timer - SeizeEnd) * 0.085f + 0.6f;
            Vector2 orbit = CocoonCenter + angle.ToRotationVector2() * 215f;
            QueenBeeMotion.SpringHover(npc, orbit, 0.05f, 0.16f, 36f);
            FaceTarget(npc, CocoonCenter);

            //茧滴蜜+托举嗡鸣
            if (!VaultUtils.isServer && Timer % 4 == 0) {
                PRTLoader.NewParticle<PRT_HoneyDrop>(CocoonCenter + Main.rand.NextVector2Circular(26f, 26f),
                    Vector2.UnitY * Main.rand.NextFloat(1f, 2.5f), QueenBeeMotion.AmberDeep,
                    Main.rand.NextFloat(0.6f, 1f));
            }
            if (Timer % 14 == 0) {
                float t = (Timer - SeizeEnd) / (float)(LiftEnd - SeizeEnd);
                QueenBeeMotion.WingHum(CocoonCenter, 0.5f, -0.4f + t * 0.5f);
            }
        }

        /// <summary>三轮穿刺：拉开-late snap蓄力-贯穿茧体-急刹，命中拍全端演出</summary>
        private void UpdateSkewerPasses(QueenBeeStateContext context, NPC npc) {
            DeclareCocoon(context, 0.38f);

            int local = Timer - LiftEnd - 1;
            int passIdx = Math.Clamp(local / PassCycle, 0, PassCount - 1);
            int tc = local % PassCycle;

            //本轮穿刺的轨道方位(确定性)
            float orbitAngle = (passIdx * 2.09f + 0.9f) * SpinDir(npc);
            Vector2 orbitPoint = CocoonCenter + orbitAngle.ToRotationVector2() * 300f;

            if (tc < 22) {
                //蓄力：轨道点定身，末段late-snap反向吸气
                float wind = tc / 22f;
                Vector2 outDir = (orbitPoint - CocoonCenter).SafeNormalize(Vector2.UnitX);
                Vector2 chargePos = orbitPoint + outDir * (float)Math.Pow(wind, 8f) * 70f;
                QueenBeeMotion.SpringHover(npc, chargePos, 0.07f, 0.2f, 42f);
                context.SetChargeState(1, wind);
                context.UseChargePose = tc > 12;
                FaceTarget(npc, CocoonCenter);
                QueenBeeMotion.ChargeGatherFX(npc.Center, wind, 90f);
            }
            else if (tc == 22) {
                //贯穿发射：穿过茧心直line
                Vector2 dir = (CocoonCenter - npc.Center).SafeNormalize(Vector2.UnitY);
                QueenBeeMotion.DashLaunch(npc, dir, 42f, 1.15f);
            }
            else if (tc < 34) {
                //穿刺中：残影拉满，接触伤保持关闭(伤害走猎物端脚本拍)
                context.UseChargePose = true;
                context.PushAfterimage(1f);
                FaceByVelocity(npc);
            }
            else {
                QueenBeeMotion.BrakeHard(npc, 0.72f);
            }

            //命中拍(tc==30，与 HurtBeats 同帧)：全端可见的穿刺反馈，被抓者的受伤由其客户端在同拍结算
            if (tc == 30) {
                QueenBeeMotion.HoneyBurst(CocoonCenter, 1.3f, 11);
                QueenBeeMotion.Shake(CocoonCenter, 4.5f, 9);
                if (!VaultUtils.isServer) {
                    PRTLoader.NewParticle<PRT_DWave>(CocoonCenter, Vector2.Zero, QueenBeeMotion.HoneyGold, 0.2f)?
                        .Configure(new Vector2(1.3f, 0.7f), npc.velocity.ToRotation() + MathHelper.PiOver2, 0.95f, 13);
                }
                SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.75f, Pitch = 0.1f + passIdx * 0.12f, MaxInstances = 3 }, CocoonCenter);
            }
        }

        /// <summary>爆散前静默：茧收缩、万籁俱寂——尖叫前的吸气</summary>
        private void UpdatePreSilence(QueenBeeStateContext context, NPC npc) {
            float t = (Timer - PassEnd) / (float)(DetonateTick - PassEnd);
            DeclareCocoon(context, MathHelper.Lerp(0.38f, 0.29f, t));

            QueenBeeMotion.BrakeHard(npc, 0.8f);
            FaceTarget(npc, CocoonCenter);
        }

        /// <summary>蜂茧爆散(终结拍)+女王恢复：蜂群化作放射刺幕，猎物抛落</summary>
        private void UpdateDetonateAndRecover(QueenBeeStateContext context, NPC npc, Player victim) {
            if (Timer == DetonateTick) {
                //爆散：径向蜂刺+重锤演出(此拍起编队蜂恢复接触伤)
                context.Swarm.LaunchRadial(0, SwarmDirector.MaxBees - 1, CocoonCenter, 16f);
                QueenBeeMotion.HoneyBurst(CocoonCenter, 2.4f, 26);
                QueenBeeMotion.Shake(CocoonCenter, 8f, 16);
                SoundEngine.PlaySound(SoundID.Item74 with { Volume = 1f, Pitch = -0.35f }, CocoonCenter);
                SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.9f, Pitch = -0.55f }, CocoonCenter);
                if (!VaultUtils.isServer) {
                    PRTLoader.NewParticle<PRT_DWave>(CocoonCenter, Vector2.Zero, QueenBeeMotion.HoneyGold, 0.34f)?
                        .Configure(Vector2.One, 0f, 1.6f, 20);
                    PRTLoader.NewParticle<PRT_DWave>(CocoonCenter, Vector2.Zero, QueenBeeMotion.AmberDeep * 0.8f, 0.22f)?
                        .Configure(Vector2.One, 0f, 1.1f, 15);
                }
            }

            //恢复拍：女王力竭缓沉，蜂群散场后自会归位
            npc.velocity *= 0.94f;
            npc.velocity.Y += 0.045f;
            if (victim.Alives()) {
                FaceTarget(npc, victim.Center);
            }
            if (!VaultUtils.isServer && Timer % 6 == 0) {
                PRTLoader.NewParticle<PRT_HoneyDrop>(npc.Center + Main.rand.NextVector2Circular(20f, 14f),
                    Vector2.UnitY * Main.rand.NextFloat(0.5f, 1.4f), QueenBeeMotion.AmberDeep,
                    Main.rand.NextFloat(0.5f, 0.9f));
            }
        }
        #endregion

        #region 幕外 中断
        /// <summary>断投：猎物死亡/逃逸/掉线，蜂群温和释放归位，女王短刹后回连接段</summary>
        private void UpdateAbort(QueenBeeStateContext context, NPC npc) {
            //Counter记录进入中断的帧，20帧后出口(见OnUpdate)
            if (Counter == 0) {
                Counter = Timer;
                QueenBeeMotion.WingHum(npc.Center, 0.4f, -0.5f);
            }
            //不再声明编队：蜂群经FrameReset自然回落光环
            npc.velocity *= 0.9f;
        }
        #endregion

        #region 工具
        /// <summary>茧壳编队：双环蜂盾紧缚茧心</summary>
        private void DeclareCocoon(QueenBeeStateContext context, float scale) {
            context.Swarm.Declare(SwarmFormation.Shield, CocoonCenter, Vector2.UnitX, scale);
            context.Swarm.PushRibbon(0.85f);
        }

        /// <summary>
        /// 茧心路径：锚点→easeOut垂直抬升→顶点悬停微沉浮+命中拍受击颤动<br/>
        /// 纯函数(同步锚点+本地Timer)，全端确定性一致
        /// </summary>
        private Vector2 ComputeCocoonCenter(Vector2 anchor) {
            if (Timer <= SeizeEnd) {
                return anchor;
            }

            float riseT = MathHelper.Clamp((Timer - SeizeEnd) / (float)(LiftEnd - SeizeEnd), 0f, 1f);
            //easeOutCubic：出手快收尾缓
            float ease = 1f - (float)Math.Pow(1f - riseT, 3f);
            Vector2 pos = anchor - Vector2.UnitY * liftHeight * ease;

            if (Timer > LiftEnd) {
                //顶点微沉浮
                pos.Y += (float)Math.Sin((Timer - LiftEnd) * 0.07f) * 6f;
                //命中拍颤动：拍后指数衰减的下坠冲击
                foreach (int beat in HurtBeats) {
                    int since = Timer - beat;
                    if (since >= 0 && since < 20) {
                        pos.Y += 13f * (float)Math.Exp(-since * 0.22f);
                    }
                }
            }
            return pos;
        }

        /// <summary>探测锚点上方净空，决定抬升高度(压低避免把茧埋进天花板)</summary>
        private static float ProbeLiftHeight(Vector2 anchor) {
            float clear = LiftHeightMax;
            for (float h = 60f; h <= LiftHeightMax + 70f; h += 16f) {
                Vector2 probe = anchor - Vector2.UnitY * h;
                if (Collision.SolidCollision(probe - new Vector2(12f, 12f), 24, 24)) {
                    clear = h - 90f;
                    break;
                }
            }
            return MathHelper.Clamp(clear, 140f, LiftHeightMax);
        }
        #endregion

        public override void OnExit(QueenBeeStateContext context) {
            base.OnExit(context);
            NPC npc = context.Npc;
            DisableContactDamage(npc);

            //跨端暴露量灭灯，GrabPlayer据此立即释放
            HoldActive = false;
            Seized = false;
            BeesHarmless = false;

            if (!VaultUtils.isClient) {
                //冷却：命中全额、空挥半额；标记簿清空
                bool whiffed = (int)npc.ai[3] == 2;
                context.OverrideAi[BrutalQueenBeeAI.AiSlotGrabCooldown] =
                    whiffed ? BrutalQueenBeeAI.GrabCooldownTicks / 2 : BrutalQueenBeeAI.GrabCooldownTicks;
                if (npc.TryGetOverride(out BrutalQueenBeeAI queenAI)) {
                    queenAI.ClearMark();
                }
                else {
                    context.OverrideAi[BrutalQueenBeeAI.AiSlotMarkTarget] = 0f;
                    context.OverrideAi[BrutalQueenBeeAI.AiSlotMarkProgress] = 0f;
                }
                //状态内掷骰槽复位，避免脏值漂给下个状态
                npc.ai[0] = 0f;
                npc.ai[1] = 0f;
                npc.ai[3] = 0f;
                npc.netUpdate = true;
            }
        }
    }
}
