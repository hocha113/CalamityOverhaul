using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal class FrostBlizzardField : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Fog";
        private float pulsePhase;
        private float expansionProgress;
        private const int MaxRadius = 320;
        private const int ExpandDuration = 25;
        private const int SustainDuration = 140;
        private const int FadeDuration = 20;
        
        public override void SetDefaults() {
            Projectile.width = MaxRadius * 2;
            Projectile.height = MaxRadius * 2;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = ExpandDuration + SustainDuration + FadeDuration;
            Projectile.hide = true;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
            Projectile.ArmorPenetration = 15;
        }

        public override void AI() {
            int totalTime = ExpandDuration + SustainDuration + FadeDuration;
            int currentPhase = totalTime - Projectile.timeLeft;

            if (currentPhase < ExpandDuration) {
                expansionProgress = currentPhase / (float)ExpandDuration;
            }
            else if (currentPhase < ExpandDuration + SustainDuration) {
                expansionProgress = 1f;
            }
            else {
                int fadeTime = currentPhase - ExpandDuration - SustainDuration;
                expansionProgress = 1f - (fadeTime / (float)FadeDuration);
            }

            Projectile.scale = expansionProgress;
            pulsePhase += 0.1f;

            if (Main.rand.NextBool(2) && expansionProgress > 0.3f) {
                Vector2 randomPos = Projectile.Center + Main.rand.NextVector2Circular(MaxRadius * expansionProgress, MaxRadius * expansionProgress);
                Dust snow = Dust.NewDustPerfect(randomPos, DustID.SnowflakeIce
                    , new Vector2(Main.rand.NextFloat(-2f, 2f), -Main.rand.NextFloat(1f, 4f)), 0, default, Main.rand.NextFloat(2f, 3.5f));
                snow.noGravity = true;
            }

            if (Main.rand.NextBool(3) && expansionProgress > 0.5f) {
                Vector2 randomPos = Projectile.Center + Main.rand.NextVector2Circular(MaxRadius * expansionProgress * 0.8f, MaxRadius * expansionProgress * 0.8f);
                Dust frost = Dust.NewDustPerfect(randomPos, DustID.IceTorch
                    , Main.rand.NextVector2Circular(3f, 3f), 0, new Color(200, 230, 255), Main.rand.NextFloat(1.5f, 2.5f));
                frost.noGravity = true;
            }

            float pulse = (float)Math.Sin(pulsePhase) * 0.3f + 0.7f;
            float lightRadius = MaxRadius * expansionProgress * 0.5f;
            Lighting.AddLight(Projectile.Center, 0.4f * pulse * expansionProgress, 0.7f * pulse * expansionProgress, 1.0f * pulse * expansionProgress);

            if (currentPhase == ExpandDuration) {
                SoundEngine.PlaySound(SoundID.Item30 with { Volume = 0.6f, Pitch = -0.4f }, Projectile.Center);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Frostburn2, 300);
            if (Main.rand.NextBool(3)) {
                target.AddBuff(BuffID.Chilled, 180);
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float currentRadius = MaxRadius * expansionProgress;
            return VaultUtils.CircleIntersectsRectangle(Projectile.Center, currentRadius, targetHitbox);
        }

        public override bool? CanDamage() {
            int currentPhase = (ExpandDuration + SustainDuration + FadeDuration) - Projectile.timeLeft;
            if (currentPhase < ExpandDuration) {
                return false;
            }
            if (currentPhase >= ExpandDuration + SustainDuration) {
                return false;
            }
            return null;
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 80; i++) {
                Vector2 randomVelocity = Main.rand.NextVector2Circular(8f, 8f);
                Dust frost = Dust.NewDustPerfect(Projectile.Center, DustID.IceTorch, randomVelocity, 0
                    , new Color(200, 230, 255), Main.rand.NextFloat(2f, 3.5f));
                frost.noGravity = true;
            }

            for (int i = 0; i < 60; i++) {
                Dust snow = Dust.NewDustPerfect(Projectile.Center, DustID.SnowflakeIce
                    , Main.rand.NextVector2Circular(7f, 7f), 0, default, Main.rand.NextFloat(2.5f, 4f));
                snow.noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.5f, Pitch = -0.5f }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (expansionProgress < 0.01f) {
                return false;
            }

            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 drawPosition = Projectile.Center - Main.screenPosition;
            Vector2 origin = texture.Size() * 0.5f;
            float alpha = expansionProgress * 0.9f;

            if (expansionProgress > 0.85f) {
                alpha = 1f - ((expansionProgress - 0.85f) / 0.15f) * 0.3f;
            }

            Vector2 scale = new Vector2(MaxRadius * 2, MaxRadius * 2) / texture.Size() * expansionProgress;

            Color drawColor = new Color(150, 210, 240) * alpha * 0.8f;
            Main.EntitySpriteDraw(texture, drawPosition, null, drawColor, pulsePhase * 0.5f, origin, scale, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(texture, drawPosition, null, drawColor, -pulsePhase * 0.5f, origin, scale, SpriteEffects.None, 0);

            Color innerColor = new Color(180, 230, 255) * alpha * 0.6f;
            Main.EntitySpriteDraw(texture, drawPosition, null, innerColor, pulsePhase * 0.8f, origin, scale * 0.8f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(texture, drawPosition, null, innerColor, -pulsePhase * 0.8f, origin, scale * 0.8f, SpriteEffects.None, 0);

            float pulse = (float)Math.Sin(pulsePhase * 1.5f) * 0.3f + 0.7f;
            Color coreColor = new Color(200, 240, 255) * alpha * pulse * 0.5f;
            Main.EntitySpriteDraw(texture, drawPosition, null, coreColor, pulsePhase, origin, scale * 0.5f, SpriteEffects.None, 0);

            return false;
        }
    }
}
