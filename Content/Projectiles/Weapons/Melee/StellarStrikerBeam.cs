using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee
{
    internal class StellarStrikerBeam : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        public ref float Time => ref Projectile.ai[0];
        private bool onhitNPCBool = true;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 22;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = false;
            Projectile.MaxUpdates = 5;
            Projectile.timeLeft = 120 * Projectile.MaxUpdates;
        }

        public override void AI() {
            if (Projectile.ai[2] == 1) {
                Projectile.penetrate = -1;
                Projectile.MaxUpdates = 8;
                Projectile.timeLeft = 120 * Projectile.MaxUpdates;
                Projectile.usesLocalNPCImmunity = true;
                Projectile.localNPCHitCooldown = -1;
            }

            if (Math.Abs(Projectile.velocity.X) + Math.Abs(Projectile.velocity.Y) < 16f) {
                Projectile.velocity *= 1.045f;
            }

            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            Lighting.AddLight(Projectile.Center, Color.AliceBlue.ToVector3() * 0.2f);
            Player Owner = Main.player[Projectile.owner];
            float targetDist = Vector2.Distance(Owner.Center, Projectile.Center);

            if (Projectile.timeLeft % 2 == 0 && Time > 5f && targetDist < 1400f) {
                PRT_SparkAlpha spark = new(Projectile.Center, Projectile.velocity * 0.05f, false, 4, 2.3f, new Color(68, 153, 112));
                PRTLoader.AddParticle(spark);
            }

            if (Projectile.timeLeft % 2 == 0 && Time > 5f && targetDist < 1400f) {
                PRT_Line spark2 = new(Projectile.Center, -Projectile.velocity * 0.05f, false, 6, 1.7f, new Color(95, 200, 157));
                PRTLoader.AddParticle(spark2);
            }

            Time++;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Projectile.ai[2] == 1) {
                Projectile.damage = (int)(Projectile.damage * 0.98f);
                return;
            }
            if (Projectile.numHits == 0 && onhitNPCBool) {
                for (int i = 0; i < 6; i++) {
                    int proj = Projectile.NewProjectile(Projectile.FromObjectGetParent(), Projectile.Center + VaultUtils.RandVr(255), Vector2.Zero, ProjectileID.LunarFlare
                        , (int)(Projectile.damage * 0.5), 0, Main.myPlayer, 0f, Main.rand.Next(3));
                    Main.projectile[proj].DamageType = DamageClass.Melee;
                    Main.projectile[proj].timeLeft = 30;
                }
                onhitNPCBool = false;
            }
        }
    }
}
