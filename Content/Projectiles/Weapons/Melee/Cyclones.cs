using CalamityMod;
using CalamityMod.CalPlayer;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee
{
    internal class Cyclones : ModProjectile
    {
        public int dustvortex;
        public override string Texture => CWRConstant.Cay_Proj_Melee + "Cyclone";
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 6;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
        }

        public override void SetDefaults() {
            Projectile.width = 56;
            Projectile.height = 56;
            Projectile.alpha = 255;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.timeLeft = 300;
            Projectile.extraUpdates = 2;
            Projectile.penetrate = 2;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
            Projectile.tileCollide = false;
        }

        public override void AI() {
            Projectile.rotation += 2.5f;
            Projectile.alpha -= 5;
            if (Projectile.alpha < 50) {
                Projectile.alpha = 50;
                if (Projectile.ai[2] >= 15f) {
                    for (int i = 1; i <= 6; i++) {
                        Vector2 velocity = new Vector2(3f, 3f).RotatedBy(MathHelper.ToRadians(dustvortex));
                        int num = Dust.NewDust(Projectile.Center, Projectile.width / 2, Projectile.height / 2, DustID.Smoke, velocity.X, velocity.Y, 200, new Color(232, 251, 250, 200), 1.3f);
                        Main.dust[num].noGravity = true;
                        Main.dust[num].velocity = velocity;
                        dustvortex += 60;
                    }

                    dustvortex -= 355;
                    Projectile.ai[2] = 0f;
                }
            }

            if (Projectile.ai[0] == 0) {
                Projectile.ai[1]++;
                Projectile.ai[2]++;
                if (Projectile.ai[1] >= 12f) {
                    Projectile.tileCollide = true;
                }
                float projX = Projectile.Center.X;
                float projY = Projectile.Center.Y;
                float num2 = 600f;
                for (int j = 0; j < Main.maxNPCs; j++) {
                    NPC npc = Main.npc[j];
                    if (!npc.CanBeChasedBy(Projectile) || !Collision.CanHit(Projectile.Center, 1, 1, npc.Center, 1, 1) || CalamityPlayer.areThereAnyDamnBosses) {
                        continue;
                    }

                    float num3 = npc.position.X + npc.width / 2;
                    float num4 = npc.position.Y + npc.height / 2;
                    if (Math.Abs(Projectile.position.X + Projectile.width / 2 - num3) + Math.Abs(Projectile.position.Y + Projectile.height / 2 - num4) < num2) {
                        if (npc.position.X < projX) {
                            npc.velocity.X += 0.05f;
                        }
                        else {
                            npc.velocity.X -= 0.05f;
                        }

                        if (npc.position.Y < projY) {
                            npc.velocity.Y += 0.05f;
                        }
                        else {
                            npc.velocity.Y -= 0.05f;
                        }
                    }
                }
            }
            if (Projectile.ai[0] == 1) {
                Projectile.localAI[0]++;
                Projectile.scale = 0.7f + MathF.Abs(MathF.Sin(MathHelper.ToRadians(Projectile.localAI[0] * 2)) * 0.5f);
                NPC target = Projectile.Center.FindClosestNPC(300);
                if (target != null) {
                    Projectile.ChasingBehavior2(target.Center, 1, 0.1f);
                }
                if (CWRUtils.GetTile(CWRUtils.WEPosToTilePos(Projectile.position)).HasSolidTile()) {
                    Projectile.velocity *= 0.99f;
                    if (Projectile.velocity.LengthSquared() < 2) {
                        Projectile.Kill();
                    }
                }
                if (Projectile.timeLeft < 300 && Projectile.timeLeft % 30 == 0) {
                    for (int i = 0; i <= 360; i += 3) {
                        Vector2 velocity = new Vector2(3f, 3f).RotatedBy(MathHelper.ToRadians(i)) * Projectile.scale;
                        int num = Dust.NewDust(Projectile.Center, Projectile.width, Projectile.height, DustID.Smoke, velocity.X, velocity.Y, 200, new Color(232, 251, 250, 200), 1.4f);
                        Main.dust[num].noGravity = true;
                        Main.dust[num].position = Projectile.Center;
                        Main.dust[num].velocity = velocity;
                    }
                }
                Lighting.AddLight(Projectile.Center, Color.White.ToVector3() * Projectile.scale * 0.5f);
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (Projectile.ai[0] == 0) {
                if (Projectile.velocity.X != oldVelocity.X)
                    Projectile.velocity.X = -oldVelocity.X;

                if (Projectile.velocity.Y != oldVelocity.Y)
                    Projectile.velocity.Y = -oldVelocity.Y;

                if (Projectile.numHits > 12) {
                    Projectile.Kill();
                }

                SpanDust();
            }
            return false;
        }

        public override Color? GetAlpha(Color lightColor) {
            return new Color(204, 255, 255, Projectile.alpha);
        }

        public void SpanDust() {
            for (int i = 0; i <= 360; i += 3) {
                Vector2 velocity = new Vector2(3f, 3f).RotatedBy(MathHelper.ToRadians(i));
                int num = Dust.NewDust(Projectile.Center, Projectile.width, Projectile.height, DustID.Smoke, velocity.X, velocity.Y, 200, new Color(232, 251, 250, 200), 1.4f);
                Main.dust[num].noGravity = true;
                Main.dust[num].position = Projectile.Center;
                Main.dust[num].velocity = velocity;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            CalamityUtils.DrawAfterimagesCentered(Projectile, ProjectileID.Sets.TrailingMode[Projectile.type], lightColor);
            return false;
        }

        public override void OnKill(int timeLeft) {
            SoundStyle style = SoundID.Item60;
            style.Volume = SoundID.Item60.Volume * 0.6f;
            SoundEngine.PlaySound(in style, Projectile.Center);
            SpanDust();
        }
    }
}
