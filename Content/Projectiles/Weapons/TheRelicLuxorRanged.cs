using CalamityMod;
using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons
{
    internal class TheRelicLuxorRanged : ModProjectile
    {
        public new string LocalizationCategory => "Projectiles.Ranged";

        public override string Texture => CWRConstant.Projectile + "TheRelicLuxorRangedProj";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 1;
        }

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 1;
            Projectile.alpha = 255;
            Projectile.timeLeft = 180;
        }

        public override Color? GetAlpha(Color lightColor) {
            if (Projectile.timeLeft < 85) {
                byte b2 = (byte)(Projectile.timeLeft * 3);
                byte a2 = (byte)(100f * (b2 / 255f));
                return new Color(b2, b2, b2, a2);
            }
            return new Color(255, 255, 255, 100);
        }

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.scale -= 0.01f;
                Projectile.alpha += 15;
                if (Projectile.alpha >= 250) {
                    Projectile.alpha = 255;
                    Projectile.localAI[0] = 1f;
                }
            }
            else if (Projectile.localAI[0] == 1f) {
                Projectile.scale += 0.01f;
                Projectile.alpha -= 15;
                if (Projectile.alpha <= 0) {
                    Projectile.alpha = 0;
                    Projectile.localAI[0] = 0f;
                }
            }
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            Projectile.velocity *= 1.01f;
            Projectile.localAI[1] += 1f;
            if (Projectile.timeLeft % 20 == 0 || Projectile.localAI[1] == 3f) {
                SpanDust();
            }
        }

        public void SpanDust() {
            for (int i = 0; i < 12; i++) {
                Vector2 offset = Vector2.UnitX * (0f - Projectile.width) / 2f;
                offset += -Vector2.UnitY.RotatedBy(i * CWRUtils.PiOver6) * new Vector2(8f, 16f);
                offset = offset.RotatedBy(Projectile.rotation - MathHelper.PiOver2);
                int electric = Dust.NewDust(Projectile.Center, 0, 0, DustID.IceTorch, 0f, 0f, 160);
                Main.dust[electric].scale = 1.1f;
                Main.dust[electric].noGravity = true;
                Main.dust[electric].position = Projectile.Center + offset;
                Main.dust[electric].velocity = Projectile.velocity * 0.1f;
                Main.dust[electric].velocity = Vector2.Normalize(Projectile.Center - Projectile.velocity * 3f - Main.dust[electric].position) * 1.25f;
            }
        }

        public override void OnKill(int timeLeft) {
            Projectile.ExpandHitboxBy(16);
            Projectile.maxPenetrate = Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
            Projectile.Damage();
            SoundEngine.PlaySound(in SoundID.Item92, Projectile.position);
            int dustAmt = Main.rand.Next(10, 20);
            for (int d = 0; d < dustAmt; d++) {
                int electric = Dust.NewDust(Projectile.Center - Projectile.velocity / 2f, 0, 0, DustID.IceTorch, 0f, 0f, 100, default, 2f);
                Dust obj = Main.dust[electric];
                obj.velocity *= 2f;
                Main.dust[electric].noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            CalamityUtils.DrawAfterimagesCentered(Projectile, ProjectileID.Sets.TrailingMode[Projectile.type], lightColor, 2);
            return false;
        }
    }
}
