﻿using CalamityMod.Particles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee
{
    internal class ExcelsusBomb : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Melee + "StreamGouge";

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.tileCollide = true;
            Projectile.penetrate = 1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.ignoreWater = true;
            Projectile.extraUpdates = 3;
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 16;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            SpanDust();
            Projectile.ai[0]++;
        }

        public void SpanDust() {
            for (int i = 0; i < 1; i++) {
                int dustType = Main.rand.NextBool(3) ? 56 : 242;
                if (Main.rand.NextBool()) {
                    Vector2 vector3 = Vector2.UnitY.RotatedByRandom(6.2831854820251465);
                    Dust obj3 = Main.dust[Dust.NewDust(Projectile.Center - vector3 * 30f, 0, 0, dustType)];
                    obj3.noGravity = true;
                    obj3.position = Projectile.Center - vector3 * Main.rand.Next(10, 21);
                    obj3.velocity = vector3.RotatedBy(1.5707963705062866) * 6f;
                    obj3.scale = 0.9f + Main.rand.NextFloat();
                    obj3.fadeIn = 0.5f;
                    obj3.customData = Projectile;
                    vector3 = Vector2.UnitY.RotatedByRandom(6.2831854820251465);
                    obj3.noGravity = true;
                    obj3.position = Projectile.Center - vector3 * Main.rand.Next(10, 21);
                    obj3.velocity = vector3.RotatedBy(1.5707963705062866) * 6f;
                    obj3.scale = 0.9f + Main.rand.NextFloat();
                    obj3.fadeIn = 0.5f;
                    obj3.customData = Projectile;
                    obj3.color = Color.Crimson;
                }
                else {
                    Vector2 vector4 = Vector2.UnitY.RotatedByRandom(6.2831854820251465);
                    Dust obj4 = Main.dust[Dust.NewDust(Projectile.Center - vector4 * 30f, 0, 0, dustType)];
                    obj4.noGravity = true;
                    obj4.position = Projectile.Center - vector4 * Main.rand.Next(20, 31);
                    obj4.velocity = vector4.RotatedBy(-1.5707963705062866) * 5f;
                    obj4.scale = 0.9f + Main.rand.NextFloat();
                    obj4.fadeIn = 0.5f;
                    obj4.customData = Projectile;
                }
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            Lighting.AddLight(Projectile.position, Color.Blue.ToVector3());
        }

        public override void OnKill(int timeLeft) {
            Projectile.Explode(300);

            Lighting.AddLight(Projectile.position, Color.Blue.ToVector3() * 3);

            for (int j = 0; j < 3; j++) {
                int dustType = Main.rand.NextBool(3) ? 56 : 242;
                float scale = Main.rand.NextFloat(1f, 1.35f);
                for (float spikeAngle = 0f; spikeAngle < MathHelper.TwoPi; spikeAngle += 0.15f) {
                    Vector2 offset = spikeAngle.ToRotationVector2() * Main.rand.NextFloat(3.95f, 7.05f);
                    Dust dust = Dust.NewDustPerfect(Projectile.Center
                        + VaultUtils.RandVrInAngleRange(0, 360, Main.rand.Next(16, 220))
                        , dustType, offset, 0, default, scale);

                    dust.customData = 0.025f;
                    dust.scale *= dustType == 56 ? 0.5f : 1;
                }

                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 10; i++)//生成这种粒子不是好主意
                    {
                        Vector2 particleSpeed = VaultUtils.RandVrInAngleRange(60, 120, -8 * (i / 20f));
                        Vector2 pos = Projectile.Center + new Vector2(Main.rand.Next(-16, 6), Main.rand.Next(0, 76)) + new Vector2(Main.rand.Next(-166, 166), 0);
                        Particle energyLeak = new SquishyLightParticle(pos, particleSpeed
                            , Main.rand.NextFloat(0.6f, 1.1f), Color.Purple, 60, 1, 1.5f, hueShift: 0.0f);
                        GeneralParticleHandler.SpawnParticle(energyLeak);
                    }
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D mainValue = TextureAssets.Projectile[Type].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 orig = mainValue.GetOrig();
            float rot = Projectile.rotation + MathHelper.PiOver4;
            for (int k = 0; k < Projectile.oldPos.Length; k++) {
                Vector2 offsetPos = Projectile.oldPos[k].To(Projectile.position);
                Vector2 drawPos2 = drawPos - offsetPos;
                Color color = Projectile.GetAlpha(Color.Pink) * ((Projectile.oldPos.Length - k) / (float)Projectile.oldPos.Length);
                Main.EntitySpriteDraw(mainValue, drawPos2, null, color, rot, orig, Projectile.scale, SpriteEffects.None, 0);
            }
            VaultUtils.DrawRotatingMarginEffect(Main.spriteBatch, mainValue, (int)Projectile.ai[0], drawPos, null, Color.Pink, rot, orig, Projectile.scale, 0);
            Main.EntitySpriteDraw(mainValue, drawPos, null, Color.White, rot, orig, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
