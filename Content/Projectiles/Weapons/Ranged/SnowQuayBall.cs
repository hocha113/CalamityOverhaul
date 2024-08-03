using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal class SnowQuayBall : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder3;
        public override void SetStaticDefaults() {
            TextureAssets.Projectile[Type] = TextureAssets.Projectile[ProjectileID.SnowBallFriendly];
        }
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.light = 0.2f;
        }

        public override void AI() {
            Projectile.rotation += Projectile.velocity.X * 0.1f;
            if (Main.rand.NextBool()) {
                int index2 = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height
                    , DustID.BlueCrystalShard, Projectile.velocity.X, Projectile.velocity.Y, 0, default, 1.1f);
                Main.dust[index2].noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Frostburn, 180);
        }

        public override void OnKill(int timeLeft) {
            var source = Main.player[Projectile.owner].GetShootState().Source;
            for (int i = 0; i < 3; i++) {
                Projectile.NewProjectileDirect(source, Projectile.Center, new Vector2(Main.rand.NextFloat(-3, 3), -3)
                    , ModContent.ProjectileType<IceExplosionFriend>(), 13, 0, Projectile.owner, 0);
                Projectile proj2 = Projectile.NewProjectileDirect(source, Projectile.Center, new Vector2(Main.rand.NextFloat(-5, 5), -13)
                    , ProjectileID.SnowBallFriendly, 13, 0, Projectile.owner, 0);
                proj2.scale += Main.rand.NextFloat(0.5f);
                proj2.light += 0.5f;
                proj2.penetrate += 2;
                proj2.extraUpdates += 2;
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            return base.OnTileCollide(oldVelocity);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = TextureAssets.Projectile[Type].Value;
            Vector2 drawPos = Projectile.Center;
            Vector2 origPos = value.Size();
            drawPos.X -= Main.screenPosition.X;
            drawPos.Y -= Main.screenPosition.Y;
            origPos.X /= 2;
            origPos.Y /= 2;
            Main.EntitySpriteDraw(value, drawPos, null, Color.White, Projectile.rotation, origPos, Projectile.scale, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(value, drawPos, null, Color.White, Projectile.rotation, origPos, Projectile.scale + 0.1f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(value, drawPos, null, Color.White, Projectile.rotation, origPos, Projectile.scale + 0.2f, SpriteEffects.None, 0);
            return false;
        }
    }
}
