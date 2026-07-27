using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend
{
    /// <summary>海洋洪流弹幕；只管理攻击状态和水流核心，离散液体由独立 PRT 管理</summary>
    internal class OceanCurrent : ModProjectile, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private ref float StreamLife => ref Projectile.ai[1];

        private readonly List<Vector2> coreTrail = new();
        private const int MaxCoreTrail = 20;
        private const float Gravity = 0.24f;
        private const float BuoyancyForce = -0.05f;

        private float glowPulse;
        private float wavePhase;
        private int spentTimer;
        private int trueDmg;
        private bool terminalSplashSpawned;

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.alpha = 0;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 420;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 6;
            Projectile.arrow = true;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (target.IsWormBody()) {
                modifiers.FinalDamage *= 0.75f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            OceanCurrentVFX.SplashBurst(Projectile.Center, Projectile.velocity, 0.72f);
            target.AddBuff(BuffID.Wet, 300);

            SoundEngine.PlaySound(SoundID.Item85 with {
                Volume = 0.6f,
                Pitch = 0.1f
            }, Projectile.Center);

            Projectile.damage = (int)(Projectile.damage * 0.66f);
        }

        public override void AI() {
            StreamLife++;
            wavePhase = MathHelper.WrapAngle(wavePhase + 0.15f);
            glowPulse = MathF.Sin(StreamLife * 0.2f) * 0.2f + 0.8f;

            if (Projectile.numHits > 2) {
                UpdateSpentAttack();
            }
            else {
                UpdateStreamingAttack();
            }

            UpdateCoreTrail();
            AddWaterLight();

            if (StreamLife % 35 == 0 && Projectile.numHits <= 2) {
                SoundEngine.PlaySound(SoundID.Splash with {
                    Volume = 0.3f,
                    Pitch = Main.rand.NextFloat(-0.3f, 0.1f)
                }, Projectile.Center);
            }
        }

        private void UpdateStreamingAttack() {
            Projectile.velocity.Y += Gravity * 0.5f + BuoyancyForce;
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            OceanCurrentVFX.EmitStream(Projectile.Center, Projectile.velocity, (int)StreamLife, wavePhase);

            if (StreamLife > 180 || Projectile.velocity.LengthSquared() < 1.5f * 1.5f) {
                Projectile.Kill();
            }
        }

        private void UpdateSpentAttack() {
            Projectile.velocity *= 0.5f;
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            if (Projectile.damage > 0) {
                trueDmg = Projectile.damage;
            }
            Projectile.damage = 0;

            OceanCurrentVFX.EmitSpent(Projectile.Center, Projectile.velocity, spentTimer);
            if (++spentTimer > 60) {
                Projectile.Kill();
            }
        }

        private void UpdateCoreTrail() {
            coreTrail.Insert(0, Projectile.Center);
            if (coreTrail.Count > MaxCoreTrail) {
                coreTrail.RemoveAt(coreTrail.Count - 1);
            }
        }

        private void AddWaterLight() {
            float lightIntensity = MathHelper.Lerp(0.4f, 0.9f, glowPulse);
            Lighting.AddLight(Projectile.Center,
                0.3f * lightIntensity,
                0.7f * lightIntensity,
                1.2f * lightIntensity);
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            OceanCurrentVFX.SplashBurst(Projectile.Center, oldVelocity, 1.08f);
            terminalSplashSpawned = true;

            SoundEngine.PlaySound(SoundID.Splash with {
                Volume = 0.7f,
                Pitch = -0.1f
            }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item96 with {
                Volume = 0.4f,
                Pitch = -0.4f
            }, Projectile.Center);

            Projectile.Kill();
            return false;
        }

        public override bool PreDraw(ref Color lightColor) => false;

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            if (StreamLife < 6 || Projectile.numHits > 2) {
                return;
            }
            DrawStreamCore(spriteBatch);
        }

        private void DrawStreamCore(SpriteBatch spriteBatch) {
            if (coreTrail.Count < 2) {
                return;
            }

            Texture2D coreTexture = CWRAsset.LightShot.Value;
            for (int i = 0; i < coreTrail.Count - 1; i++) {
                float progress = 1f - i / (float)coreTrail.Count;
                Vector2 drawPosition = coreTrail[i] - Main.screenPosition;
                Vector2 segment = coreTrail[i + 1] - coreTrail[i];
                float rotation = segment.ToRotation();
                Color coreColor = Color.Lerp(
                    OceanCurrentVFX.WaterBright,
                    OceanCurrentVFX.ShallowOcean,
                    progress
                ) * progress * glowPulse * 0.82f;
                float scale = progress * 0.12f;

                spriteBatch.Draw(coreTexture, drawPosition, null, coreColor, rotation
                    , coreTexture.Size() * 0.5f, new Vector2(scale * 3f, scale * 1.15f)
                    , SpriteEffects.None, 0f);
                spriteBatch.Draw(coreTexture, drawPosition, null
                    , OceanCurrentVFX.DeepOcean * (progress * glowPulse * 0.48f), rotation
                    , coreTexture.Size() * 0.5f, new Vector2(scale * 5f, scale * 2.35f)
                    , SpriteEffects.None, 0f);
            }

            Vector2 headPosition = Projectile.Center - Main.screenPosition;
            spriteBatch.Draw(coreTexture, headPosition, null
                , OceanCurrentVFX.WaterBright * glowPulse, Projectile.velocity.ToRotation()
                , coreTexture.Size() * 0.5f, new Vector2(0.18f, 0.14f)
                , SpriteEffects.None, 0f);

            Texture2D starTexture = CWRAsset.StarTexture_White.Value;
            float pulseScale = 0.1f + MathF.Sin(wavePhase) * 0.02f;
            spriteBatch.Draw(starTexture, headPosition, null
                , OceanCurrentVFX.OceanFoam * (glowPulse * 0.72f), StreamLife * 0.08f
                , starTexture.Size() * 0.5f, pulseScale, SpriteEffects.None, 0f);
        }

        public override void OnKill(int timeLeft) {
            if (!terminalSplashSpawned) {
                Vector2 releaseVelocity = Projectile.velocity.LengthSquared() > 0.25f
                    ? Projectile.velocity
                    : -Vector2.UnitY * 4f;
                OceanCurrentVFX.SplashBurst(Projectile.Center, releaseVelocity * 0.72f, 0.9f);
            }

            Projectile.damage = trueDmg / 2;
            Projectile.Explode(100, default, false);
        }
    }
}