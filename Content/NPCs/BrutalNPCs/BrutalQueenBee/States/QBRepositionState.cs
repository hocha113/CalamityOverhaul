using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.Rendering;
using InnoVault.PRT;
using System;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.States
{
    /// <summary>
    /// 连接段：女王侧翼小跳位+蜂群重整光环+振翅起手式，末帧服务端按出招环选下一招<br/>
    /// 段落标点：让玩家的眼睛得到"下一招要来了"的换行
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)QueenBeeStateIndex.Reposition, typeof(QueenBeeStateContext))]
    internal class QBRepositionState : QueenBeeStateBase
    {
        public override string StateName => "Reposition";
        public override QueenBeeStateIndex StateIndex => QueenBeeStateIndex.Reposition;

        //一阶段出招环
        private static readonly QueenBeeStateIndex[] CycleP1 = [
            QueenBeeStateIndex.DiveStrafe,
            QueenBeeStateIndex.StingerFan,
            QueenBeeStateIndex.SwarmArrow,
            QueenBeeStateIndex.HoneyMortar,
        ];
        //二阶段出招环：墙/漩涡隔开，蜜炮(补员喘息)垫后
        private static readonly QueenBeeStateIndex[] CycleP2 = [
            QueenBeeStateIndex.SwarmWall,
            QueenBeeStateIndex.DiveStrafe,
            QueenBeeStateIndex.WaxTurret,
            QueenBeeStateIndex.SwarmArrow,
            QueenBeeStateIndex.SwarmVortex,
            QueenBeeStateIndex.StingerFan,
            QueenBeeStateIndex.HoneyMortar,
        ];
        //大招后强压环：喘息招收敛
        private static readonly QueenBeeStateIndex[] CycleP3 = [
            QueenBeeStateIndex.DiveStrafe,
            QueenBeeStateIndex.SwarmWall,
            QueenBeeStateIndex.SwarmArrow,
            QueenBeeStateIndex.SwarmVortex,
            QueenBeeStateIndex.StingerFan,
        ];

        private int Duration(QueenBeeStateContext context) => context.UltimateDone ? 30 : 42;

        public override void OnEnter(QueenBeeStateContext context) {
            base.OnEnter(context);
            if (!VaultUtils.isClient) {
                NPC npc = context.Npc;

                //远距回归阀：被甩太远直接瞬移到目标视野边(镜面毁灭者远距阀)
                if (context.Target.Alives() && npc.Distance(context.Target.Center) > 3200f) {
                    Vector2 dir = (npc.Center - context.Target.Center).SafeNormalize(-Vector2.UnitY);
                    npc.Center = context.Target.Center + dir * 1150f;
                    npc.velocity = -dir * 14f;
                }

                //侧翼小跳位方向：服务端掷骰进ai[0]
                int side = npc.Center.X < context.Target.Center.X ? -1 : 1;
                //三成概率换边，避免死贴一侧
                if (Main.rand.NextBool(3)) {
                    side = -side;
                }
                npc.ai[0] = side;
                npc.netUpdate = true;
            }
        }

        public override IQueenBeeState OnUpdate(QueenBeeStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            int duration = Duration(context);
            int side = npc.ai[0] >= 0f ? 1 : -1;

            Timer++;

            //弹簧滑向侧翼待机位
            Vector2 hoverPos = player.Center + new Vector2(side * 400f, -270f);
            QueenBeeMotion.SpringHover(npc, hoverPos, 0.02f, 0.1f, 30f);
            FaceTarget(npc, player.Center);

            //蜂群重整光环+微光脉动
            context.Swarm.PushRibbon(0.25f);

            //末10帧振翅起手式：轻抬+金闪(下一招的元预告)
            if (Timer == duration - 10) {
                npc.velocity -= Vector2.UnitY * 2.6f;
                QueenBeeMotion.WingHum(npc.Center, 0.45f, 0.1f);
                if (!VaultUtils.isServer) {
                    for (int i = 0; i < 6; i++) {
                        PRTLoader.NewParticle<PRT_BeeGlint>(
                            npc.Center + Main.rand.NextVector2Circular(40f, 30f),
                            -Vector2.UnitY * Main.rand.NextFloat(0.5f, 1.5f),
                            QueenBeeMotion.HoneyGold, Main.rand.NextFloat(0.9f, 1.4f));
                    }
                }
            }

            //补员涓流：每20帧至多2只
            if (!VaultUtils.isClient && Timer % 20 == 0) {
                context.Swarm.ServerTopUp(context.IsPhase2 ? 24 : 16, 2);
            }

            //服务端末帧选招
            if (Timer >= duration) {
                return PickNextAttack(context);
            }
            return null;
        }

        /// <summary>按阶段出招环取下一招(服务端裁定，客户端由ai[2]跟随)</summary>
        private static IQueenBeeState PickNextAttack(QueenBeeStateContext context) {
            if (VaultUtils.isClient) {
                return null;
            }

            QueenBeeStateIndex[] cycle = context.UltimateDone ? CycleP3 : context.IsPhase2 ? CycleP2 : CycleP1;
            int cursor = context.AttackCycleIndex;
            QueenBeeStateIndex next = cycle[Math.Abs(cursor) % cycle.Length];

            //游标推进并同步
            context.OverrideAi[4] = cursor + 1;
            context.Npc.netUpdate = true;

            return CreateState(next);
        }

        /// <summary>状态索引→实例</summary>
        internal static IQueenBeeState CreateState(QueenBeeStateIndex index) {
            return index switch {
                QueenBeeStateIndex.DiveStrafe => new QBDiveStrafeState(),
                QueenBeeStateIndex.StingerFan => new QBStingerFanState(),
                QueenBeeStateIndex.SwarmArrow => new QBSwarmArrowState(),
                QueenBeeStateIndex.HoneyMortar => new QBHoneyMortarState(),
                QueenBeeStateIndex.SwarmWall => new QBSwarmWallState(),
                QueenBeeStateIndex.SwarmVortex => new QBSwarmVortexState(),
                QueenBeeStateIndex.WaxTurret => new QBWaxTurretState(),
                _ => new QBStingerFanState(),
            };
        }
    }
}
