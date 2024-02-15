using CalamityMod;
using CalamityMod.Buffs.DamageOverTime;
using CalamityMod.Graphics.Metaballs;
using CalamityMod.Particles;
using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee
{
    internal class StreamBeams : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Melee + "StreamGouge";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
        }

        public override void SetDefaults() {
            Projectile.width = 28;
            Projectile.height = 28;
            Projectile.scale = 1f;
            Projectile.alpha = 150;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = false;
            Projectile.penetrate = -1;
            Projectile.MaxUpdates = 3;
            Projectile.timeLeft = 180 * Projectile.MaxUpdates;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = Projectile.MaxUpdates;
        }

        public override void OnSpawn(IEntitySource source) {
            base.OnSpawn(source);
        }

        public override void OnKill(int timeLeft) {
            base.OnKill(timeLeft);
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            Projectile.alpha += 5;
            if (Projectile.alpha > 255)
                Projectile.alpha = 255;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<GodSlayerInferno>(), 300);
            SoundEngine.PlaySound(in SoundID.Item74, target.Center);

            if (Projectile.numHits == 0) {
                float randRot = Main.rand.NextFloat(MathHelper.TwoPi);
                for (int i = 0; i < 6; i++) {
                    Vector2 vr = (MathHelper.TwoPi / 6 * i + randRot).ToRotationVector2() * 15;
                    Projectile.NewProjectile(
                        Projectile.parent(),
                        target.Center,// + vr.UnitVector() * 164
                        vr,
                        ModContent.ProjectileType<GodKillers>(),
                        Projectile.damage / 2,
                        0,
                        Projectile.owner
                        );
                }
            }

            StarRT(Projectile, target);
        }

        public static void StarRT(Projectile projectile, Entity target) {
            if (Main.netMode != NetmodeID.Server) {
                Color color = Color.Lerp(Color.Cyan, Color.White, Main.rand.NextFloat(0.3f, 0.64f));
                GeneralParticleHandler.SpawnParticle(new ImpactParticle(Vector2.Lerp(projectile.Center, target.Center, 0.65f), 0.1f, 20, Main.rand.NextFloat(0.4f, 0.5f), color));
                for (int i = 0; i < 20; i++) {
                    Vector2 spawnPosition = target.Center + Main.rand.NextVector2Circular(30f, 30f);
                    StreamGougeMetaball.SpawnParticle(spawnPosition, Main.rand.NextVector2Circular(3f, 3f), 60f);

                    float scale = MathHelper.Lerp(24f, 64f, CalamityUtils.Convert01To010(i / 19f));
                    spawnPosition = target.Center + projectile.velocity.SafeNormalize(Vector2.UnitY) * MathHelper.Lerp(-40f, 90f, i / 19f);
                    Vector2 particleVelocity = projectile.velocity.SafeNormalize(Vector2.UnitY).RotatedByRandom(0.23f) * Main.rand.NextFloat(2.5f, 9f);
                    StreamGougeMetaball.SpawnParticle(spawnPosition, particleVelocity, scale);
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = CWRUtils.GetT2DValue(Texture);
            float alp = Projectile.alpha / 255f;

            if (Projectile.alpha > 225) {
                for (int i = 0; i < Projectile.oldPos.Length; i++) {
                    Main.EntitySpriteDraw(
                        texture,
                        CWRUtils.WDEpos(Projectile.oldPos[i] + Projectile.Center - Projectile.position),
                        null,
                        Color.White * alp * 0.5f,
                        Projectile.rotation + MathHelper.PiOver4,
                        CWRUtils.GetOrig(texture),
                        Projectile.scale - 0.05f * i,
                        SpriteEffects.None,
                        0
                    );
                }
            }

            Main.EntitySpriteDraw(
                texture,
                CWRUtils.WDEpos(Projectile.Center),
                null,
                Color.White * alp,
                Projectile.rotation + MathHelper.PiOver4,
                CWRUtils.GetOrig(texture),
                Projectile.scale,
                SpriteEffects.None,
                0
                );
            return false;
        }
    }
}
