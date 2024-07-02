using CalamityOverhaul.Content.Projectiles.Weapons.Ranged;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee
{
    internal class LaserFountains : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        public override void SetDefaults() {
            Projectile.width = 4;
            Projectile.height = 4;
            Projectile.friendly = true;
            Projectile.alpha = 255;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 60;
            Projectile.DamageType = DamageClass.Melee;
        }

        public ref float Time => ref Projectile.ai[0];
        public ref float Fower => ref Projectile.ai[1];

        public override bool? CanDamage() => false;

        public override void AI() {
            int types = ModContent.ProjectileType<DeathLaser>();
            if (Time > 0 && Time % 12 == 0 && Main.player[Projectile.owner].ownedProjectileCounts[types] <= 13) {
                SoundEngine.PlaySound(in SoundID.Item12, Projectile.position);
                Vector2 vr = CWRUtils.GetRandomVevtor(0, 360, Main.rand.Next(760, 920));
                int proj = Projectile.NewProjectile(Projectile.parent(), Projectile.Center + vr,
                    vr.UnitVector() * -1, types, Projectile.damage / 2, 0, Projectile.owner, 1);
                Main.projectile[proj].DamageType = DamageClass.Melee;
                Main.projectile[proj].localAI[1] = 1500;
            }
            Time++;
        }
    }
}
