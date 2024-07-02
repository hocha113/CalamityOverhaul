using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal class ArcNovaDiffuserOnSpan : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        public Player Owner => Main.player[Projectile.owner];
        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.alpha = 255;
            Projectile.extraUpdates = 1;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 40;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.light = 1.2f;
        }

        public override void AI() {
            BaseOnSpanProj.FlowerAI(Projectile);
        }
    }
}
