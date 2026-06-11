using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core;
using CalamityOverhaul.Content.Projectiles.Boss.SkeletronPrime;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.States
{
    /// <summary>
    /// 机械风暴：头部升至高空收拢四肢，召唤 <see cref="SetPosingStarm"/> 毁灭者协奏领域，
    /// 双子魔眼环绕领域火力压制。领域消亡时会把头部传送至领域中心并写入
    /// 传送恢复计时（<see cref="SetPosingStarm.OnKill"/>），随后由主控制器切入 <see cref="PrimeTeleportRecoverState"/>。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)PrimeStateIndex.MechStorm, typeof(PrimeStateContext))]
    internal class PrimeMechStormState : PrimeStateBase
    {
        public override string StateName => "MechStorm";
        public override PrimeStateIndex StateIndex => PrimeStateIndex.MechStorm;

        private const int SummonTick = 30;

        public override IPrimeState OnUpdate(PrimeStateContext context) {
            NPC npc = context.Npc;
            npc.damage = 0;
            context.FrameMode = 0;

            //高空压阵，四肢由机械臂逻辑自动收拢为环绕编队
            npc.ChasingBehavior(context.Target.Center + new Vector2(0, -300), 20);
            LeanByVelocity(npc);

            if (Timer == SummonTick && !VaultUtils.isClient
                && context.StormCount == 0
                && CWRUtils.FindNPCFromeType(NPCID.TheDestroyer) == null) {
                SoundEngine.PlaySound(SoundID.Roar, npc.Center);
                int damage = ScaleDamage(npc.defDamage / 3);
                Projectile.NewProjectile(npc.GetSource_FromAI(), context.Target.Center, Vector2.Zero,
                    ModContent.ProjectileType<SetPosingStarm>(), damage, 2, -1, 0, npc.whoAmI);
                npc.netUpdate = true;
            }

            Timer++;

            //领域结束（或召唤失败）：返回指挥悬停。
            //若领域正常消亡，SetPosingStarm.OnKill 会写入传送计时，主控制器优先切入传送恢复
            if (Timer > SummonTick + 30 && context.StormCount == 0 && !VaultUtils.isClient) {
                return new PrimeCommandHoverState();
            }
            return null;
        }
    }
}
