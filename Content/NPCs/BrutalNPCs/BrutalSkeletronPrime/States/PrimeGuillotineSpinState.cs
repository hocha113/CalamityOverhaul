using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core;
using CalamityOverhaul.Content.Projectiles.Boss.SkeletronPrime;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.States
{
    /// <summary>
    /// 断头台旋杀：以玩家为圆心大半径快速圆周，沿途悬停锯刃，分段收紧逼玩家破圈。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)PrimeStateIndex.GuillotineSpin, typeof(PrimeStateContext))]
    internal class PrimeGuillotineSpinState : PrimeStateBase
    {
        public override string StateName => "GuillotineSpin";
        public override PrimeStateIndex StateIndex => PrimeStateIndex.GuillotineSpin;

        internal static int Telegraph => 36;
        internal static int SpinFrames => 200;
        /// <summary>圆周起始半径</summary>
        internal static float OrbitRadiusStart => 420f;
        /// <summary>圆周收紧终点半径</summary>
        internal static float OrbitRadiusEnd => 180f;

        private float orbitAngle;
        private float orbitRadius;

        public override void OnEnter(PrimeStateContext context) {
            base.OnEnter(context);
            orbitAngle = Main.rand.NextFloat(MathHelper.TwoPi);
            orbitRadius = OrbitRadiusStart;
        }

        public override IPrimeState OnUpdate(PrimeStateContext context) {
            NPC npc = context.Npc;
            context.FrameMode = 2;

            if (Timer < Telegraph) {
                npc.damage = 0;
                context.SetChargeState(1, Timer / (float)Telegraph);
                if (!VaultUtils.isClient && Timer == 1) {
                    PrimeTelegraphLine.SpawnRing(context.Npc, context.Target.Center, OrbitRadiusStart, Telegraph);
                }
                return null;
            }

            orbitRadius = MathHelper.Lerp(OrbitRadiusStart, OrbitRadiusEnd, (Timer - Telegraph) / (float)SpinFrames);
            orbitAngle += Main.masterMode ? 0.09f : 0.07f;
            Vector2 targetPos = context.Target.Center + orbitAngle.ToRotationVector2() * orbitRadius;
            npc.velocity = (targetPos - npc.Center) * 0.22f;
            float speed = npc.velocity.Length();
            npc.damage = speed > PrimeDirector.DashContactSpeedThreshold ? npc.defDamage * 2 : 0;
            SpinRotation(npc, 0.38f);

            if (!VaultUtils.isClient && Timer % 22 == 0) {
                SpawnSawBlade(context, orbitRadius);
            }

            if (!VaultUtils.isServer && Timer == Telegraph + 4) {
                SoundEngine.PlaySound(SoundID.Item22 with { Volume = 0.9f, Pitch = -0.1f }, npc.Center);
            }

            Timer++;
            if (Timer >= Telegraph + SpinFrames && !VaultUtils.isClient) {
                return new PrimeRageConnectorState();
            }
            return null;
        }

        private static void SpawnSawBlade(PrimeStateContext context, float radius) {
            NPC npc = context.Npc;
            int damage = ScaleDamage(CWRRef.GetProjectileDamage(npc, ProjectileID.RocketSkeleton));
            Vector2 pos = context.Target.Center + Main.rand.NextVector2CircularEdge(radius, radius);
            Vector2 vel = (context.Target.Center - pos).SafeNormalize(Vector2.UnitY) * 6f;
            Projectile.NewProjectile(npc.GetSource_FromAI(), pos, vel,
                ModContent.ProjectileType<DeadLaser>(), damage, 0f, Main.myPlayer, 1f, 0f);
        }
    }
}
