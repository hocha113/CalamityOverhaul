using CalamityMod;
using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons
{
    internal class TheRelicLuxorRogue : ModProjectile
    {
        public new string LocalizationCategory => "Projectiles.Rogue";

        public override string Texture => CWRConstant.Projectile + "TheRelicLuxorRogueProj";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 1;
        }

        public override void SetDefaults() {
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.friendly = true;
            Projectile.penetrate = 13;
            Projectile.aiStyle = 2;
            Projectile.alpha = 255;
            Projectile.aiStyle = -1;
            Projectile.timeLeft = 300;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
            Projectile.DamageType = ModContent.GetInstance<RogueDamageClass>();
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            if (Projectile.ai[0] == 0) {
                Projectile.velocity.Y += 0.1f;
                Projectile.velocity.X *= 1.01f;
            }
            if (Projectile.ai[0] == 1) {
                Player owners = CWRUtils.GetPlayerInstance(Projectile.owner);
                if (owners != null) {
                    Projectile.ChasingBehavior2(owners.Center, 0.999f, 0.2f);
                    if (Projectile.timeLeft <= 120) {
                        Projectile.ai[0] = 2;
                    }
                }
            }
            if (Projectile.ai[0] == 2) {
                if (Projectile.IsOwnedByLocalPlayer()) {
                    Projectile.velocity = Projectile.Center.To(Main.MouseWorld).UnitVector() * 22f;
                    Projectile.ai[0] = 3;
                }
            }

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
            Projectile.localAI[1] += 1f;
            if (Projectile.localAI[1] == 3f) {
                SpanDust();
            }
            if (Projectile.timeLeft % 20 == 0)
                SpanDust(22);
        }

        private void SpanDust(int maxfores = 12) {
            for (int i = 0; i < maxfores; i++) {
                Vector2 modeVrs = Vector2.UnitX * (0f - Projectile.width) / 2f;
                modeVrs -= Vector2.UnitY.RotatedBy(i * CWRUtils.PiOver6) * new Vector2(8f, 16f);
                modeVrs = modeVrs.RotatedBy(Projectile.rotation - MathHelper.PiOver2);
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.ShadowbeamStaff, 0f, 0f, 160);
                Main.dust[dust].scale = 1.1f;
                Main.dust[dust].noGravity = true;
                Main.dust[dust].position = Projectile.Center + modeVrs;
                Main.dust[dust].velocity = Projectile.velocity * 0.1f;
                Main.dust[dust].velocity = Vector2.Normalize(Projectile.Center
                    - Projectile.velocity * 3f - Main.dust[dust].position) * 1.25f;
            }
        }

        public override Color? GetAlpha(Color lightColor) {
            if (Projectile.timeLeft < 85) {
                byte b2 = (byte)(Projectile.timeLeft * 3);
                byte a2 = (byte)(100f * (b2 / 255f));
                return new Color(b2, b2, b2, a2);
            }
            return new Color(255, 255, 255, 100);
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            SpanDust();
            Projectile.penetrate--;
            Projectile.ai[0] = 1;
            if (Projectile.penetrate <= 0) {
                Projectile.Kill();
            }
            else {
                if (Projectile.velocity.X != oldVelocity.X) {
                    Projectile.velocity.X = 0f - oldVelocity.X;
                }
                if (Projectile.velocity.Y != oldVelocity.Y) {
                    Projectile.velocity.Y = 0f - oldVelocity.Y;
                }
            }
            return false;
        }

        public override bool PreDraw(ref Color lightColor) {
            CalamityUtils.DrawAfterimagesCentered(Projectile, ProjectileID.Sets.TrailingMode[Projectile.type], lightColor, 2);
            return false;
        }
    }
}
