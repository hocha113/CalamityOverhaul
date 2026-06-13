using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core;
using CalamityOverhaul.Content.Projectiles.Boss.SkeletronPrime;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.States
{
    /// <summary>火力阵：升高位+四臂炮台阵，波浪寻热导弹，滚动缺口</summary>
    [InnoVault.StateMachines.VaultState((int)PrimeStateIndex.BarrageCommand, typeof(PrimeStateContext))]
    internal class PrimeBarrageCommandState : PrimeStateBase
    {
        public override string StateName => "BarrageCommand";
        public override PrimeStateIndex StateIndex => PrimeStateIndex.BarrageCommand;

        internal static int Duration => 150;
        internal static int GapPeriod => 11;

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
            float warmup = MathHelper.Clamp(MathHelper.Lerp(PrimeDirector.ProjectileWarmupStart, 1f, timer / 60f), 0f, 1f);

            //从四联炮台阵位（与 PrimeArm.ApplyBarrageFormation 对齐）向上扇形抛射，
            //导弹自身完成滞空→错相点火→俯冲微追踪
            Vector2 muzzle = npc.Center + new Vector2((armSlot - 1.5f) * 70f, 90f);
            Vector2 vel = (-Vector2.UnitY).RotatedBy((armSlot - 1.5f) * 0.34f + Main.rand.NextFloat(-0.07f, 0.07f))
                * 3.8f * warmup;

            Projectile.NewProjectile(npc.GetSource_FromAI(), muzzle, vel,
                ModContent.ProjectileType<PrimeSeekerMissile>(), damage, 0f,
                Main.myPlayer, npc.target, timer + armSlot * 5);
        }
    }
}
