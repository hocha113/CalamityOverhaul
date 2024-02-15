using CalamityMod.Buffs.StatDebuffs;
using CalamityMod.Particles;
using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee
{
    internal class DarkIceZeros : ModProjectile
    {
        public override string Texture => CWRConstant.Cay_Proj_Melee + "DarkIceZero";

        public ref float Time => ref Projectile.ai[0];

        public Color InnerColor = Color.BlueViolet;

        public override void SetDefaults() {
            Projectile.width = 28;
            Projectile.height = 28;
            Projectile.timeLeft = 600;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 1;
            Projectile.ignoreWater = true;
            Projectile.extraUpdates = 2;
            Projectile.coldDamage = true;
        }

        public override bool PreDraw(ref Color lightColor) {
            return Projectile.timeLeft <= 595;
        }

        public override void AI() {
            if (Math.Abs(Projectile.velocity.X) + Math.Abs(Projectile.velocity.Y) < 16f) {
                Projectile.velocity *= 1.045f;
            }

            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            Lighting.AddLight(Projectile.Center, InnerColor.ToVector3() * 0.2f);
            Player Owner = Main.player[Projectile.owner];
            float targetDist = Vector2.Distance(Owner.Center, Projectile.Center);

            if (Projectile.timeLeft % 3 == 0 && Time > 5f && targetDist < 1400f) {
                AltSparkParticle spark = new(Projectile.Center, Projectile.velocity * 0.05f, false, 12, 2.3f, Color.DarkBlue);
                GeneralParticleHandler.SpawnParticle(spark);
            }

            if (Main.rand.NextBool(3) && Time > 5f && targetDist < 1400f) {
                Particle orb = new GenericBloom(Projectile.Center + Main.rand.NextVector2Circular(10, 10), Projectile.velocity * Main.rand.NextFloat(0.05f, 0.5f), Color.WhiteSmoke, Main.rand.NextFloat(0.2f, 0.45f), Main.rand.Next(9, 12), true, false);
                GeneralParticleHandler.SpawnParticle(orb);
            }

            if (Projectile.timeLeft % 3 == 0 && Time > 5f && targetDist < 1400f) {
                LineParticle spark2 = new(Projectile.Center, -Projectile.velocity * 0.05f, false, 17, 1.7f, InnerColor);
                GeneralParticleHandler.SpawnParticle(spark2);
            }

            Time++;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Frostburn2, 180);
            target.AddBuff(ModContent.BuffType<GlacialState>(), 30);
        }

        public override Color? GetAlpha(Color lightColor) {
            return new Color(198, 197, 246);
        }

        public override void OnKill(int timeLeft) {
            if (timeLeft > 0) {
                Projectile.Explode(92, SoundID.Item27);
                for (int i = 0; i < 30; i++) {
                    int dustSpawns = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.DungeonWater, 0f, 0f, 0, default, Main.rand.NextFloat(1f, 2f));
                    Main.dust[dustSpawns].noGravity = true;
                    Main.dust[dustSpawns].velocity *= 4f;

                    Vector2 randVr = CWRUtils.GetRandomVevtor(-170, -10, Main.rand.Next(9, 32));
                    AltSparkParticle spark = new(Projectile.Center, randVr, true, 12, Main.rand.NextFloat(1.3f, 2.2f), Color.Blue);
                    GeneralParticleHandler.SpawnParticle(spark);
                    AltSparkParticle spark2 = new(Projectile.Center, randVr, false, 9, Main.rand.NextFloat(1.1f, 1.5f), Color.AntiqueWhite);
                    GeneralParticleHandler.SpawnParticle(spark2);
                }
                for (int j = 0; j < 20; ++j) {
                    int dustSpawns = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.BlueCrystalShard, 0f, 0f, 0, new Color(), 1.3f);
                    Main.dust[dustSpawns].noGravity = true;
                    Main.dust[dustSpawns].velocity *= 1.5f;
                }
            }
        }
    }
}
