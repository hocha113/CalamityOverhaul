using CalamityOverhaul.Content.DamageModify;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeavenfallLongbowProj
{
    internal class InfiniteRune : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 63;
        }

        public override void SetDefaults() {
            Projectile.width = 44;
            Projectile.height = 44;
            Projectile.DamageType = EndlessDamageClass.Instance;
            Projectile.tileCollide = false;
            Projectile.alpha = 255;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
        }

        public override void AI() {
            Projectile.Explode(1600, spanSound: false);
            Projectile.Kill();
        }

        public override bool PreDraw(ref Color lightColor) {
            return false;
        }
    }
}
