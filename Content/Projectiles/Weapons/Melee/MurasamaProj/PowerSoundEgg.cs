using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.MurasamaProj
{
    internal class PowerSoundEgg : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        public override void SetStaticDefaults() => CWRLoad.ProjValue.ImmuneFrozen[Type] = true;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 2;
            Projectile.tileCollide = Projectile.ignoreWater = false;
            Projectile.friendly = true;
            Projectile.timeLeft = 600;
            Projectile.CWR().NotSubjectToSpecialEffects = true;
        }

        public override bool? CanDamage() => false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) => false;

        public override void AI() => Main.player[Projectile.owner].CWR().InFoodStallChair = true;
    }
}
