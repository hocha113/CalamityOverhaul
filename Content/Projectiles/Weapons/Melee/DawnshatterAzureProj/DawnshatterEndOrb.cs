using CalamityMod.Projectiles.Rogue;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Particles.Core;
using CalamityOverhaul.Content.Particles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.DawnshatterAzureProj
{
    internal class DawnshatterEndOrb : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 62;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 60;
        }

        public override bool? CanDamage() {
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Projectile.IsOwnedByLocalPlayer()) {
                for (int i = 0; i < 4; i++) {
                    float rot = MathHelper.PiOver2 * i;
                    Vector2 vr = rot.ToRotationVector2() * 10;
                    for (int j = 0; j < 76; j++) {
                        HeavenfallStarParticle spark = new HeavenfallStarParticle(Projectile.Center, vr * (0.3f + j * 0.1f), false, 37, Main.rand.Next(3, 17), Color.Red);
                        CWRParticleHandler.AddParticle(spark);
                    }
                }
                float starSpeed = 30f;
                for (int i = 0; i < 40; i++) {
                    Vector2 shootVelocity = (MathHelper.TwoPi * i / 40f).ToRotationVector2() * starSpeed;
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center + shootVelocity, shootVelocity
                        , ModContent.ProjectileType<TheDaybreak3>(), Projectile.damage, 0f, Projectile.owner, Projectile.Center.X, Projectile.Center.Y);
                }
                int pointsOnStar = 8;
                for (int k = 0; k < 2; k++) {
                    for (int i = 0; i < pointsOnStar; i++) {
                        float angle = MathHelper.Pi * 1.5f - i * MathHelper.TwoPi / pointsOnStar;
                        float nextAngle = MathHelper.Pi * 1.5f - (i + 3) % pointsOnStar * MathHelper.TwoPi / pointsOnStar;
                        if (k == 1)
                            nextAngle = MathHelper.Pi * 1.5f - (i + 2) * MathHelper.TwoPi / pointsOnStar;
                        Vector2 start = angle.ToRotationVector2();
                        Vector2 end = nextAngle.ToRotationVector2();
                        int pointsOnStarSegment = 18;
                        for (int j = 0; j < pointsOnStarSegment; j++) {
                            Vector2 shootVelocity = Vector2.Lerp(start, end, j / (float)pointsOnStarSegment) * starSpeed;
                            Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center + shootVelocity, shootVelocity
                                , ModContent.ProjectileType<TheDaybreak3>(), Projectile.damage, 0f, Projectile.owner, Projectile.Center.X, Projectile.Center.Y);
                        }
                    }
                }
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero, ModContent.ProjectileType<MakeDamage>(), Projectile.damage, 0f, Projectile.owner);
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero
                        , ModContent.ProjectileType<DawnshatterEndOrb2>(), Projectile.damage, 0f, Projectile.owner);
            }
            Projectile.Explode(3000, spanSound: false);
        }
    }
}
