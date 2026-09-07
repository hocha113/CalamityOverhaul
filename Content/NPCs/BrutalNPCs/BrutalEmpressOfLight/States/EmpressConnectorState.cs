using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Core;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.States
{
    /// <summary>
    /// 衔接拍：贴身追击（连接段本身有压迫）+ 刹车 + 攻击选择（手写循环表，冲刺当连接符、重招隔开）。
    /// 阶段门槛与终章入口都在这里裁决，攻击态永不自选后继
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)EmpressStateIndex.Connector, typeof(EmpressStateContext))]
    internal class EmpressConnectorState : EmpressStateBase
    {
        public override string StateName => "EmpressConnector";
        public override EmpressStateIndex StateIndex => EmpressStateIndex.Connector;

        /// <summary>一阶段：光球螺旋, 冲刺, 日舞, 冲刺, 万华镜, 光痕, 冲刺, 长枪墙, 冲刺, 蝶群</summary>
        private static readonly EmpressStateIndex[] Phase1Cycle = [
            EmpressStateIndex.LightSpiral,
            EmpressStateIndex.DashGrab,
            EmpressStateIndex.SunDance,
            EmpressStateIndex.DashGrab,
            EmpressStateIndex.Kaleidoscope,
            EmpressStateIndex.Echo,
            EmpressStateIndex.DashGrab,
            EmpressStateIndex.LanceWall,
            EmpressStateIndex.DashGrab,
            EmpressStateIndex.Lacewing,
        ];

        /// <summary>二阶段：长枪墙, 光球螺旋, 冲刺, 万华镜, 月影, 日舞, 冲刺, 光痕, 蝶群, 冲刺, 长枪墙, 月影（夜里月影退成蝶群）</summary>
        private static readonly EmpressStateIndex[] Phase2Cycle = [
            EmpressStateIndex.LanceWall,
            EmpressStateIndex.LightSpiral,
            EmpressStateIndex.DashGrab,
            EmpressStateIndex.Kaleidoscope,
            EmpressStateIndex.MoonShadow,
            EmpressStateIndex.SunDance,
            EmpressStateIndex.DashGrab,
            EmpressStateIndex.Echo,
            EmpressStateIndex.Lacewing,
            EmpressStateIndex.DashGrab,
            EmpressStateIndex.LanceWall,
            EmpressStateIndex.MoonShadow,
        ];

        /// <summary>二阶段门槛</summary>
        internal const float Phase2LifeFraction = 0.6f;
        /// <summary>三阶段（终章）门槛</summary>
        internal const float Phase3LifeFraction = 0.15f;

        private int ChaseFrames => Context.IsSecondPhase ? 20 : 15;
        private int Duration => ChaseFrames + Context.Scaled(14);
        private EmpressStateContext Context;

        public override void OnEnter(EmpressStateContext context) {
            base.OnEnter(context);
            Context = context;
        }

        public override IEmpressState OnUpdate(EmpressStateContext context) {
            Context = context;
            NPC npc = context.Npc;
            Player target = context.Target;
            Timer++;

            context.Pose = EmpressPose.Idle;
            context.PoseTimer = 0f;

            if (target.Alives()) {
                if (Timer <= ChaseFrames) {
                    //贴身追击：追不是飘
                    EmpressMotion.DashTo(npc, target.Center, target.velocity, Timer, context.DayEmpowered || context.IsSecondPhase);
                }
                else {
                    npc.velocity *= 0.9f;
                }
            }
            else {
                npc.velocity *= 0.92f;
            }

            EmpressMotion.AmbientGlow(npc, context.DayFormBlend);

            if (Timer < Duration) {
                return null;
            }

            //只在第一拍起手：连接段是圆舞的换气，招在拍上落
            if (!context.Downbeat) {
                return null;
            }

            //客户端不选招：等ai[2]同步跟随，防计数器在本地空转
            if (VaultUtils.isClient) {
                return null;
            }
            return PickNext(context, npc, target);
        }

        /// <summary>攻击选择：离场/阶段门槛特判优先，其余走循环表</summary>
        private IEmpressState PickNext(EmpressStateContext context, NPC npc, Player target) {
            if (!target.Alives() || npc.Distance(target.Center) > 6400f) {
                return new EmpressDespawnState();
            }

            if (!context.IsSecondPhase && npc.life <= npc.lifeMax * Phase2LifeFraction) {
                return new EmpressPhaseTransitionState();
            }

            if (context.IsSecondPhase && !context.IsThirdPhase && npc.life <= npc.lifeMax * Phase3LifeFraction) {
                return new EmpressPhase3TransformState();
            }

            if (context.IsThirdPhase) {
                return new EmpressFinaleState();
            }

            EmpressStateIndex[] cycle = context.IsSecondPhase ? Phase2Cycle : Phase1Cycle;
            EmpressStateIndex pick = cycle[context.AttackCounter % cycle.Length];
            context.AttackCounter++;

            //月影只在昼；夜里这一格退成蝶群
            if (pick == EmpressStateIndex.MoonShadow && !context.DayEmpowered) {
                pick = EmpressStateIndex.Lacewing;
            }

            //起手侧滑：非静场攻击前一记横向摆动（原版规约），静场招（日舞/月影/光痕）不侧滑
            if (pick != EmpressStateIndex.SunDance && pick != EmpressStateIndex.MoonShadow && pick != EmpressStateIndex.Echo && target.Alives()) {
                int side = target.Center.X > npc.Center.X ? 1 : -1;
                npc.velocity = npc.DirectionFrom(target.Center).SafeNormalize(Vector2.Zero)
                    .RotatedBy(MathHelper.PiOver2 * side) * 19f;
            }

            return CreateState(pick);
        }

        internal static IEmpressState CreateState(EmpressStateIndex index) {
            return index switch {
                EmpressStateIndex.LightSpiral => new EmpressLightSpiralState(),
                EmpressStateIndex.DashGrab => new EmpressDashGrabState(),
                EmpressStateIndex.SunDance => new EmpressSunDanceState(),
                EmpressStateIndex.LanceWall => new EmpressLanceWallState(),
                EmpressStateIndex.Kaleidoscope => new EmpressKaleidoscopeState(),
                EmpressStateIndex.Echo => new EmpressEchoState(),
                EmpressStateIndex.Lacewing => new EmpressLacewingState(),
                EmpressStateIndex.MoonShadow => new EmpressMoonShadowState(),
                _ => new EmpressConnectorState(),
            };
        }
    }
}
