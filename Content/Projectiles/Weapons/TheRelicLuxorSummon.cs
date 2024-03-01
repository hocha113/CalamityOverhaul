using CalamityMod;
using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons
{
    internal class TheRelicLuxorSummon : ModProjectile
    {
        private int dust = 3;

        public new string LocalizationCategory => "Projectiles.Summon";

        public override string Texture => CWRConstant.Projectile + "TheRelicLuxorSummonProj";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.MinionTargettingFeature[Projectile.type] = true;
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 1;
        }

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.netImportant = true;
            Projectile.friendly = true;
            Projectile.ignoreWater = true;
            Projectile.minionSlots = 0f;
            Projectile.timeLeft = 180000;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.timeLeft *= 5;
            Projectile.minion = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
            Projectile.DamageType = DamageClass.Summon;
        }

        int status = 0;
        int bevers = 0;
        int time = 0;
        Vector2 dashVr = Vector2.Zero;

        public void SpanDust() {
            int maxRogs = 36;
            for (int i = 0; i < maxRogs; i++) {
                Vector2 val = (Vector2.Normalize(Projectile.velocity) * new Vector2(Projectile.width / 2f, Projectile.height) * 0.75f).RotatedBy((i - (maxRogs / 2 - 1)) * ((float)Math.PI * 2f) / maxRogs) + Projectile.Center;
                Vector2 vector7 = val - Projectile.Center;
                int dust = Dust.NewDust(val + vector7, 0, 0, DustID.TerraBlade, vector7.X * 1.75f, vector7.Y * 1.75f, 100, default, 1.1f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = vector7;
            }
        }

        public override void AI() {
            Player player = Main.player[Projectile.owner];
            CWRPlayer modPlayer = player.CWR();
            if (dust > 0) {
                SpanDust();
                dust--;
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
            if (Projectile.type == ModContent.ProjectileType<TheRelicLuxorSummon>() && (modPlayer.theRelicLuxor == 0 || player.dead)) {
                Projectile.Kill();
            }
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            NPC target = Projectile.Center.FindClosestNPC(900);
            if (target != null) {
                Vector2 toTargetVr = Projectile.Center.To(target.Center);
                if (status == 0) {
                    if (bevers == 0) {
                        Projectile.ChasingBehavior2(target.Center, 1.01f, 0.02f);
                        if (time > 30 || toTargetVr.LengthSquared() < 130 * 130) {
                            bevers = 1;
                            time = 0;
                        }
                    }
                    if (bevers == 1) {
                        SpanDust();
                        dashVr = toTargetVr;
                        bevers = 2;
                    }
                    if (bevers == 2) {
                        Projectile.velocity = dashVr.UnitVector() * 32f;
                        Projectile.damage = Projectile.originalDamage * 3;
                        if (Projectile.damage > 800)
                            Projectile.damage = 800;
                        if (time > 15) {
                            bevers = 0;
                            time = 0;
                            SpanDust();
                            Projectile.Center = player.Center;
                            Projectile.velocity = Projectile.velocity.UnitVector().RotatedBy(Main.rand.NextFloat(-0.35f, 0.35f)) * 12;
                            SpanDust();
                        }
                    }

                    if (Projectile.Center.To(player.Center).LengthSquared() > 3200 * 3200) {
                        SpanDust();
                        Projectile.Center = player.Center;
                    }
                }
            }
            else {
                Projectile.ChargingMinionAI(700f, 1000f, 2200f, 150f, 0, 40f, 9f, 4f
                    , new Vector2(0f, -60f), 40f, 9f, tileVision: true, ignoreTilesWhenCharging: true);
            }
            time++;
        }

        public override Color? GetAlpha(Color lightColor) {
            if (Projectile.timeLeft < 85) {
                byte b2 = (byte)(Projectile.timeLeft * 3);
                byte a2 = (byte)(100f * (b2 / 255f));
                return new Color(b2, b2, b2, a2);
            }
            return new Color(255, 255, 255, 100);
        }

        public override bool PreDraw(ref Color lightColor) {
            CalamityUtils.DrawAfterimagesCentered(Projectile, ProjectileID.Sets.TrailingMode[Projectile.type], lightColor, 2);
            return false;
        }
    }
}
