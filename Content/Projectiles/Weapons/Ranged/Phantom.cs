using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal class Phantom : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Ranged + "PhangasmBow";
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 22;
            Projectile.tileCollide = Projectile.ignoreWater = false;
            Projectile.timeLeft = 300;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
        }

        public override void AI() {
            base.AI();
        }

        public override bool PreDraw(ref Color lightColor) {
            return false;
        }
    }
}
