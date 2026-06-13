using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core;
using CalamityOverhaul.Content.Projectiles.Boss.SkeletronPrime;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.States
{
    /// <summary>火箭帷幕：两侧火箭墙向中线折叠，缺口滚动</summary>
    [InnoVault.StateMachines.VaultState((int)PrimeStateIndex.RocketCurtain, typeof(PrimeStateContext))]
    internal class PrimeRocketCurtainState : PrimeStateBase
    {
        public override string StateName => "RocketCurtain";
        public override PrimeStateIndex StateIndex => PrimeStateIndex.RocketCurtain;

        internal static int Telegraph => 36;
        internal static int Duration => 150;
        internal static int Spacing => 180;
        internal static int GapRoll => 9;

        private bool wallsSpawned;

        public override IPrimeState OnUpdate(PrimeStateContext context) {
            NPC npc = context.Npc;
            npc.damage = 0;
            context.FrameMode = 2;

            Vector2 anchor = context.Target.Center + new Vector2(0, -320);
            npc.velocity = Vector2.Lerp(npc.velocity, (anchor - npc.Center) * 0.04f, 0.18f);
            LeanByVelocity(npc);

            if (Timer < Telegraph) {
                context.SetChargeState(3, Timer / (float)Telegraph);
                //两面火箭墙的起飞走廊预警（自下方两翼向中线折叠）
                if (!VaultUtils.isClient && Timer == 1) {
                    Vector2 velL = new(4.5f, -5.5f);
                    Vector2 velR = new(-4.5f, -5.5f);
                    PrimeTelegraphLine.SpawnLine(npc, context.Target.Center + new Vector2(-360f, 400f),
                        velL.ToRotation(), Telegraph);
                    PrimeTelegraphLine.SpawnLine(npc, context.Target.Center + new Vector2(360f, 400f),
                        velR.ToRotation(), Telegraph);
                }
            }
            else if (!wallsSpawned && !VaultUtils.isClient) {
                SpawnWalls(context);
                wallsSpawned = true;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item36 with { Volume = 0.9f }, npc.Center);
                }
            }

            Timer++;
            if (Timer >= Duration && !VaultUtils.isClient) {
                return new PrimeRageConnectorState();
            }
            return null;
        }

        private static void SpawnWalls(PrimeStateContext context) {
            NPC npc = context.Npc;
            Player target = context.Target;
            int damage = ScaleDamage(CWRRef.GetProjectileDamage(npc, ProjectileID.RocketSkeleton));
            float warmup = PrimeDirector.ProjectileWarmupStart;

            for (int i = 0; i < 28; i++) {
                if (i % GapRoll == 0) {
                    continue;
                }
                Vector2 left = target.Center + new Vector2(-Spacing * 2 + i * Spacing * 0.14f, 400f);
                Vector2 right = target.Center + new Vector2(Spacing * 2 - i * Spacing * 0.14f, 400f);
                Vector2 velL = new Vector2(4.5f, -5.5f) * warmup;
                Vector2 velR = new Vector2(-4.5f, -5.5f) * warmup;
                Projectile.NewProjectile(npc.GetSource_FromAI(), left, velL,
                    ModContent.ProjectileType<PrimeCannonOnSpan>(), damage, 0f, Main.myPlayer, -1, -1, 0f);
                Projectile.NewProjectile(npc.GetSource_FromAI(), right, velR,
                    ModContent.ProjectileType<PrimeCannonOnSpan>(), damage, 0f, Main.myPlayer, -1, -1, 0f);
            }
        }
    }
}
