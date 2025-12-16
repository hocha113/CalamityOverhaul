using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.NeutronBowProjs;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal class NeutronBullet : ModProjectile, IWarpDrawable
    {
        public override string Texture => "CalamityMod/Projectiles/Ranged/GodSlayerSlugBlue";
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 6;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
        }

        public override void SetDefaults() {
            Projectile.width = 4;
            Projectile.height = 4;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.ignoreWater = true;
            Projectile.alpha = 255;
            Projectile.MaxUpdates = 6;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = 60;
        }

        public override void AI() {
            if (Projectile.alpha > 0) {
                Projectile.alpha -= 5;
            }
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), target.Center
                , Vector2.Zero, ModContent.ProjectileType<NeutronExplosionRanged>(), Projectile.damage, 0);
            if (Projectile.ai[0] > 0 && Projectile.numHits < 8) {
                for (int i = 0; i < 3; i++) {
                    Projectile.NewProjectile(Projectile.GetSource_FromThis()
                    , target.Center + new Vector2(Main.rand.Next(-120, 120), -Main.rand.Next(720, 850))
                    , new Vector2(0, 22), ModContent.ProjectileType<NeutronLaser>(), Projectile.damage, 0);
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;

        public bool CanDrawCustom() => true;

        public void DrawCustom(SpriteBatch spriteBatch) {
            CWRRef.DrawAfterimagesFromEdge(Projectile, 0, Color.White, TextureAssets.Projectile[Type].Value);
        }

        public void Warp() {
            Texture2D warpTex = CWRUtils.GetT2DAsset(CWRConstant.Masking + "StarTexture_White").Value;
            Color warpColor = new Color(45, 45, 45) * 0.1f;
            for (int i = 0; i < 3; i++) {
                Main.spriteBatch.Draw(warpTex, Projectile.Center - Main.screenPosition
                    , null, warpColor, Projectile.rotation, warpTex.Size() / 2, 0.2f, SpriteEffects.None, 0f);
            }
        }
    }
}
