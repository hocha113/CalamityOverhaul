using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.PhosphorescentGauntletProj
{
    internal class PGDeriveProj : PGProj
    {
        private int CanDamageTime;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 3;
        }

        public override void SetDefaults() {
            base.SetDefaults();
            Projectile.width = Projectile.height = 22;
            Projectile.timeLeft = 56;
            Projectile.extraUpdates = 2;
            Projectile.penetrate = 3;
            CanDamageTime = Main.rand.Next(13);
        }

        public override bool? CanDamage() {
            return CanDamageTime > 0 ? false : base.CanDamage();
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            Projectile.velocity *= 0.99f;
            Projectile.scale += 0.01f;
            if (Projectile.timeLeft < 30) {
                NPC target = Projectile.Center.FindClosestNPC(900);
                if (target != null) {
                    Projectile.ChasingBehavior(target.Center, 22);
                }
            }
            CanDamageTime--;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {

        }

        public override void OnKill(int timeLeft) {

        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            return base.OnTileCollide(oldVelocity);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = TextureAssets.Projectile[Type].Value;
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, null, Color.White * (Projectile.timeLeft / 15f), Projectile.rotation + MathHelper.PiOver4 + MathHelper.Pi + (Projectile.velocity.X > 0 ? MathHelper.PiOver2 : 0)
                , value.Size() / 2, Projectile.scale, Projectile.velocity.X > 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0);
            return false;
        }
    }
}
