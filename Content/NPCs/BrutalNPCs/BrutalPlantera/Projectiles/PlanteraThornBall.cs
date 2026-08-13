using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.Rendering;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.Projectiles
{
    /// <summary>荆棘滚球：弹跳滚动，三次落地后炸成孢子雾</summary>
    internal class PlanteraThornBall : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_277";

        internal static int GetDamage(NPC boss) => Math.Max((int)(boss.defDamage * 0.38f), 16);

        private ref float BounceCount => ref Projectile.ai[0];

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 360;
            Projectile.aiStyle = -1;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            Projectile.velocity.Y += 0.24f;
            if (Projectile.velocity.Y > 17f) {
                Projectile.velocity.Y = 17f;
            }
            Projectile.rotation += Projectile.velocity.X * 0.045f;

            Lighting.AddLight(Projectile.Center, PlanteraRenderHelper.PetalPink.ToVector3() * 0.2f);

            if (!VaultUtils.isServer && Main.rand.NextBool(7)) {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    DustID.Plantera_Pink, 0f, 0f, 130, default, 0.95f);
                dust.noGravity = true;
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            BounceCount += 1f;
            if (BounceCount >= 4f) {
                Projectile.Kill();
                return false;
            }

            //反弹保留大部分动能
            if (Math.Abs(oldVelocity.X) > 0.1f && Math.Sign(Projectile.velocity.X) != Math.Sign(oldVelocity.X)) {
                Projectile.velocity.X = -oldVelocity.X * 0.9f;
            }
            if (Math.Abs(oldVelocity.Y) > 0.1f && Math.Sign(Projectile.velocity.Y) != Math.Sign(oldVelocity.Y)) {
                Projectile.velocity.Y = -oldVelocity.Y * 0.82f;
            }

            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.7f, Pitch = -0.3f, MaxInstances = 4 }, Projectile.Center);
                for (int i = 0; i < 5; i++) {
                    Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                        DustID.JungleGrass, 0f, 0f, 100, default, 1.1f);
                    dust.velocity = Main.rand.NextVector2Circular(3f, 3f) - oldVelocity * 0.1f;
                }
            }
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (!VaultUtils.isClient) {
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero,
                    ModContent.ProjectileType<PlanteraSporeCloud>(),
                    Math.Max(Projectile.damage / 2, 8), 0f, Main.myPlayer, 0.9f);
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCDeath1 with { Volume = 0.6f, Pitch = 0.2f, MaxInstances = 3 }, Projectile.Center);
                PlanteraRenderHelper.SpawnSporePuff(Projectile.Center, 1.1f);
                PlanteraRenderHelper.SpawnPetalBurst(Projectile.Center, 6, 5f, false);
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.Bleeding, 180);
        }
    }
}
