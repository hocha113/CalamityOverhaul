using CalamityMod;
using CalamityMod.Buffs.DamageOverTime;
using CalamityMod.Projectiles;
using CalamityOverhaul.Content.Particles;
using CalamityOverhaul.Content.Particles.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal class PlanetaryArrow : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.ignoreWater = true;
            Projectile.arrow = true;
            Projectile.penetrate = 1;
            Projectile.MaxUpdates = 6;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.Calamity().pointBlankShotDuration = CalamityGlobalProjectile.DefaultPointBlankDuration;
        }

        public override void AI() {
            Lighting.AddLight(Projectile.Center, Main.DiscoColor.ToVector3());
            for (int i = 0; i < 3; i++) {
                BaseParticle particle = new PRK_Light(Projectile.Center + (Projectile.velocity.UnitVector() * Main.rand.Next(6))
                    , Vector2.Zero, 0.2f, Main.DiscoColor, 12);
                DRKLoader.AddParticle(particle);
            }
            CalamityUtils.HomeInOnNPC(Projectile, !Projectile.tileCollide, 200f, 12f, 20f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<ElementalMix>(), 60);
        }

        public override void OnKill(int timeLeft) {
            Vector2 dustRotation = (Projectile.rotation - 1.57079637f).ToRotationVector2();
            Vector2 dustVel = dustRotation * Projectile.velocity.Length() * Projectile.MaxUpdates;
            Projectile.Explode(60, SoundID.Item14);
            int inc;
            for (int i = 0; i < 20; i = inc + 1) {
                BaseParticle particle = new PRK_HeavenStar(Projectile.Center, dustVel * Main.rand.NextFloat()
                    , Main.DiscoColor, Color.Azure, Main.rand.NextFloat() * MathHelper.TwoPi, new Vector2(0.2f, 1), new Vector2(1.45f, 2.1f), 22);
                DRKLoader.AddParticle(particle);
                inc = i;
            }
            for (int j = 0; j < 10; j = inc + 1) {
                BaseParticle particle = new PRK_Light(Projectile.Center + (Projectile.velocity.UnitVector() * Main.rand.Next(6))
                    , Vector2.Zero, 0.2f, Main.DiscoColor, 12);
                DRKLoader.AddParticle(particle);
                inc = j;
            }
        }
    }
}
