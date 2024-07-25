using CalamityMod;
using CalamityMod.Projectiles;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Content.Particles;
using CalamityOverhaul.Content.Particles.Core;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal class TheMaelstromTheArrow : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        public override void SetDefaults() {
            Projectile.width = 44;
            Projectile.height = 44;
            Projectile.friendly = true;
            Projectile.ignoreWater = true;
            Projectile.arrow = true;
            Projectile.penetrate = 1;
            Projectile.Opacity = 0f;
            Projectile.MaxUpdates = 3;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.Calamity().pointBlankShotDuration = CalamityGlobalProjectile.DefaultPointBlankDuration;
        }

        public override void AI() {
            int sparkLifetime = Main.rand.Next(22, 36);
            float sparkScale = Main.rand.NextFloat(1f, 1.3f);
            Color sparkColor = Color.Lerp(Color.Cyan, Color.AliceBlue, Main.rand.NextFloat(0.35f));
            Vector2 sparkVelocity = Projectile.velocity;
            BaseParticle spark = new PRK_Spark(Projectile.Center, sparkVelocity, false, sparkLifetime, sparkScale, sparkColor);
            DRKLoader.AddParticle(spark);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            SoundEngine.PlaySound(SoundID.Item66, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item96, Projectile.Center);
            for (int i = 0; i < 12; i++) {
                Dust blood = Dust.NewDustPerfect(Projectile.Center, 5);
                blood.velocity = Main.rand.NextVector2Circular(6f, 6f);
                blood.scale *= Main.rand.NextFloat(0.7f, 1.3f);
                blood.noGravity = true;
            }

            if (Main.myPlayer != Projectile.owner)
                return;

            if (Main.rand.NextBool(3))
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero, ModContent.ProjectileType<TheMaelstromExplosion>(), Projectile.damage, 0f, Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            return base.PreDraw(ref lightColor);
        }
    }
}
