using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Summon
{
    internal class Ieaf : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile + "Ieaf";

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = true;
            Projectile.penetrate = 3;
            Projectile.usesIDStaticNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
        }

        public override void AI() {
            base.AI();
        }

        public override bool? CanHitNPC(NPC target) {
            return base.CanHitNPC(target);
        }

        public override bool PreDraw(ref Color lightColor) {
            return false;
        }
    }
}
