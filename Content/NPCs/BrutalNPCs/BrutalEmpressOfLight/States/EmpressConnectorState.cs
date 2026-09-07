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

        /// <summary>一阶段：光球螺旋, 冲刺, 日舞, 冲刺, 瞬现枪, 光球螺旋, 冲刺, 长枪墙, 冲刺, 日舞</summary>
        private static readonly EmpressStateIndex[] Phase1Cycle = [
            EmpressStateIndex.LightSpiral,
            EmpressStateIndex.DashGrab,
            EmpressStateIndex.SunDance,
            EmpressStateIndex.DashGrab,
            EmpressStateIndex.HitscanVolley,
            EmpressStateIndex.LightSpiral,
            EmpressStateIndex.DashGrab,
            EmpressStateIndex.LanceWall,
            EmpressStateIndex.DashGrab,
            EmpressStateIndex.SunDance,
        ];

        /// <summary>二阶段：长枪墙, 光球螺旋, 冲刺, 瞬现枪, 熔光扇, 日舞, 冲刺, 长枪墙, 熔光扇, 冲刺</summary>
        private static readonly EmpressStateIndex[] Phase2Cycle = [
            EmpressStateIndex.LanceWall,
            EmpressStateIndex.LightSpiral,
            EmpressStateIndex.DashGrab,
            EmpressStateIndex.HitscanVolley,
            EmpressStateIndex.MeltingLight,
            EmpressStateIndex.SunDance,
            EmpressStateIndex.DashGrab,
            EmpressStateIndex.LanceWall,
            EmpressStateIndex.MeltingLight,
            EmpressStateIndex.DashGrab,
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

            //起手侧滑：非静场攻击前一记横向摆动（原版规约），静场招（日舞/熔光扇）不侧滑
            if (pick != EmpressStateIndex.SunDance && pick != EmpressStateIndex.MeltingLight && target.Alives()) {
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
                EmpressStateIndex.HitscanVolley => new EmpressHitscanVolleyState(),
                EmpressStateIndex.MeltingLight => new EmpressMeltingLightState(),
                _ => new EmpressConnectorState(),
            };
        }
    }
}
