using CalamityMod;
using CalamityMod.Particles;
using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Rogue
{
    [LegacyName("DuneHopperProjectile")]
    public class RWaveSkipperProjectile : ModProjectile, ILocalizedModType
    {
        public new string LocalizationCategory => "Projectiles.Rogue";
        public override string Texture => "CalamityMod/Items/Weapons/Rogue/WaveSkipper";
        public int Time = 0;
        public int TimeUnderground = 0;
        public bool PostExitTiles = false;
        public bool InitialTileHit = false;
        public bool InsideTiles = false;
        public Vector2 SavedOldVelocity;
        public Vector2 NPCDestination;
        public bool SetPierce = false;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 1;
        }

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.friendly = true;
            Projectile.penetrate = 6;
            Projectile.DamageType = ModContent.GetInstance<RogueDamageClass>();
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Projectile.tileCollide = true;
            Projectile.MaxUpdates = 2;
        }

        public override void AI() {
            if (!SetPierce) {
                Projectile.penetrate = Projectile.Calamity().stealthStrike ? 4 : 2;
                SetPierce = true;
            }
            InsideTiles = Collision.SolidCollision(Projectile.Center, 50, 50);
            Time++;
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;
            Player Owner = Main.player[Projectile.owner];
            float playerDist = Vector2.Distance(Owner.Center, Projectile.Center);

            if (Main.rand.NextBool(2)) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(20, 20), Main.rand.NextBool() ? 288 : 207);
                dust.scale = Main.rand.NextFloat(0.3f, 0.55f);
                dust.noGravity = true;
                dust.velocity = -Projectile.velocity * 0.5f;
            }

            if (!InitialTileHit && Time > 45) {
                if (Projectile.velocity.Y < 0) {
                    Projectile.velocity.Y *= 0.95f;
                }
                Projectile.velocity.Y += 0.05f;
                Projectile.velocity.X *= 0.99f;
            }

            if (InitialTileHit && !InsideTiles && !PostExitTiles) // 入土时候让它做出一些行为
            {
                Projectile.extraUpdates = 4;
                if (!Projectile.Calamity().stealthStrike)
                    Projectile.timeLeft = 200;
                SoundEngine.PlaySound(SoundID.NPCHit11 with { Volume = 1.3f, Pitch = 1.1f }, Projectile.Center);
                for (int i = 0; i <= 55; i++) {
                    Dust dust = Dust.NewDustPerfect(Projectile.Center - Projectile.velocity * 6.5f, Main.rand.NextBool() ? 207 : 216, Projectile.velocity.RotatedByRandom(MathHelper.ToRadians(30f)) * Main.rand.NextFloat(1.3f, 2.9f), 0, default, Main.rand.NextFloat(1.5f, 2.8f));
                    dust.noGravity = true;
                }
                PostExitTiles = true;
            }
            if (PostExitTiles) {
                if (Projectile.timeLeft % 2 == 0 && playerDist < 1400f) {
                    SparkParticle spark = new SparkParticle(Projectile.Center - Projectile.velocity * 8f, -Projectile.velocity * 0.1f, false, 9, 2.3f, Color.Yellow * 0.1f);
                    GeneralParticleHandler.SpawnParticle(spark);
                }
            }
            if (InsideTiles) {
                for (int i = 0; i < Main.maxNPCs; i++) {
                    if (Main.npc[i].CanBeChasedBy(Projectile.GetSource_FromThis(), false))
                        NPCDestination = Main.npc[i].Center + Main.npc[i].velocity * 5f;
                }

                TimeUnderground++;
                Vector3 DustLight = new Vector3(0.171f, 0.124f, 0.086f);
                Lighting.AddLight(Projectile.Center + Projectile.velocity, DustLight * 4);
                if (Time % 15 == 0 && TimeUnderground < 120) {
                    SoundEngine.PlaySound(SoundID.WormDig with { Volume = 0.7f, Pitch = 0.2f }, Projectile.Center);
                }
                float returnSpeed = 10;
                float acceleration = 0.2f;
                float xDist = NPCDestination.X - Projectile.Center.X;
                float yDist = NPCDestination.Y - Projectile.Center.Y;
                float dist = (float)Math.Sqrt(xDist * xDist + yDist * yDist);
                dist = returnSpeed / dist;
                xDist *= dist;
                yDist *= dist;
                float targetDist = Vector2.Distance(NPCDestination, Projectile.Center);
                if (targetDist < 1800 && TimeUnderground > 25) {
                    if (Projectile.velocity.X < xDist) {
                        Projectile.velocity.X = Projectile.velocity.X + acceleration;
                        if (Projectile.velocity.X < 0f && xDist > 0f)
                            Projectile.velocity.X += acceleration;
                    }
                    else if (Projectile.velocity.X > xDist) {
                        Projectile.velocity.X = Projectile.velocity.X - acceleration;
                        if (Projectile.velocity.X > 0f && xDist < 0f)
                            Projectile.velocity.X -= acceleration;
                    }
                    if (Projectile.velocity.Y < yDist) {
                        Projectile.velocity.Y = Projectile.velocity.Y + acceleration;
                        if (Projectile.velocity.Y < 0f && yDist > 0f)
                            Projectile.velocity.Y += acceleration;
                    }
                    else if (Projectile.velocity.Y > yDist) {
                        Projectile.velocity.Y = Projectile.velocity.Y - acceleration;
                        if (Projectile.velocity.Y > 0f && yDist < 0f)
                            Projectile.velocity.Y -= acceleration;
                    }
                }
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            SavedOldVelocity = oldVelocity;
            Projectile.tileCollide = false;
            if (!InitialTileHit) // Enter ground
            {
                for (int i = 0; i <= 25; i++) {
                    Dust dust = Dust.NewDustPerfect(Projectile.Center + Projectile.velocity * 3, Main.rand.NextBool() ? 207 : 216, -Projectile.velocity.RotatedByRandom(MathHelper.ToRadians(30f)) * Main.rand.NextFloat(0.3f, 0.9f), 0, default, Main.rand.NextFloat(1.3f, 1.8f));
                    dust.noGravity = true;
                }
                Projectile.velocity = oldVelocity * 0.7f;
                SoundEngine.PlaySound(SoundID.WormDig with { Volume = 1.5f, Pitch = 1.1f }, Projectile.Center);
                InitialTileHit = true;
            }
            return false;
        }

        public override bool PreDraw(ref Color lightColor) {
            CalamityUtils.DrawAfterimagesCentered(Projectile, ProjectileID.Sets.TrailingMode[Projectile.type], lightColor, Projectile.Calamity().stealthStrike ? 4 : 2);
            return false;
        }
    }
}
