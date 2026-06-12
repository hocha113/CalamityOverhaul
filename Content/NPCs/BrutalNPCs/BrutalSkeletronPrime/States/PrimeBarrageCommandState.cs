using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core;
using CalamityOverhaul.Content.Projectiles.Boss.SkeletronPrime;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.States
{
    /// <summary>
    /// 火力阵：头部升高位，四臂收拢成炮台阵列，波浪齐射带滚动缺口。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)PrimeStateIndex.BarrageCommand, typeof(PrimeStateContext))]
    internal class PrimeBarrageCommandState : PrimeStateBase
    {
        public override string StateName => "BarrageCommand";
        public override PrimeStateIndex StateIndex => PrimeStateIndex.BarrageCommand;

        private const int Duration = 150;
        private const int GapPeriod = 11;

        public override IPrimeState OnUpdate(PrimeStateContext context) {
            NPC npc = context.Npc;
            npc.damage = 0;
            context.FrameMode = 0;

            Vector2 anchor = context.Target.Center + new Vector2(0, -360);
            npc.velocity = Vector2.Lerp(npc.velocity, (anchor - npc.Center) * 0.05f, 0.18f);
            LeanByVelocity(npc);

            int rate = context.MasterMode ? 10 : 12;
            if (!VaultUtils.isClient && Timer > 20 && Timer % rate == 0) {
                int armSlot = (Timer / rate) % 4;
                if ((Timer + armSlot * 3) % GapPeriod != 0) {
                    FireWave(context, armSlot, Timer);
                }
            }

            if (!VaultUtils.isServer && Timer == 8) {
                SoundEngine.PlaySound(SoundID.Item36 with { Volume = 0.8f, Pitch = -0.2f }, npc.Center);
            }

            Timer++;
            if (Timer >= Duration && !VaultUtils.isClient) {
                return new PrimeCommandSequenceState();
            }
            return null;
        }

        private static void FireWave(PrimeStateContext context, int armSlot, int timer) {
            NPC npc = context.Npc;
            int damage = ScaleDamage(CWRRef.GetProjectileDamage(npc, ProjectileID.RocketSkeleton));
            float warmup = MathHelper.Lerp(PrimeDirector.ProjectileWarmupStart, 1f, timer / 60f);
            Vector2 dir = DirectionToTarget(context).RotatedBy((armSlot - 1.5f) * 0.18f);
            float speed = 9f * warmup;

            Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center + dir * 80f, dir * speed,
                ModContent.ProjectileType<PrimeCannonOnSpan>(), damage, 0f,
                Main.myPlayer, npc.whoAmI, npc.target, armSlot * 0.12f);
        }
    }
}
