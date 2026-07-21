using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.JusticeUnveileds
{
    /// <summary>环绕十字架</summary>
    internal class JusticeUnveiledCross : ModProjectile
    {
        public override string Texture => CWRConstant.Item_Accessorie + "JusticeUnveiled";
        [VaultLoaden(CWRConstant.Item_Accessorie + "JusticeUnveiled")]
        private static Asset<Texture2D> CrossTex = null;
        private int crossIndex;
        private float rotation;
        private float spawnProgress = 0f;
        private float pulsePhase = 0f;
        private int particleTimer = 0;

        public override void SetStaticDefaults() {
            Main.projFrames[Type] = 1;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = int.MaxValue;
        }

        public override void AI() {
            Player player = Main.player[Projectile.owner];
            if (!player.active || player.dead || !player.CWR().IsJusticeUnveiled) {
                SpawnDespawnEffect();
                Projectile.Kill();
                return;
            }

            crossIndex = (int)Projectile.ai[0];

            //出场前30帧
            if (spawnProgress < 1f) {
                spawnProgress += 0.033f;//≈30帧
                if (spawnProgress > 1f) spawnProgress = 1f;

                if (spawnProgress >= 0.98f && Projectile.owner == Main.myPlayer) {
                    SoundEngine.PlaySound(SoundID.Item29 with {
                        Volume = 0.4f,
                        Pitch = 0.3f + crossIndex * 0.1f
                    }, Projectile.Center);
                }
            }

            float appearEase = VaultUtils.EaseOutBack(spawnProgress);

            rotation += 0.06f + crossIndex * 0.01f;
            pulsePhase += 0.12f;

            float baseDistance = 60f;
            float distanceWave = (float)Math.Sin(Main.GameUpdateCount * 0.03f + crossIndex * MathHelper.PiOver2) * 5f;
            float distance = (baseDistance + distanceWave) * appearEase;

            float baseAngle = MathHelper.TwoPi / 5f * crossIndex;
            float angle = baseAngle + Main.GameUpdateCount * 0.02f;

            float verticalOffset = (float)Math.Sin(Main.GameUpdateCount * 0.04f + crossIndex * MathHelper.Pi) * 3f * appearEase;
            Vector2 targetPos = player.Center + angle.ToRotationVector2() * distance;
            targetPos.Y += verticalOffset;

            Projectile.Center = Vector2.Lerp(Projectile.Center, targetPos, 0.2f);

            particleTimer++;
            if (spawnProgress >= 1f && particleTimer % 8 == 0) {
                SpawnTrailParticles(player);
            }

            if (Main.rand.NextBool(15) && spawnProgress >= 1f) {
                SpawnAuraParticle();
            }

            if (Projectile.IsOwnedByLocalPlayer() && CWRKeySystem.Accessory_Skills.JustPressed && player.CWR().JusticeUnveiledCooldown <= 0) {
                if (player.CWR().JusticeUnveiledCharges > 0 && crossIndex == player.CWR().JusticeUnveiledCharges) {
                    NPC target = player.Center.FindClosestNPC(1200, false);
                    if (target != null) {
                        player.CWR().JusticeUnveiledCharges--;
                        if (player.CWR().JusticeUnveiledCharges < 0) {
                            player.CWR().JusticeUnveiledCharges = 0;
                        }
                        player.CWR().JusticeUnveiledCooldown = 2;

                        SpawnLaunchEffect(target.Center);

                        Projectile.Kill();
                        ShootState shootState = player.GetShootState();
                        Projectile.NewProjectile(player.FromObjectGetParent()
                            , target.Center + new Vector2(0, -1120), new Vector2(0, 6)
                            , ModContent.ProjectileType<DivineJustice>(), shootState.WeaponDamage, 2, player.whoAmI, target.whoAmI);
                        SoundEngine.PlaySound(SoundID.Item9 with { Volume = 0.6f }, player.Center);
                    }
                }
            }

            float lightPulse = (float)Math.Sin(pulsePhase) * 0.4f + 0.6f;
            float lightIntensity = lightPulse * appearEase;
            Lighting.AddLight(Projectile.Center,
                1.0f * lightIntensity,
                0.8f * lightIntensity,
                0.3f * lightIntensity);
        }

        private void SpawnTrailParticles(Player player) {
            Vector2 toPlayer = Projectile.Center.To(player.Center);
            float angle = toPlayer.ToRotation();

            for (int i = 0; i < 2; i++) {
                Vector2 particleVel = angle.ToRotationVector2().RotatedByRandom(0.3f) * Main.rand.NextFloat(1f, 3f);
                Dust trail = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                    DustID.GoldCoin,
                    particleVel,
                    0,
                    default,
                    Main.rand.NextFloat(0.6f, 1.0f)
                );
                trail.noGravity = true;
                trail.fadeIn = 0.6f;
            }
        }

        private void SpawnAuraParticle() {
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            Vector2 offset = angle.ToRotationVector2() * Main.rand.NextFloat(15f, 25f);
            Vector2 velocity = -offset.SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(1f, 2f);

            Dust aura = Dust.NewDustPerfect(
                Projectile.Center + offset,
                DustID.Electric,
                velocity,
                0,
                Color.Gold,
                Main.rand.NextFloat(0.8f, 1.2f)
            );
            aura.noGravity = true;
            aura.fadeIn = 0.8f;
        }

        private void SpawnLaunchEffect(Vector2 targetPos) {
            for (int i = 0; i < 20; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(8f, 8f);
                Dust launch = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.GoldCoin,
                    velocity,
                    0,
                    default,
                    Main.rand.NextFloat(1.2f, 1.8f)
                );
                launch.noGravity = true;
            }

            Vector2 toTarget = Projectile.Center.To(targetPos);
            float distance = toTarget.Length();
            int particleCount = (int)(distance / 30f);

            for (int i = 0; i < particleCount; i++) {
                float progress = i / (float)particleCount;
                Vector2 pos = Vector2.Lerp(Projectile.Center, targetPos, progress);

                Dust beam = Dust.NewDustPerfect(
                    pos,
                    DustID.Electric,
                    Vector2.Zero,
                    0,
                    Color.Gold,
                    Main.rand.NextFloat(0.8f, 1.2f)
                );
                beam.noGravity = true;
                beam.fadeIn = 1.0f;
            }
        }

        private void SpawnDespawnEffect() {
            if (VaultUtils.isServer) return;

            for (int i = 0; i < 12; i++) {
                float angle = MathHelper.TwoPi * i / 12f;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(2f, 5f);

                Dust despawn = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.GoldCoin,
                    velocity,
                    0,
                    default,
                    Main.rand.NextFloat(1.0f, 1.5f)
                );
                despawn.noGravity = true;
                despawn.fadeIn = 0.8f;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = CrossTex.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            float pulseScale = 1f + (float)Math.Sin(pulsePhase) * 0.15f;
            float baseScale = 0.5f * spawnProgress;
            float scale = baseScale * pulseScale;
            float alpha = spawnProgress * 0.9f;

            Color glowColor = Color.Gold * 0.6f * alpha;
            glowColor.A = 0;

            for (int i = 0; i < 3; i++) {
                float offset = i * 0.15f;
                float glowScale = scale * (1f + offset);
                float glowAlpha = (1f - offset * 0.5f) * alpha;

                Main.spriteBatch.Draw(
                    texture,
                    drawPos,
                    null,
                    glowColor * glowAlpha,
                    rotation + i * 0.2f,
                    texture.Size() / 2f,
                    glowScale,
                    SpriteEffects.None,
                    0
                );
            }

            float ringScale = scale * (1f + (float)Math.Sin(pulsePhase * 1.5f) * 0.2f);
            Color ringColor = Color.Lerp(Color.Gold, Color.Yellow, 0.5f) * alpha;
            ringColor.A = 0;

            Main.spriteBatch.Draw(
                texture,
                drawPos,
                null,
                ringColor * 0.7f,
                -rotation * 0.5f,
                texture.Size() / 2f,
                ringScale,
                SpriteEffects.None,
                0
            );

            Main.spriteBatch.Draw(
                texture,
                drawPos,
                null,
                Color.White * alpha,
                rotation,
                texture.Size() / 2f,
                scale * 0.85f,
                SpriteEffects.None,
                0
            );

            Color coreColor = Color.White with { A = 0 };
            float coreScale = scale * 0.3f * (1f + (float)Math.Sin(pulsePhase * 2f) * 0.3f);
            Main.spriteBatch.Draw(
                texture,
                drawPos,
                null,
                coreColor * alpha * 0.8f,
                rotation * 2f,
                texture.Size() / 2f,
                coreScale,
                SpriteEffects.None,
                0
            );

            return false;
        }
    }
}
