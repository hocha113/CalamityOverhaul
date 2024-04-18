using CalamityMod.Projectiles;
using CalamityMod;
using CalamityOverhaul.Common;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Terraria.ID;
using Terraria;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic;
using Terraria.Audio;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal class TheArrowPunishment : ModProjectile
    {
        public override string Texture => CWRConstant.Cay_Proj_Ranged + "CondemnationArrow";
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Projectile.type] = 1;
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 11;
        }

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 90;
            Projectile.friendly = true;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.arrow = true;
            Projectile.extraUpdates = 2;
            Projectile.timeLeft = 300;
            Projectile.penetrate = 3;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 5;
            Projectile.Calamity().pointBlankShotDuration = CalamityGlobalProjectile.DefaultPointBlankDuration;
        }

        public override void AI() {
            Lighting.AddLight(Projectile.Center, Color.Violet.ToVector3());
            Projectile.SetArrowRot();
            if (Projectile.ai[0] == 1) {
                Projectile.penetrate = 1;
                Projectile.extraUpdates = 1;
                NPC potentialTarget = Projectile.Center.ClosestNPCAt(1500f, true, true);
                if (potentialTarget != null) {
                    Projectile.velocity = (Projectile.velocity * 29f + Projectile.SafeDirectionTo(potentialTarget.Center) * 21f) / 30f;
                    Projectile.velocity *= 1.01f;
                    if (Projectile.Center.Distance(potentialTarget.Center) <= Projectile.width) {
                        Projectile.Kill();
                    }
                }  
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Projectile.ai[0] == 1) {
                Projectile.Explode(300, SoundID.Item14);

                for (int i = 0; i < 4; i++) {
                    int dust = Dust.NewDust(new Vector2(Projectile.position.X, Projectile.position.Y), Projectile.width, Projectile.height, DustID.RedTorch, 0f, 0f, 100, default, 1.5f);
                    Main.dust[dust].position = Projectile.Center + (Vector2.UnitY.RotatedByRandom(MathHelper.Pi) * (float)Main.rand.NextDouble() * Projectile.width / 2f);
                    Main.dust[dust].noGravity = true;
                }

                for (int j = 0; j < 30; j++) {
                    int dustIndex = Dust.NewDust(new Vector2(Projectile.position.X, Projectile.position.Y), Projectile.width, Projectile.height, DustID.RedTorch, 0f, 0f, 200, default, 3.7f);
                    Dust newDust = Main.dust[dustIndex];
                    newDust.position = Projectile.Center + (Vector2.UnitY.RotatedByRandom(MathHelper.Pi) * (float)Main.rand.NextDouble() * Projectile.width / 2f);
                    newDust.noGravity = true;
                    newDust.velocity *= 3f;
                    _ = Dust.NewDust(new Vector2(Projectile.position.X, Projectile.position.Y), Projectile.width, Projectile.height, DustID.RedTorch, 0f, 0f, 100, default, 1.5f);
                    newDust.position = Projectile.Center + (Vector2.UnitY.RotatedByRandom(MathHelper.Pi) * (float)Main.rand.NextDouble() * Projectile.width / 2f);
                    newDust.velocity *= 12f;
                    newDust.noGravity = true;
                    newDust.fadeIn = 1f;
                    newDust.color = Color.Crimson * 0.5f;
                }

                for (int k = 0; k < 30; k++) {
                    int num3 = Dust.NewDust(new Vector2(Projectile.position.X, Projectile.position.Y), Projectile.width, Projectile.height, DustID.RedTorch, 0f, 0f, 0, default, 2.7f);
                    Dust obj2 = Main.dust[num3];
                    obj2.position = Projectile.Center + (Vector2.UnitX.RotatedByRandom(MathHelper.Pi).RotatedBy(Projectile.velocity.ToRotation()) * Projectile.width / 2f);
                    obj2.noGravity = true;
                    obj2.velocity *= 7f;
                }

                for (int l = 0; l < 50; l++) {
                    int num4 = Dust.NewDust(new Vector2(Projectile.position.X, Projectile.position.Y), Projectile.width, Projectile.height, DustID.RedTorch, 0f, 0f, 0, default, 1.5f);
                    Dust obj3 = Main.dust[num4];
                    obj3.position = Projectile.Center + (Vector2.UnitX.RotatedByRandom(MathHelper.Pi).RotatedBy(Projectile.velocity.ToRotation()) * Projectile.width / 2f);
                    obj3.noGravity = true;
                    obj3.velocity *= 15f;
                }
            }
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 30; i++) {
                Dust fire = Dust.NewDustPerfect(Projectile.Center, 130);
                fire.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitY).RotatedByRandom(0.8f) * new Vector2(4f, 1.25f) * Main.rand.NextFloat(0.9f, 1f);
                fire.velocity = fire.velocity.RotatedBy(Projectile.rotation - MathHelper.PiOver2);
                fire.velocity += Projectile.velocity * 0.7f;

                fire.noGravity = true;
                fire.color = Color.Lerp(Color.White, Color.Purple, Main.rand.NextFloat());
                fire.scale = Main.rand.NextFloat(1f, 1.1f);

                fire = Dust.CloneDust(fire);
                fire.velocity = Main.rand.NextVector2Circular(3f, 3f);
                fire.velocity += Projectile.velocity * 0.6f;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Projectile.ai[0] == 1) {
                CalamityUtils.DrawAfterimagesCentered(Projectile, ProjectileID.Sets.TrailingMode[Projectile.type], lightColor, 1);
            }
            return base.PreDraw(ref lightColor);
        }
    }
}
