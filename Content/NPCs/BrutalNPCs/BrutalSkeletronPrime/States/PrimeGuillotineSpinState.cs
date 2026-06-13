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
        internal static float OrbitRadiusStart => 620f;
        /// <summary>圆周收紧终点半径</summary>
        internal static float OrbitRadiusEnd => 380f;

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

            //注意：Timer++ 必须在所有分支的公共路径上执行。
            //此前预警分支提前 return 跳过了自增，Timer 永远为 0，
            //状态卡死在预警段且无运动控制——boss 带着残速无限漂移。
            if (Timer < Telegraph) {
                npc.damage = 0;
                //预警期缓刹，消化进入状态时的惯性残速
                npc.velocity *= 0.9f;
                LeanTowards(npc, context.Target.Center);
                context.SetChargeState(1, Timer / (float)Telegraph);
                if (!VaultUtils.isClient && Timer == 1) {
                    PrimeTelegraphLine.SpawnRing(context.Npc, context.Target.Center, OrbitRadiusStart, Telegraph);
                }
            }
            else {
                orbitRadius = MathHelper.Lerp(OrbitRadiusStart, OrbitRadiusEnd, (Timer - Telegraph) / (float)SpinFrames);
                orbitAngle += Main.masterMode ? 0.09f : 0.07f;
                Vector2 targetPos = context.Target.Center + orbitAngle.ToRotationVector2() * orbitRadius;
                //软切入：前 15 帧趋近系数渐升，避免入轨第一帧产生巨大瞬时速度撞击玩家
                float approach = MathHelper.Clamp((Timer - Telegraph) / 15f, 0.3f, 1f) * 0.22f;
                npc.velocity = (targetPos - npc.Center) * approach;
                float speed = npc.velocity.Length();
                npc.damage = speed > PrimeDirector.DashContactSpeedThreshold ? npc.defDamage * 2 : 0;
                SpinRotation(npc, 0.38f);

                if (!VaultUtils.isClient && Timer % 22 == 0) {
                    SpawnSawBlade(context, orbitRadius);
                }
                if (!VaultUtils.isServer && Timer == Telegraph + 4) {
                    SoundEngine.PlaySound(SoundID.Item22 with { Volume = 0.9f, Pitch = -0.1f }, npc.Center);
                }
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
