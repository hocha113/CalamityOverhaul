using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.States
{
    /// <summary>
    /// 脱战离场：目标已死或脱离战场，机体悬停回能后闪光离场。
    /// 机械臂检测到该状态会自行收拢倒计时退场。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)PrimeStateIndex.Despawn, typeof(PrimeStateContext))]
    internal class PrimeDespawnState : PrimeStateBase
    {
        public override string StateName => "Despawn";
        public override PrimeStateIndex StateIndex => PrimeStateIndex.Despawn;

        private const int DespawnTime = 60;

        public override void OnEnter(PrimeStateContext context) {
            base.OnEnter(context);

            //被金币枪羞辱后跑路的不甘嘲讽
            if (context.DespawnFromCoinFury && !VaultUtils.isServer) {
                for (int i = 0; i < 5; i++) {
                    VaultUtils.Text(HeadPrimeAI.SkeletronPrime_Text.Value, Color.Red);
                }
            }
            context.DespawnFromCoinFury = false;
        }

        public override IPrimeState OnUpdate(PrimeStateContext context) {
            NPC npc = context.Npc;
            npc.damage = 0;
            npc.dontTakeDamage = true;
            npc.velocity = Vector2.Zero;
            context.FrameMode = 0;

            //回能充填——离场前的能量回收演出
            int addNum = (npc.lifeMax - npc.life) / DespawnTime;
            npc.life = System.Math.Min(npc.life + addNum, npc.lifeMax);

            Timer++;
            if (Timer >= DespawnTime) {
                if (!VaultUtils.isServer) {
                    context.Owner.SpawnHouengEffect();
                }
                if (!VaultUtils.isClient) {
                    npc.active = false;
                    npc.netUpdate = true;
                }
            }
            return null;
        }
    }
}
