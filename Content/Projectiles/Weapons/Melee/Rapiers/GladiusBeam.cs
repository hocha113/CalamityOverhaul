using CalamityMod;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.Rapiers
{
    internal class GladiusBeam : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        private float tileRot;
        private Vector2 tilePos;
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.Size = new Vector2(36);
            Projectile.penetrate = 1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = 330;
            Projectile.extraUpdates = 3;
            VaultUtils.SafeLoadItem(ItemID.Gladius);
        }

        public override void AI() {
            Projectile.ai[1] = (float)Math.Abs(Math.Sin(Projectile.timeLeft * 0.05f));
            Projectile.alpha = (int)(155 + Projectile.ai[1] * 100);
            if (Projectile.ai[0] > 20) {
                Projectile.velocity *= 0.98f;
            }

            if (Projectile.ai[2] > 0) {
                Projectile.rotation = tileRot;
                Projectile.Center = tilePos;
                Player player = CWRUtils.GetPlayerInstance(Projectile.owner);
                if (player != null) {
                    if (player.Distance(Projectile.Center) < Projectile.width) {
                        player.CWR().ReceivingPlatformTime = 2;
                    }
                }
            }
            else {
                Projectile.rotation = Projectile.velocity.ToRotation();
                CalamityUtils.HomeInOnNPC(Projectile, true, 250f, 10f, 25f);
            }
            Projectile.ai[0]++;
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (Projectile.ai[2] == 0) {
                tileRot = Projectile.rotation;
                tilePos = Projectile.Center;
                Projectile.timeLeft = 300;
                Projectile.velocity = Vector2.Zero;
                Projectile.ai[2]++;
            }
            return false;
        }

        public override void OnKill(int timeLeft) {
            CWRUtils.SplashDust(Projectile, 11, DustID.WhiteTorch, DustID.WhiteTorch, 16, Color.White);
            Projectile.Explode(explosionSound: SoundID.Item14 with { Pitch = 0.6f, Volume = 0.6f });
        }

        public override bool PreDraw(ref Color color) {
            Texture2D mainValue = TextureAssets.Item[ItemID.Gladius].Value;
            Main.spriteBatch.Draw(mainValue, Projectile.Center - Main.screenPosition, null
                , Color.White with { G = (byte)(Projectile.ai[1] * 255) } * (Projectile.alpha / 255f)
                , Projectile.rotation + MathHelper.PiOver4, mainValue.Size() / 2, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
