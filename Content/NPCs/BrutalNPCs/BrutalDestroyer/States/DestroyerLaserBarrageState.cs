using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.States
{
    /// <summary>
    /// 激光弹幕状态：体节沿身体依次发光预警，然后波次射击
    /// </summary>
    internal class DestroyerLaserBarrageState : DestroyerStateBase
    {
        public override string StateName => "LaserBarrage";
        public override DestroyerStateIndex StateIndex => DestroyerStateIndex.LaserBarrage;

        private const int ChargeTime = 60;
        private const int FireTime = 240;

        public override IDestroyerState OnUpdate(DestroyerStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            SetMovement(context, player.Center + player.velocity * 30f, 12f, 0.6f);

            int fireRate = context.IsEnraged ? 3 : 5;
            Timer++;

            //阶段1：充能预警
            if (Timer < ChargeTime) {
                context.SetChargeState(2, Timer / (float)ChargeTime);

                if (!VaultUtils.isServer && Timer % 4 == 0 && context.BodySegments.Count > 0) {
                    int warningIndex = (int)(context.ChargeProgress * context.BodySegments.Count);
                    warningIndex = Math.Clamp(warningIndex, 0, context.BodySegments.Count - 1);
                    NPC warningSegment = context.BodySegments[warningIndex];
                    if (warningSegment.active) {
                        for (int i = 0; i < 3; i++) {
                            Dust dust = Dust.NewDustDirect(
                                warningSegment.Center + Main.rand.NextVector2Circular(20, 20),
                                1, 1, DustID.FireworkFountain_Red, 0, 0, 100, default, 1.8f);
                            dust.noGravity = true;
                            dust.velocity = (warningSegment.Center - dust.position).SafeNormalize(Vector2.Zero) * 2f;
                        }
                    }
                }
            }
            //阶段2：波次射击
            else if (Timer < FireTime) {
                context.ResetChargeState();
                if (context.BodySegments.Count > 0 && Timer % fireRate == 0) {
                    int segmentIndex = (int)((Timer - ChargeTime) / fireRate) % context.BodySegments.Count;
                    NPC segment = context.BodySegments[segmentIndex];
                    if (segment.active) {
                        FireLaser(context, segment);
                    }
                }
            }
            //阶段3：结束
            else {
                return new DestroyerPatrolState();
            }

            return null;
        }

        private static void FireLaser(DestroyerStateContext context, NPC source) {
            if (VaultUtils.isClient) return;

            float speed = CWRWorld.Death ? 6f : 4f;
            Vector2 velocity = (context.Target.Center - source.Center).SafeNormalize(Vector2.Zero) * speed;
            int damage = HeadPrimeAI.SetMultiplier(CWRRef.GetProjectileDamage(context.Npc, ProjectileID.DeathLaser));
            Projectile.NewProjectile(source.GetSource_FromAI(), source.Center, velocity,
                ProjectileID.DeathLaser, damage, 0f, Main.myPlayer, ai2: context.Npc.target);
        }
    }
}
