using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal class StakeHmmod : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        private float tileRot;
        private Vector2 tilePos;
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.timeLeft = 300;
            Projectile.extraUpdates = 4;
            Projectile.penetrate = -1;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            if (Projectile.ai[0] > 0) {
                Projectile.rotation = tileRot;
                Projectile.Center = tilePos;
                Player player = CWRUtils.GetPlayerInstance(Projectile.owner);
                if (player != null) {
                    if (player.Distance(Projectile.Center) < Projectile.width) {
                        player.CWR().ReceivingPlatformTime = 2;
                    }
                }
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (Projectile.ai[0] <= 0) {
                tileRot = Projectile.rotation;
                tilePos = Projectile.Center;
                Projectile.extraUpdates = 0;
                Projectile.timeLeft = 300;
                CWRDust.SplashDust(Projectile, 31, DustID.JungleGrass, DustID.JungleGrass, -3, Color.Goldenrod);
                Projectile.ai[0]++;
            }
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            CWRDust.SplashDust(Projectile, 11, DustID.Blood, DustID.Blood, -3, Color.Goldenrod);
        }

        public override void OnKill(int timeLeft) {
            CWRDust.SplashDust(Projectile, 31, DustID.JungleGrass, DustID.JungleGrass, -3, Color.Goldenrod);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D mainValue = TextureAssets.Projectile[ProjectileID.Stake].Value;
            Main.instance.LoadProjectile(ProjectileID.Stake);
            Vector2 off = Projectile.position.To(Projectile.Center);
            Main.EntitySpriteDraw(mainValue, Projectile.Center - Main.screenPosition, null, lightColor
            , Projectile.rotation + MathHelper.PiOver2, mainValue.Size() / 2, Projectile.scale, SpriteEffects.None, 0);
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                Main.EntitySpriteDraw(mainValue, Projectile.oldPos[i] - Main.screenPosition + off, null
                    , lightColor * (1 - i / (float)Projectile.oldPos.Length) * 0.6f
                    , Projectile.rotation + MathHelper.PiOver2, mainValue.Size() / 2, Projectile.scale, SpriteEffects.None, 0);
            }

            return false;
        }
    }
}
