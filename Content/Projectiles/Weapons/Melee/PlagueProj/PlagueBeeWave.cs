using CalamityMod;
using CalamityMod.Buffs.DamageOverTime;
using CalamityOverhaul.Content.Projectiles.Others;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.PlagueProj
{
    internal class PlagueBeeWave : ModProjectile
    {
        public override string Texture => "CalamityMod/Projectiles/Melee/VirulentWave";
        private List<Bee> bees = [];
        public override void SetStaticDefaults() {
            Main.projFrames[Projectile.type] = 4;
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 4;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
        }

        public override void SetDefaults() {
            Projectile.width = 70;
            Projectile.height = 70;
            Projectile.friendly = true;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.MaxUpdates = 3;
            Projectile.timeLeft = 150 * Projectile.MaxUpdates;
            Projectile.alpha = 100;
        }

        public override void AI() {
            Player player = Main.player[Projectile.owner];
            Projectile.ai[1]++;
            Lighting.AddLight(Projectile.Center, 0.05f, 0.4f, 0f);
            if (Projectile.ai[1] == 60f) {

            }
            if (Projectile.ai[1] < 60f) {
                if (Projectile.ai[0] > 7f) {
                    float scalar = 1f;
                    if (Projectile.ai[0] == 8f) {
                        scalar = 0.25f;
                    }
                    else if (Projectile.ai[0] == 9f) {
                        scalar = 0.5f;
                    }
                    else if (Projectile.ai[0] == 10f) {
                        scalar = 0.75f;
                    }

                    int dustType = 89;
                    int plague = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, dustType, Projectile.velocity.X * 0.2f, Projectile.velocity.Y * 0.2f, 100, default, 1f);
                    Dust dust = Main.dust[plague];
                    if (Main.rand.NextBool(3)) {
                        dust.noGravity = true;
                        dust.scale *= 1.8f;
                        dust.velocity.X *= 2f;
                        dust.velocity.Y *= 2f;
                    }
                    else {
                        dust.scale *= 1.3f;
                    }
                    dust.velocity.X *= 1.2f;
                    dust.velocity.Y *= 1.2f;
                    dust.scale *= scalar;
                }
                else {
                    Projectile.ai[0] += 1f;
                }
            }
            else {
                Projectile.damage = (int)(Projectile.damage * 0.6);
                Projectile.velocity *= 0.85f;
                //Fade out
                if (Projectile.alpha < 255) {
                    Projectile.alpha += 5;
                }

                if (Projectile.alpha >= 255) {
                    Projectile.Kill();
                }
            }

            Projectile.spriteDirection = Projectile.direction = (Projectile.velocity.X > 0).ToDirectionInt();
            Projectile.rotation = Projectile.velocity.ToRotation() + (Projectile.spriteDirection == 1 ? 0f : MathHelper.Pi);

            CWRUtils.ClockFrame(ref Projectile.frame, 8, 3);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.immune[Projectile.owner] = 8;
            target.AddBuff(ModContent.BuffType<Plague>(), 180);
        }

        public override bool PreDraw(ref Color lightColor) {
            CalamityUtils.DrawAfterimagesCentered(Projectile, ProjectileID.Sets.TrailingMode[Projectile.type], lightColor, 1);
            Texture2D value = CWRUtils.GetT2DValue(CWRConstant.Projectile + "Bee");
            foreach (Bee bee in bees) {
                bee.Draw(Main.spriteBatch, value);
            }
            return false;
        }
    }
}
