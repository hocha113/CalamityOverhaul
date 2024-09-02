using CalamityMod;
using CalamityMod.Projectiles.Magic;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Magic
{
    internal class GhastlyBlasts : ModProjectile
    {
        public override string Texture => CWRConstant.Cay_Proj_Magic + "GhastlyBlast";
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
        }

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.alpha = 255;
            Projectile.penetrate = 6;
            Projectile.MaxUpdates = 3;
            Projectile.timeLeft = 300;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
        }

        public override void AI() {
            Projectile.ai[0] += 1f;
            Projectile.alpha -= 15;
            if (Projectile.alpha < 0) {
                Projectile.alpha = 0;
            }
            NPC target = Projectile.Center.FindClosestNPC(1600);
            Projectile.rotation -= MathF.PI / 30f;
            if (Projectile.numHits == 0) {
                SpanDust();

                if (Projectile.timeLeft < 200) {

                    if (target != null) {
                        _ = Projectile.timeLeft > 100 ? Projectile.ChasingBehavior2(target.Center) : Projectile.ChasingBehavior(target.Center, 16);
                    }
                }
            }
            else {
                if (target != null) {
                    _ = Projectile.ChasingBehavior2(target.Center, 1, 0.5f);
                }
            }

            if (Projectile.alpha < 150) {
                Lighting.AddLight(Projectile.Center, 0.9f, 0f, 0.1f);
            }

            if (Projectile.ai[0] >= 360f * Projectile.MaxUpdates) {
                Projectile.Kill();
            }
        }

        public void SpanDust() {
            for (int i = 0; i < 1; i++) {
                if (Main.rand.NextBool()) {
                    Vector2 vector3 = Vector2.UnitY.RotatedByRandom(6.2831854820251465);
                    Dust dust = Main.dust[Dust.NewDust(Projectile.Center - (vector3 * 30f), 0, 0, DustID.RedTorch)];
                    dust.noGravity = true;
                    dust.position = Projectile.Center - (vector3 * Main.rand.Next(10, 21));
                    dust.velocity = vector3.RotatedBy(1.5707963705062866) * 6f;
                    dust.scale = 0.9f + Main.rand.NextFloat();
                    dust.fadeIn = 0.5f;
                    dust.customData = Projectile;
                    vector3 = Vector2.UnitY.RotatedByRandom(6.2831854820251465);
                    dust.noGravity = true;
                    dust.position = Projectile.Center - (vector3 * Main.rand.Next(10, 21));
                    dust.velocity = vector3.RotatedBy(1.5707963705062866) * 6f;
                    dust.scale = 0.9f + Main.rand.NextFloat();
                    dust.fadeIn = 0.5f;
                    dust.customData = Projectile;
                    dust.color = Color.Crimson;
                }
                else {
                    Vector2 vector4 = Vector2.UnitY.RotatedByRandom(6.2831854820251465);
                    Dust dust = Main.dust[Dust.NewDust(Projectile.Center - (vector4 * 30f), 0, 0, DustID.RedTorch)];
                    dust.noGravity = true;
                    dust.position = Projectile.Center - (vector4 * Main.rand.Next(20, 31));
                    dust.velocity = vector4.RotatedBy(-1.5707963705062866) * 5f;
                    dust.scale = 0.9f + Main.rand.NextFloat();
                    dust.fadeIn = 0.5f;
                    dust.customData = Projectile;
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            CalamityUtils.DrawAfterimagesCentered(Projectile, ProjectileID.Sets.TrailingMode[Projectile.type], lightColor);
            return false;
        }

        public override Color? GetAlpha(Color lightColor) {
            return new Color(255 - Projectile.alpha, 255 - Projectile.alpha, 255 - Projectile.alpha, 255 - Projectile.alpha);
        }

        public override void OnKill(int timeLeft) {
            Projectile.position = Projectile.Center;
            Projectile.width = Projectile.height = 238;
            Projectile.Center = Projectile.position;
            Projectile.maxPenetrate = -1;
            Projectile.penetrate = -1;
            Projectile.Damage();
            _ = SoundEngine.PlaySound(in SoundID.Item14, Projectile.position);
            for (int i = 0; i < 4; i++) {
                int dust = Dust.NewDust(new Vector2(Projectile.position.X, Projectile.position.Y), Projectile.width, Projectile.height, DustID.RedTorch, 0f, 0f, 100, default, 1.5f);
                Main.dust[dust].position = Projectile.Center + (Vector2.UnitY.RotatedByRandom(3.1415927410125732) * (float)Main.rand.NextDouble() * Projectile.width / 2f);
            }

            for (int j = 0; j < 30; j++) {
                int dustIndex = Dust.NewDust(new Vector2(Projectile.position.X, Projectile.position.Y), Projectile.width, Projectile.height, DustID.RedTorch, 0f, 0f, 200, default, 3.7f);
                Dust newDust = Main.dust[dustIndex];
                newDust.position = Projectile.Center + (Vector2.UnitY.RotatedByRandom(3.1415927410125732) * (float)Main.rand.NextDouble() * Projectile.width / 2f);
                newDust.noGravity = true;
                newDust.velocity *= 3f;
                _ = Dust.NewDust(new Vector2(Projectile.position.X, Projectile.position.Y), Projectile.width, Projectile.height, DustID.RedTorch, 0f, 0f, 100, default, 1.5f);
                newDust.position = Projectile.Center + (Vector2.UnitY.RotatedByRandom(3.1415927410125732) * (float)Main.rand.NextDouble() * Projectile.width / 2f);
                newDust.velocity *= 2f;
                newDust.noGravity = true;
                newDust.fadeIn = 1f;
                newDust.color = Color.Crimson * 0.5f;
            }

            for (int k = 0; k < 10; k++) {
                int num3 = Dust.NewDust(new Vector2(Projectile.position.X, Projectile.position.Y), Projectile.width, Projectile.height, DustID.RedTorch, 0f, 0f, 0, default, 2.7f);
                Dust obj2 = Main.dust[num3];
                obj2.position = Projectile.Center + (Vector2.UnitX.RotatedByRandom(3.1415927410125732).RotatedBy(Projectile.velocity.ToRotation()) * Projectile.width / 2f);
                obj2.noGravity = true;
                obj2.velocity *= 3f;
            }

            for (int l = 0; l < 10; l++) {
                int num4 = Dust.NewDust(new Vector2(Projectile.position.X, Projectile.position.Y), Projectile.width, Projectile.height, DustID.RedTorch, 0f, 0f, 0, default, 1.5f);
                Dust obj3 = Main.dust[num4];
                obj3.position = Projectile.Center + (Vector2.UnitX.RotatedByRandom(3.1415927410125732).RotatedBy(Projectile.velocity.ToRotation()) * Projectile.width / 2f);
                obj3.noGravity = true;
                obj3.velocity *= 3f;
            }

            if (Main.myPlayer != Projectile.owner) {
                return;
            }

            for (int m = 0; m < Main.maxProjectiles; m++) {
                if (Main.projectile[m].active && Main.projectile[m].type == ModContent.ProjectileType<GhastlySubBlast>() && Main.projectile[m].ai[1] == Projectile.whoAmI) {
                    Main.projectile[m].Kill();
                }
            }
        }
    }
}
