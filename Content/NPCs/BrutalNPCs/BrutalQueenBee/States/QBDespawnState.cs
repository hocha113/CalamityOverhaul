using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.Core;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.States
{
    /// <summary>
    /// 撤离：唤回蜂群逐只吸收入体，随后加速横向离场<br/>
    /// 无有效目标时进入
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)QueenBeeStateIndex.Despawn, typeof(QueenBeeStateContext))]
    internal class QBDespawnState : QueenBeeStateBase
    {
        public override string StateName => "Despawn";
        public override QueenBeeStateIndex StateIndex => QueenBeeStateIndex.Despawn;

        private const int AbsorbTime = 110;

        public override void OnEnter(QueenBeeStateContext context) {
            base.OnEnter(context);
            QueenBeeMotion.WingHum(context.Npc.Center, 0.5f, -0.6f);
        }

        public override IQueenBeeState OnUpdate(QueenBeeStateContext context) {
            NPC npc = context.Npc;
            npc.dontTakeDamage = true;

            Timer++;

            //吸收拍：原地缓浮，亲卫归体
            if (Timer <= AbsorbTime) {
                npc.velocity *= 0.94f;
                npc.velocity.Y -= 0.05f;
                context.Swarm.Declare(SwarmFormation.Absorb, npc.Center, Vector2.UnitX);
                context.Swarm.PushSnap(1.8f);

                //贴身蜂吸收：服务端消实体，各端播蜜雾
                foreach (var bee in context.Swarm.Bees) {
                    if (!bee.active || bee.Distance(npc.Center) > 52f) {
                        continue;
                    }
                    if (!VaultUtils.isServer) {
                        QueenBeeMotion.HoneyBurst(bee.Center, 0.4f, 2, false);
                    }
                    if (!VaultUtils.isClient) {
                        bee.life = 0;
                        bee.active = false;
                        bee.netUpdate = true;
                    }
                }
                return null;
            }

            //离场拍：横向加速遁走
            int escapeDir = npc.Center.X < Main.maxTilesX * 8f ? -1 : 1;
            npc.velocity.X += escapeDir * 0.42f;
            npc.velocity.Y -= 0.08f;
            FaceByVelocity(npc);
            npc.EncourageDespawn(10);

            //拖长离场没必要，远出屏直接消
            if (Timer > AbsorbTime + 160 && !VaultUtils.isClient) {
                npc.active = false;
                npc.netUpdate = true;
            }
            return null;
        }
    }
}
