using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Projectiles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.States
{
    /// <summary>离子过载</summary>
    [InnoVault.StateMachines.VaultState((int)PrimeStateIndex.IonOverload, typeof(PrimeStateContext))]
    internal class PrimeIonOverloadState : PrimeStateBase
    {
        public override string StateName => "IonOverload";
        public override PrimeStateIndex StateIndex => PrimeStateIndex.IonOverload;

        internal static int ChargeFrames => 72;
        internal static int SilenceFrames => 8;
        internal static int BurstInterval => 42;
        internal static int MaxBursts => 3;
        internal static float GapAngle => 0.55f;

        public override IPrimeState OnUpdate(PrimeStateContext context) {
            NPC npc = context.Npc;
            npc.damage = 0;
            context.FrameMode = 2;

            Vector2 anchor = context.Target.Center + new Vector2(0, -300);
            npc.velocity = Vector2.Lerp(npc.velocity, (anchor - npc.Center) * 0.04f, 0.2f);
            LeanByVelocity(npc);

            if (Timer < ChargeFrames) {
                float p = Timer / (float)ChargeFrames;
                context.SetChargeState(3, p);
                if (!VaultUtils.isServer && Timer % 3 == 0) {
                    int spawn = (int)System.Math.Sqrt(Timer) + 1;
                    for (int i = 0; i < spawn; i++) {
                        Vector2 pos = npc.Center + Main.rand.NextVector2CircularEdge(120f, 120f);
                        Dust dust = Dust.NewDustDirect(pos, 1, 1, DustID.FireworkFountain_Red, 0, 0, 100, Color.Orange, 1.2f);
                        dust.velocity = (npc.Center - pos) * 0.12f;
                        dust.noGravity = true;
                    }
                }
            }
            else if (Timer < ChargeFrames + SilenceFrames) {
                context.ResetChargeState();
            }
            else {
                int burstTimer = Timer - ChargeFrames - SilenceFrames;
                if (burstTimer % BurstInterval == 0 && Counter < MaxBursts && !VaultUtils.isClient) {
                    FireGapRing(context, Counter);
                    Counter++;
                    if (!VaultUtils.isServer && Counter == 1) {
                        PrimeScreenEffects.PushShockRing(npc.Center, 0.85f, 560f);
                        SoundEngine.PlaySound(SoundID.Item62 with { Volume = 1.1f, Pitch = -0.15f }, npc.Center);
                    }
                }
            }

            Timer++;
            if (Counter >= MaxBursts && Timer > ChargeFrames + SilenceFrames + BurstInterval * MaxBursts
                && !VaultUtils.isClient) {
                return new PrimeRageConnectorState();
            }
            return null;
        }

        private static void FireGapRing(PrimeStateContext context, int wave) {
            NPC npc = context.Npc;
            int damage = ScaleDamage(CWRRef.GetProjectileDamage(npc, ProjectileID.RocketSkeleton));
            int count = context.BossRush ? 15 : (context.MasterMode ? 13 : 11);
            float gapCenter = wave * GapAngle + Main.rand.NextFloat(-0.1f, 0.1f);
            float warmup = MathHelper.Lerp(PrimeDirector.ProjectileWarmupStart, 1f, wave / (float)MaxBursts);

            for (int i = 0; i < count; i++) {
                float ang = MathHelper.TwoPi / count * i;
                float rel = MathHelper.WrapAngle(ang - gapCenter);
                if (System.Math.Abs(rel) < GapAngle * 0.5f) {
                    continue;
                }
                Vector2 vel = ang.ToRotationVector2() * 10f * warmup;
                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, vel,
                    ModContent.ProjectileType<PrimeCannonOnSpan>(), damage, 0f,
                    Main.myPlayer, npc.whoAmI, npc.target, ang);
            }
        }
    }
}
