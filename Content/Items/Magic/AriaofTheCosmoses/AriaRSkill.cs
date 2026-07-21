using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.AriaofTheCosmoses
{
    /// R 伽马暴，吸光→白闪→扇形点射(每3帧一道×9)
    internal class AriaRSkill : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int ChargeTime = 60;
        private const int BeamCount = 9;
        private const int BeamInterval = 3;
        private const int FireWindow = BeamCount * BeamInterval + GammaRayBeam.TotalLife;
        private const float SpreadDeg = 44f;

        private int beamsFired;
        private float chargeProgress;

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = ChargeTime + FireWindow;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.alpha = 255;
        }

        public override void AI() {
            Player player = Main.player[Projectile.owner];
            if (!player.active || player.dead) {
                Projectile.Kill();
                return;
            }

            Projectile.Center = player.Center;

            if (Projectile.timeLeft > FireWindow) {
                ChargePhase(player);
            }
            else {
                FirePhase(player);
            }
        }

        private void ChargePhase(Player player) {
            int currentTime = ChargeTime - (Projectile.timeLeft - FireWindow);
            chargeProgress = MathHelper.Clamp(currentTime / (float)ChargeTime, 0f, 1f);

            if (currentTime == 1) {
                SoundEngine.PlaySound(SoundID.DD2_DarkMageHealImpact with { Volume = 0.8f, Pitch = -0.3f }, Projectile.Center);
            }
            else if (currentTime == ChargeTime / 2) {
                SoundEngine.PlaySound(SoundID.DD2_WitherBeastAuraPulse with { Volume = 0.9f, Pitch = 0.1f }, Projectile.Center);
            }

            if (!VaultUtils.isServer && currentTime % 2 == 0) {
                int count = (int)(2 + chargeProgress * 4);
                for (int i = 0; i < count; i++) {
                    Vector2 pos = player.Center + Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(90f, 200f);
                    PRTLoader.NewParticle<PRT_Spark>(pos,
                        (player.Center - pos).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(6f, 12f) * (0.5f + chargeProgress),
                        Color.Lerp(GammaRayBeam.ColViolet, GammaRayBeam.ColCore, chargeProgress), Main.rand.NextFloat(0.6f, 1.1f))
                        ?.Configure(false, Main.rand.Next(10, 18), player);
                }

                //蓄力后半程:身周电离弧
                if (chargeProgress > 0.5f && Main.rand.NextBool(2)) {
                    PRTLoader.NewParticle<PRT_GammaIonize>(player.Center + Main.rand.NextVector2Circular(40f, 40f),
                        Main.rand.NextVector2Circular(2f, 2f),
                        GammaRayBeam.ColCheren, Main.rand.NextFloat(0.4f, 0.7f))
                        ?.Configure(Main.rand.Next(12, 20), Main.rand.NextFloat(MathHelper.TwoPi));
                }
            }

            if (chargeProgress > 0.5f) {
                player.CWR().GetScreenShake(chargeProgress * 3f);
            }

            Lighting.AddLight(Projectile.Center, GammaRayBeam.ColViolet.ToVector3() * chargeProgress * 1.4f);
        }

        private void FirePhase(Player player) {
            int fireTime = FireWindow - Projectile.timeLeft;

            if (fireTime == 0) {
                SoundEngine.PlaySound(SoundID.Item109 with { Volume = 1.1f, Pitch = 0.4f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.DD2_ExplosiveTrapExplode with { Volume = 1f, Pitch = 0.2f }, Projectile.Center);
                player.CWR().GetScreenShake(13f);
                if (CWRServerConfig.Instance.LensEasing) {
                    Main.SetCameraLerp(0.12f, 40);
                }

                if (!VaultUtils.isServer) {
                    for (int i = 0; i < 40; i++) {
                        PRTLoader.NewParticle<PRT_Light>(player.Center, Main.rand.NextVector2Circular(22f, 22f),
                            Color.Lerp(GammaRayBeam.ColCore, GammaRayBeam.ColViolet, Main.rand.NextFloat()),
                            Main.rand.NextFloat(0.7f, 1.4f))
                            ?.Configure(Main.rand.Next(18, 30), opacity: 1.6f, squishStrenght: 2.2f, hueShift: 0.02f);
                    }
                }
            }

            if (fireTime % BeamInterval == 0 && beamsFired < BeamCount && Projectile.IsOwnedByLocalPlayer()) {
                Vector2 mouseDir = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.UnitX);
                float sweep01 = beamsFired / (float)(BeamCount - 1);
                float angleOffset = MathHelper.ToRadians(MathHelper.Lerp(-SpreadDeg * 0.5f, SpreadDeg * 0.5f, sweep01));

                Projectile.NewProjectile(Projectile.GetSource_FromThis(), player.Center,
                    mouseDir.RotatedBy(angleOffset) * 4f,
                    ModContent.ProjectileType<GammaRayBeam>(),
                    (int)(Projectile.damage * 1.2f), Projectile.knockBack * 1.5f, Projectile.owner,
                    1f, beamsFired * 0.113f);

                beamsFired++;
                player.CWR().GetScreenShake(3.5f);
            }

            Lighting.AddLight(Projectile.Center, GammaRayBeam.ColViolet.ToVector3() * 1.1f);
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item62 with { Volume = 0.6f, Pitch = 0.5f }, Projectile.Center);

            //余韵电离弧
            for (int i = 0; i < 16; i++) {
                float ang = MathHelper.TwoPi * i / 16f;
                PRTLoader.NewParticle<PRT_GammaIonize>(Projectile.Center, ang.ToRotationVector2() * Main.rand.NextFloat(3f, 8f),
                    Color.Lerp(GammaRayBeam.ColViolet, GammaRayBeam.ColCheren, Main.rand.NextFloat()), Main.rand.NextFloat(0.4f, 0.8f))
                    ?.Configure(Main.rand.Next(12, 24), Main.rand.NextFloat(MathHelper.TwoPi));
            }
        }
    }
}
