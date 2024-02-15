using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee
{
    internal class OrderbringerWhiteOrbs : ModProjectile
    {
        public new string LocalizationCategory => "Projectiles.Melee";
        public override string Texture => "CalamityMod/Projectiles/InvisibleProj";

        public override void SetDefaults() {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.friendly = true;
            Projectile.MaxUpdates = 2;
            Projectile.penetrate = -1;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 60;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.usesIDStaticNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.tileCollide = false;
        }

        public override void AI() {
            Lighting.AddLight(Projectile.Center, Main.DiscoColor.ToVector3());
            for (int i = 0; i < 2; i++) {
                int rainbow = Dust.NewDust(new Vector2(Projectile.position.X, Projectile.position.Y), Projectile.width, Projectile.height, DustID.RainbowTorch, 0f, 0f, 100, new Color(Main.DiscoR, Main.DiscoG, Main.DiscoB), 1f);
                Main.dust[rainbow].noGravity = true;
                Main.dust[rainbow].velocity *= 0.5f;
                Main.dust[rainbow].velocity += Projectile.velocity * 0.1f;
            }
        }
    }
}
