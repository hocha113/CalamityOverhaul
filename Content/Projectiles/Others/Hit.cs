using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Others
{
    internal class Hit : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 2;
        }
        public override bool ShouldUpdatePosition() => false;
        public override bool? CanDamage() => false;
        public override bool PreDraw(ref Color lightColor) => false;
    }
}
