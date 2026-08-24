using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.JusticeUnveileds
{
    internal class DivineJustice : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        private bool spawn;
        private readonly List<LightningBolt> lightningBolts = new();
        private float chargeIntensity = 0f;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 64;
            Projectile.timeLeft = 190;
            Projectile.extraUpdates = 12;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = false;
            Projectile.penetrate = 1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override bool? CanHitNPC(NPC target) {
            if (((int)Projectile.ai[0]).TryGetNPC(out var _) && target.whoAmI == Projectile.ai[0]) {
                return true;
            }
            return false;
        }

        public override void AI() {
            if (!spawn) {
                if (Projectile.ai[0].TryGetNPC(out var target)) {
                    JusticeUnveiled.SpawnCrossMarker(target, Projectile.owner);
                }

                spawn = true;
            }
            //蓄能
            if (Projectile.timeLeft > 160) {
                float progress = (190 - Projectile.timeLeft) / 30f;
                chargeIntensity = MathHelper.Lerp(0f, 1f, VaultUtils.EaseOutCubic(progress));

                //充能粒
                if (Main.rand.NextBool(3)) {
                    SpawnChargeParticle();
                }

                //闪电
                if (Main.rand.NextBool(8)) {
                    SpawnChargeLightning();
                }
            }

            //金光粒
            PRTLoader.NewParticle<PRT_Spark>(Projectile.Center, new Vector2(0, 2), Color.Lerp(Color.Gold, Color.Yellow, chargeIntensity), 1.2f).Configure(false, 22);

            for (int i = lightningBolts.Count - 1; i >= 0; i--) {
                lightningBolts[i].Update();
                if (lightningBolts[i].IsExpired()) {
                    lightningBolts.RemoveAt(i);
                }
            }

            if (Projectile.timeLeft == 160) {
                SoundEngine.PlaySound(SoundID.DD2_LightningAuraZap with {
                    Pitch = -0.3f,
                    Volume = 0.5f
                }, Projectile.Center);
            }

            //震屏预警
            if (Projectile.timeLeft < 20 && Projectile.timeLeft > 10) {
                if (CWRClientConfig.Instance.ScreenVibration) {
                    Main.instance.CameraModifiers.Add(new PunchCameraModifier(
                        Projectile.Center,
                        Main.rand.NextVector2Unit(),
                        1.5f * chargeIntensity,
                        6f,
                        5,
                        800f,
                        FullName
                    ));
                }
            }

            //照明
            Lighting.AddLight(Projectile.Center,
                1.2f * chargeIntensity,
                1f * chargeIntensity,
                0.2f * chargeIntensity);
        }

        private void SpawnChargeParticle() {
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            float distance = Main.rand.NextFloat(60f, 120f);
            Vector2 spawnPos = Projectile.Center + angle.ToRotationVector2() * distance;
            Vector2 velocity = (Projectile.Center - spawnPos).SafeNormalize(Vector2.Zero) *
                               Main.rand.NextFloat(3f, 6f) * (1f + chargeIntensity);

            PRTLoader.NewParticle<PRT_Light>(spawnPos, velocity,
                Color.Lerp(Color.Gold, Color.OrangeRed, Main.rand.NextFloat()),
                Main.rand.NextFloat(0.5f, 1f)).Configure(20, 1, 1.2f, hueShift: 0.0f);
        }

        private void SpawnChargeLightning() {
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            Vector2 direction = angle.ToRotationVector2();
            lightningBolts.Add(new LightningBolt(
                Projectile.Center,
                direction,
                Main.rand.Next(80, 140),
                15
            ));
        }

        public override void OnKill(int timeLeft) {
            Projectile.NewProjectile(Projectile.FromObjectGetParent(), Projectile.Center, Vector2.Zero
            , ModContent.ProjectileType<JusticeUnveiledExplode>(), Projectile.damage, 2, Projectile.owner, Projectile.ai[0]);
        }

        public override bool PreDraw(ref Color lightColor) {

            foreach (var bolt in lightningBolts) {
                bolt.Draw(Main.spriteBatch);
            }

            //充能光环
            DrawChargeAura();

            return false;
        }

        private void DrawChargeAura() {
            if (chargeIntensity <= 0.1f) return;

            Texture2D glowTex = CWRAsset.StarTexture.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            //多层光晕
            for (int i = 0; i < 2; i++) {
                float scale = (1f + i * 0.2f) * chargeIntensity * 0.6f;
                float alpha = (1f - i * 0.3f) * chargeIntensity * 0.5f;
                Color color = Color.Lerp(Color.Gold, Color.OrangeRed, i / 2f) * alpha;
                color.A = 0;

                Main.spriteBatch.Draw(
                    glowTex,
                    drawPos,
                    null,
                    color,
                    Main.GlobalTimeWrappedHourly * 2f + i,
                    glowTex.Size() / 2f,
                    scale,
                    SpriteEffects.None,
                    0f
                );
            }
        }
    }
}
