using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.PhosphorescentGauntletProj
{
    internal class GauntletDerive : ModProjectile
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "PhosphorescentGauntlet";
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 3;
        }

        public override void SetDefaults() {
            Projectile.DamageType = DamageClass.Melee;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = false;
            Projectile.timeLeft = 120 * Projectile.MaxUpdates;
            Projectile.width = Projectile.height = 22;
            Projectile.timeLeft = 56;
            Projectile.extraUpdates = 2;
            Projectile.penetrate = 3;
        }

        public override bool? CanDamage() => Projectile.ai[1] > 0 ? false : base.CanDamage();

        public override void AI() {
            if (Projectile.ai[0] == 0) {
                Projectile.ai[1] = Main.rand.Next(13);
            }
            Projectile.rotation = Projectile.velocity.ToRotation();
            Projectile.velocity *= 0.99f;
            Projectile.scale += 0.01f;
            if (Projectile.timeLeft < 30) {
                NPC target = CWRUtils.GetNPCInstance((int)Projectile.ai[2]);
                if (target != null) {
                    Projectile.ChasingBehavior(target.Center, 22);
                }
            }
            Projectile.ai[0]++;
            Projectile.ai[1]--;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = TextureAssets.Projectile[Type].Value;
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, null, Color.White * (Projectile.timeLeft / 15f)
                , Projectile.rotation + MathHelper.PiOver4 + MathHelper.Pi + (Projectile.velocity.X > 0 ? MathHelper.PiOver2 : 0)
                , value.Size() / 2, Projectile.scale, Projectile.velocity.X > 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0);
            return false;
        }
    }
}
