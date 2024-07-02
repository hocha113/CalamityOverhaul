using CalamityMod;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Magic
{
    internal class MagicStar : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Magic + "MagicStar";
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 6;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.friendly = true;
            Projectile.timeLeft = 660;
        }

        public override void AI() {
            Lighting.AddLight(Projectile.Center, Color.Blue.ToVector3());
            Projectile.rotation += Projectile.velocity.X * 0.1f;
            Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.BlueTorch);
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 6; i++) {
                Vector2 vr = CWRUtils.randVr(6);
                Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.BlueTorch, vr.X, vr.Y);
                Main.dust[Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.FireworkFountain_Blue, vr.X, vr.Y)].noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            CalamityUtils.DrawAfterimagesCentered(Projectile, ProjectileID.Sets.TrailingMode[Projectile.type], Color.White, 1);
            return false;
        }
    }
}
