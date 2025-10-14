using InnoVault.GameContent.BaseEntity;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    internal class FishHarpy : FishSkill
    {
        public override int UnlockFishID => ItemID.Harpyfish;
        public override int DefaultCooldown => 30 - HalibutData.GetDomainLayer() * 2;

        //羽毛管理系统
        public static List<int> ActiveFeathers = new();
        private static int MaxFeathers => 5 + HalibutData.GetDomainLayer();

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {

            if (Cooldown <= 0) {
                SetCooldown();
                CleanupInactiveFeathers();

                if (ActiveFeathers.Count < MaxFeathers) {
                    int featherProj = Projectile.NewProjectile(
                        source,
                        player.Center,
                        Vector2.Zero,
                        ModContent.ProjectileType<HarpyFeatherOrbit>(),
                        (int)(damage * (2 + HalibutData.GetDomainLayer() * 0.5)),
                        knockback * 0.25f,
                        player.whoAmI,
                        ai0: ActiveFeathers.Count
                    );

                    if (featherProj >= 0 && featherProj < Main.maxProjectiles) {
                        ActiveFeathers.Add(featherProj);
                        SpawnSummonEffect(player.Center);

                        SoundEngine.PlaySound(SoundID.Item32 with {
                            Volume = 0.5f,
                            Pitch = 0.2f + ActiveFeathers.Count * 0.04f
                        }, player.Center);

                        if (ActiveFeathers.Count >= MaxFeathers) {
                            NotifyFeathersToLaunch(player);
                        }
                    }
                }
            }

            return null;
        }

        private static void CleanupInactiveFeathers() {
            ActiveFeathers.RemoveAll(id => {
                if (!id.TryGetProjectile(out var proj)) return true;
                if (proj.type != ModContent.ProjectileType<HarpyFeatherOrbit>()) return true;
                if (proj.ai[1] >= 4) return true;
                return false;
            });
        }

        private void NotifyFeathersToLaunch(Player player) {
            SoundEngine.PlaySound(SoundID.Item30 with {
                Volume = 0.7f,
                Pitch = 0.6f
            }, player.Center);

            SoundEngine.PlaySound(SoundID.DD2_WitherBeastAuraPulse with {
                Volume = 0.5f,
                Pitch = 0.8f
            }, player.Center);

            for (int i = 0; i < 30; i++) {
                float angle = MathHelper.TwoPi * i / 30f;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(4f, 8f);

                Dust charge = Dust.NewDustPerfect(
                    player.Center,
                    DustID.Cloud,
                    velocity,
                    100,
                    new Color(255, 255, 255),
                    Main.rand.NextFloat(1.5f, 2.5f)
                );
                charge.noGravity = true;
                charge.fadeIn = 1.3f;
            }
        }

        private void SpawnSummonEffect(Vector2 position) {
            for (int i = 0; i < 18; i++) {
                float angle = MathHelper.TwoPi * i / 18f;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(1.5f, 4f);

                Dust feather = Dust.NewDustPerfect(
                    position,
                    DustID.Cloud,
                    velocity,
                    100,
                    new Color(240, 240, 255),
                    Main.rand.NextFloat(1.2f, 1.8f)
                );
                feather.noGravity = true;
                feather.fadeIn = 1.1f;
            }

            for (int i = 0; i < 6; i++) {
                Dust air = Dust.NewDustDirect(
                    position - new Vector2(15),
                    30, 30,
                    DustID.Cloud,
                    Scale: Main.rand.NextFloat(1.5f, 2.2f)
                );
                air.velocity = Main.rand.NextVector2Circular(2f, 2f);
                air.noGravity = true;
                air.alpha = 120;
            }
        }
    }

    internal class HarpyFeatherOrbit : BaseHeldProj
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.HarpyFeather;

        private enum FeatherState
        {
            Gathering,
            Floating,
            Orbiting,
            Charging,
            Launching
        }

        private FeatherState State {
            get => (FeatherState)Projectile.ai[1];
            set => Projectile.ai[1] = (float)value;
        }

        private ref float StateTimer => ref Projectile.localAI[0];
        private ref float GlobalOrbitAngle => ref Projectile.localAI[1];

        private const float orbitRadius = 140f;
        private float orbitSpeed = 0.03f;
        private const float MaxOrbitSpeed = 0.15f;

        private float floatPhase = 0f;
        private const float floatAmplitude = 8f;
        private const float floatFrequency = 0.08f;

        private const int GatherDuration = 25;
        private const int FloatDuration = 35;
        private const int ChargeDuration = 30;
        private const float LaunchSpeed = 22f;

        private float glowIntensity = 0f;
        private float swayAngle = 0f;

        private int launchCountdown = 0;
        private const int LaunchDelay = 20;

        [VaultLoaden(CWRConstant.Masking)]
        private static Asset<Texture2D> SoftGlow = null;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 600;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;

            floatPhase = Main.rand.NextFloat(MathHelper.TwoPi);
        }

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }

            bool skillActive = FishSkill.GetT<FishHarpy>().Active(Owner);

            if (!skillActive && State != FeatherState.Launching) {
                if (State == FeatherState.Orbiting || State == FeatherState.Charging) {
                    LaunchFeather(Owner);
                }
                else {
                    Projectile.Kill();
                }
                return;
            }

            StateTimer++;

            if (State == FeatherState.Orbiting) {
                int totalFeathers = GetActiveFeatherCount(Owner);
                int maxFeathers = 5 + HalibutData.GetDomainLayer();

                if (totalFeathers >= maxFeathers && StateTimer >= 30) {
                    SyncAllFeathersToCharging(Owner);
                }
            }

            switch (State) {
                case FeatherState.Gathering:
                    GatheringPhaseAI(Owner);
                    break;

                case FeatherState.Floating:
                    FloatingPhaseAI(Owner);
                    break;

                case FeatherState.Orbiting:
                    OrbitingPhaseAI(Owner);
                    break;

                case FeatherState.Charging:
                    ChargingPhaseAI(Owner);
                    break;

                case FeatherState.Launching:
                    LaunchingPhaseAI();
                    break;
            }

            if (State != FeatherState.Launching) {
                swayAngle = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3f + floatPhase) * 0.15f;
            }

            float lightIntensity = glowIntensity * 0.4f;
            Lighting.AddLight(Projectile.Center,
                0.9f * lightIntensity,
                0.9f * lightIntensity,
                1.0f * lightIntensity);
        }

        private int GetActiveFeatherCount(Player owner) {
            int count = 0;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                if (Main.projectile[i].active &&
                    Main.projectile[i].type == Projectile.type &&
                    Main.projectile[i].owner == owner.whoAmI &&
                    Main.projectile[i].ai[1] < 4) {
                    count++;
                }
            }
            return count;
        }

        private void SyncAllFeathersToCharging(Player owner) {
            for (int i = 0; i < Main.maxProjectiles; i++) {
                if (Main.projectile[i].active &&
                    Main.projectile[i].type == Projectile.type &&
                    Main.projectile[i].owner == owner.whoAmI &&
                    Main.projectile[i].ai[1] < 4) {

                    Main.projectile[i].ai[1] = (float)FeatherState.Charging;
                    Main.projectile[i].localAI[0] = 0;

                    if (Main.projectile[i].ModProjectile is HarpyFeatherOrbit feather) {
                        feather.launchCountdown = LaunchDelay;
                    }
                }
            }

            SoundEngine.PlaySound(SoundID.Item30 with {
                Volume = 0.7f,
                Pitch = 0.6f
            }, owner.Center);

            SoundEngine.PlaySound(SoundID.DD2_WitherBeastAuraPulse with {
                Volume = 0.5f,
                Pitch = 0.8f
            }, owner.Center);

            for (int i = 0; i < 30; i++) {
                float angle = MathHelper.TwoPi * i / 30f;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(4f, 8f);

                Dust charge = Dust.NewDustPerfect(
                    owner.Center,
                    DustID.Cloud,
                    velocity,
                    100,
                    new Color(255, 255, 255),
                    Main.rand.NextFloat(1.5f, 2.5f)
                );
                charge.noGravity = true;
                charge.fadeIn = 1.3f;
            }
        }

        private void GatheringPhaseAI(Player owner) {
            float progress = StateTimer / GatherDuration;

            int myIndex = GetMyFeatherIndex(owner);
            int totalFeathers = GetActiveFeatherCount(owner);
            float targetAngle = MathHelper.TwoPi * myIndex / Math.Max(totalFeathers, 1);

            Vector2 targetPos = owner.Center + targetAngle.ToRotationVector2() * orbitRadius;

            float easeProgress = EaseOutSine(progress);

            Vector2 driftOffset = new Vector2(
                (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f + floatPhase) * 15f * (1f - easeProgress),
                (float)Math.Cos(Main.GlobalTimeWrappedHourly * 1.5f + floatPhase) * 12f * (1f - easeProgress)
            );

            Projectile.Center = Vector2.Lerp(Projectile.Center, targetPos + driftOffset, easeProgress * 0.3f);

            GlobalOrbitAngle = targetAngle;
            glowIntensity = MathHelper.Lerp(0f, 0.4f, progress);

            if (Main.rand.NextBool(5)) {
                SpawnGatherParticle(owner);
            }

            if (StateTimer >= GatherDuration) {
                State = FeatherState.Floating;
                StateTimer = 0;

                SoundEngine.PlaySound(SoundID.Item32 with {
                    Volume = 0.3f,
                    Pitch = 0.3f
                }, Projectile.Center);
            }
        }

        private void FloatingPhaseAI(Player owner) {
            float progress = StateTimer / FloatDuration;

            int myIndex = GetMyFeatherIndex(owner);
            int totalFeathers = GetActiveFeatherCount(owner);
            float targetAngle = MathHelper.TwoPi * myIndex / Math.Max(totalFeathers, 1);

            float radiusPulse = (float)Math.Sin(StateTimer * 0.15f) * 8f;
            float currentRadius = orbitRadius + radiusPulse;

            floatPhase += floatFrequency;
            Vector2 floatOffset = new Vector2(
                (float)Math.Sin(floatPhase * 1.2f) * floatAmplitude,
                (float)Math.Cos(floatPhase * 0.8f) * floatAmplitude * 0.7f
            );

            GlobalOrbitAngle = MathHelper.Lerp(GlobalOrbitAngle, targetAngle, 0.08f);

            Vector2 orbitPos = owner.Center + GlobalOrbitAngle.ToRotationVector2() * currentRadius;
            Vector2 targetPos = orbitPos + floatOffset;

            Projectile.Center = Vector2.Lerp(Projectile.Center, targetPos, 0.25f);

            glowIntensity = MathHelper.Lerp(0.4f, 0.6f, progress);

            if (Main.rand.NextBool(8)) {
                SpawnFloatParticle();
            }

            if (StateTimer >= FloatDuration) {
                State = FeatherState.Orbiting;
                StateTimer = 0;

                SoundEngine.PlaySound(SoundID.Item30 with {
                    Volume = 0.35f,
                    Pitch = 0.4f
                }, Projectile.Center);
            }
        }

        private void OrbitingPhaseAI(Player owner) {
            float timeProgress = MathHelper.Clamp(StateTimer / 60f, 0f, 1f);

            float speedProgress = EaseInOutQuad(timeProgress);
            orbitSpeed = MathHelper.Lerp(0.03f, MaxOrbitSpeed, speedProgress);

            float radiusScale = MathHelper.Lerp(1f, 0.92f, MathHelper.Clamp(speedProgress, 0f, 1f));
            float radiusWave = (float)Math.Sin(StateTimer * 0.2f) * 6f;
            float currentRadius = orbitRadius * radiusScale + radiusWave;

            GlobalOrbitAngle -= orbitSpeed;

            float floatScale = MathHelper.Clamp(1f - speedProgress * 0.6f, 0.4f, 1f);
            floatPhase += floatFrequency * floatScale;
            Vector2 floatOffset = new Vector2(
                (float)Math.Sin(floatPhase) * floatAmplitude * floatScale,
                (float)Math.Cos(floatPhase * 0.7f) * floatAmplitude * 0.6f * floatScale
            );

            Vector2 orbitPos = owner.Center + GlobalOrbitAngle.ToRotationVector2() * currentRadius;
            Vector2 targetPos = orbitPos + floatOffset;

            Projectile.Center = Vector2.Lerp(Projectile.Center, targetPos, 0.35f);

            glowIntensity = MathHelper.Lerp(0.6f, 0.8f, timeProgress);

            if (Main.rand.NextBool(4)) {
                SpawnOrbitParticle(timeProgress);
            }

            if (StateTimer % (int)MathHelper.Lerp(30, 15, timeProgress) == 0) {
                SoundEngine.PlaySound(SoundID.Item32 with {
                    Volume = 0.2f + 0.15f * timeProgress,
                    Pitch = 0.3f + timeProgress * 0.3f
                }, Projectile.Center);
            }
        }

        private void ChargingPhaseAI(Player owner) {
            float progress = StateTimer / ChargeDuration;

            orbitSpeed = MaxOrbitSpeed;

            float radiusOscillation = (float)Math.Sin(StateTimer * 0.6f) * 12f * progress;
            float currentRadius = orbitRadius * 0.92f + radiusOscillation;

            GlobalOrbitAngle -= orbitSpeed;
            Vector2 orbitOffset = GlobalOrbitAngle.ToRotationVector2() * currentRadius;
            Vector2 targetPos = owner.Center + orbitOffset;
            Projectile.Center = Vector2.Lerp(Projectile.Center, targetPos, 0.4f);

            glowIntensity = 0.9f + (float)Math.Sin(StateTimer * 1.2f) * 0.1f;

            if (Main.rand.NextBool()) {
                SpawnChargeParticle(owner.Center, progress);
            }

            if (StateTimer % 8 == 0) {
                SpawnChargePulse();
            }

            if (StateTimer % 6 == 0) {
                SoundEngine.PlaySound(SoundID.Item32 with {
                    Volume = 0.2f + progress * 0.3f,
                    Pitch = 0.5f + progress * 0.5f
                }, Projectile.Center);
            }

            launchCountdown--;
            if (launchCountdown <= 0) {
                LaunchFeather(owner);
            }
        }

        private void SpawnChargeParticle(Vector2 ownerCenter, float progress) {
            Vector2 toCenter = (ownerCenter - Projectile.Center).SafeNormalize(Vector2.Zero);
            Vector2 velocity = toCenter * Main.rand.NextFloat(2f, 5f) * progress;

            Dust charge = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                DustID.Cloud,
                velocity,
                100,
                new Color(255, 255, 255),
                Main.rand.NextFloat(1.3f, 2f)
            );
            charge.noGravity = true;
            charge.fadeIn = 1.2f;
        }

        private void SpawnChargePulse() {
            for (int i = 0; i < 8; i++) {
                float angle = MathHelper.TwoPi * i / 8f;
                Vector2 velocity = angle.ToRotationVector2() * 3f;

                Dust pulse = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Cloud,
                    velocity,
                    100,
                    new Color(255, 255, 255),
                    Main.rand.NextFloat(1.5f, 2.2f)
                );
                pulse.noGravity = true;
                pulse.fadeIn = 1.3f;
            }
        }

        private void LaunchFeather(Player owner) {
            Vector2 launchDir = (GlobalOrbitAngle - MathHelper.PiOver2).ToRotationVector2();

            float speedBonus = orbitSpeed / MaxOrbitSpeed;
            float finalSpeed = LaunchSpeed * (1f + speedBonus * 0.4f);

            Projectile.velocity = launchDir * finalSpeed;
            Projectile.tileCollide = true;

            State = FeatherState.Launching;
            StateTimer = 0;

            SpawnLaunchEffect();

            if (Projectile.whoAmI == GetFirstFeatherID(owner)) {
                SoundEngine.PlaySound(SoundID.Item1 with {
                    Volume = 0.6f,
                    Pitch = 0.6f
                }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item32 with {
                    Volume = 0.5f,
                    Pitch = 0.8f
                }, Projectile.Center);
            }
        }

        private void LaunchingPhaseAI() {
            Projectile.velocity *= 0.995f;

            Vector2 driftForce = new Vector2(
                (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f + floatPhase) * 0.05f,
                (float)Math.Cos(Main.GlobalTimeWrappedHourly * 1.5f + floatPhase) * 0.04f
            );
            Projectile.velocity += driftForce;

            if (Projectile.velocity.LengthSquared() > 0.1f) {
                Projectile.rotation = Projectile.velocity.ToRotation();
            }

            glowIntensity = 0.8f;

            if (Main.rand.NextBool(4)) {
                SpawnLaunchTrailParticle();
            }
        }

        private int GetMyFeatherIndex(Player owner) {
            int index = 0;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                if (Main.projectile[i].active &&
                    Main.projectile[i].type == Projectile.type &&
                    Main.projectile[i].owner == owner.whoAmI &&
                    Main.projectile[i].ai[1] < 4) {

                    if (Main.projectile[i].whoAmI == Projectile.whoAmI) {
                        return index;
                    }
                    index++;
                }
            }
            return 0;
        }

        private int GetFirstFeatherID(Player owner) {
            for (int i = 0; i < Main.maxProjectiles; i++) {
                if (Main.projectile[i].active &&
                    Main.projectile[i].type == Projectile.type &&
                    Main.projectile[i].owner == owner.whoAmI) {
                    return Main.projectile[i].whoAmI;
                }
            }
            return Projectile.whoAmI;
        }

        private void SpawnGatherParticle(Player owner) {
            Dust gather = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(12f, 12f),
                DustID.Cloud,
                (owner.Center - Projectile.Center).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(0.5f, 2f),
                100,
                new Color(240, 240, 255),
                Main.rand.NextFloat(0.8f, 1.3f)
            );
            gather.noGravity = true;
            gather.fadeIn = 1f;
        }

        private void SpawnFloatParticle() {
            Vector2 velocity = Main.rand.NextVector2Circular(1f, 1f);

            Dust float_ = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                DustID.Cloud,
                velocity,
                100,
                new Color(245, 245, 255),
                Main.rand.NextFloat(0.7f, 1.2f)
            );
            float_.noGravity = true;
            float_.fadeIn = 1f;
            float_.alpha = 100;
        }

        private void SpawnOrbitParticle(float progress) {
            Vector2 tangentDir = new Vector2(
                -(float)Math.Sin(GlobalOrbitAngle),
                (float)Math.Cos(GlobalOrbitAngle)
            );

            Vector2 velocity = tangentDir * Main.rand.NextFloat(1f, 3f) * progress;

            Dust orbit = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(6f, 6f),
                DustID.Cloud,
                velocity,
                100,
                new Color(250, 250, 255),
                Main.rand.NextFloat(0.9f, 1.5f)
            );
            orbit.noGravity = true;
            orbit.fadeIn = 1.1f;
            orbit.alpha = 80;
        }

        private void SpawnLaunchEffect() {
            for (int i = 0; i < 15; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(2f, 8f);

                Dust launch = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Cloud,
                    velocity,
                    100,
                    new Color(250, 250, 255),
                    Main.rand.NextFloat(1.2f, 2f)
                );
                launch.noGravity = true;
                launch.fadeIn = 1.2f;
            }

            for (int i = 0; i < 8; i++) {
                Dust air = Dust.NewDustDirect(
                    Projectile.Center - new Vector2(10),
                    20, 20,
                    DustID.Cloud,
                    Scale: Main.rand.NextFloat(1.5f, 2.5f)
                );
                air.velocity = Main.rand.NextVector2Circular(4f, 4f);
                air.noGravity = true;
                air.alpha = 120;
            }
        }

        private void SpawnLaunchTrailParticle() {
            Dust trail = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(6f, 6f),
                DustID.Cloud,
                -Projectile.velocity * Main.rand.NextFloat(0.1f, 0.2f),
                100,
                new Color(245, 245, 255),
                Main.rand.NextFloat(0.8f, 1.4f)
            );
            trail.noGravity = true;
            trail.fadeIn = 1f;
            trail.alpha = 120;
        }

        private float EaseOutSine(float t) {
            return (float)Math.Sin(t * MathHelper.PiOver2);
        }

        private float EaseInOutQuad(float t) {
            return t < 0.5f ? 2f * t * t : 1f - (float)Math.Pow(-2f * t + 2f, 2) / 2f;
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (Math.Abs(Projectile.velocity.X - oldVelocity.X) > float.Epsilon) {
                Projectile.velocity.X = -oldVelocity.X * 0.6f;
            }
            if (Math.Abs(Projectile.velocity.Y - oldVelocity.Y) > float.Epsilon) {
                Projectile.velocity.Y = -oldVelocity.Y * 0.6f;
            }

            SoundEngine.PlaySound(SoundID.Item32 with {
                Volume = 0.3f,
                Pitch = 0.5f
            }, Projectile.Center);

            for (int i = 0; i < 5; i++) {
                Dust.NewDust(
                    Projectile.position,
                    Projectile.width,
                    Projectile.height,
                    DustID.Cloud,
                    Scale: Main.rand.NextFloat(1f, 1.5f)
                );
            }

            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            for (int i = 0; i < 10; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(5f, 5f);
                Dust hitDust = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Cloud,
                    velocity,
                    100,
                    new Color(250, 250, 255),
                    Main.rand.NextFloat(1.2f, 1.8f)
                );
                hitDust.noGravity = true;
                hitDust.fadeIn = 1.1f;
            }

            SoundEngine.PlaySound(SoundID.NPCHit5 with {
                Volume = 0.4f,
                Pitch = 0.4f
            }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D featherTex = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Rectangle sourceRect = featherTex.Frame(1, 1);
            Vector2 origin = sourceRect.Size() / 2f;

            Color baseColor = lightColor;
            float alpha = (255f - Projectile.alpha) / 255f;

            if (State == FeatherState.Orbiting || State == FeatherState.Charging || State == FeatherState.Launching) {
                DrawFeatherAfterimages(sb, featherTex, sourceRect, origin, baseColor, alpha);
            }

            if (glowIntensity > 0.3f && SoftGlow?.Value != null) {
                Texture2D glow = SoftGlow.Value;
                float glowScale = Projectile.scale * (0.8f + glowIntensity * 0.4f);
                float glowAlpha = (glowIntensity - 0.3f) * alpha * 0.3f;

                if (State == FeatherState.Charging) {
                    glowAlpha *= 1.5f;
                }

                sb.Draw(
                    glow,
                    drawPos,
                    null,
                    new Color(250, 250, 255, 0) * glowAlpha,
                    Projectile.rotation + swayAngle,
                    glow.Size() / 2f,
                    glowScale,
                    SpriteEffects.None,
                    0f
                );
            }

            float drawRotation;
            if (State == FeatherState.Launching) {
                drawRotation = Projectile.rotation - MathHelper.PiOver2;
            }
            else {
                drawRotation = GlobalOrbitAngle - MathHelper.PiOver2 + swayAngle;
            }

            sb.Draw(
                featherTex,
                drawPos,
                sourceRect,
                baseColor * alpha,
                drawRotation,
                origin,
                Projectile.scale,
                SpriteEffects.None,
                0
            );

            if ((State == FeatherState.Orbiting || State == FeatherState.Charging || State == FeatherState.Launching)
                && glowIntensity > 0.5f) {
                float lightAlpha = (glowIntensity - 0.5f) * 2f * alpha * 0.35f;

                if (State == FeatherState.Charging) {
                    lightAlpha *= 1.3f;
                }

                Color featherLight = new Color(245, 245, 255);

                sb.Draw(
                    featherTex,
                    drawPos,
                    sourceRect,
                    featherLight * lightAlpha,
                    drawRotation,
                    origin,
                    Projectile.scale * 1.02f,
                    SpriteEffects.None,
                    0
                );
            }

            return false;
        }

        private void DrawFeatherAfterimages(SpriteBatch sb, Texture2D featherTex, Rectangle sourceRect,
            Vector2 origin, Color baseColor, float alpha) {

            int afterimageCount = State == FeatherState.Launching ? 10 : (State == FeatherState.Charging ? 8 : 6);

            for (int i = 0; i < afterimageCount; i++) {
                if (i >= Projectile.oldPos.Length || Projectile.oldPos[i] == Vector2.Zero) continue;

                float afterimageProgress = 1f - i / (float)afterimageCount;
                float afterimageAlpha = afterimageProgress * alpha * (State == FeatherState.Charging ? 0.6f : 0.5f);

                Color afterimageColor;
                if (State == FeatherState.Launching) {
                    afterimageColor = Color.Lerp(
                        new Color(240, 240, 255),
                        new Color(255, 255, 255),
                        afterimageProgress
                    ) * afterimageAlpha;
                }
                else if (State == FeatherState.Charging) {
                    afterimageColor = Color.Lerp(
                        new Color(245, 245, 255),
                        new Color(255, 255, 255),
                        afterimageProgress
                    ) * afterimageAlpha;
                }
                else {
                    afterimageColor = new Color(245, 245, 255) * (afterimageAlpha * 0.6f);
                }

                Vector2 afterimagePos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                float afterimageScale = Projectile.scale * MathHelper.Lerp(0.9f, 1f, afterimageProgress);

                float afterimageRotation;
                if (State == FeatherState.Launching) {
                    afterimageRotation = Projectile.velocity.ToRotation() - MathHelper.PiOver2 - i * 0.05f;
                }
                else {
                    afterimageRotation = GlobalOrbitAngle - MathHelper.PiOver2 + swayAngle - i * 0.08f;
                }

                sb.Draw(
                    featherTex,
                    afterimagePos,
                    sourceRect,
                    afterimageColor,
                    afterimageRotation,
                    origin,
                    afterimageScale,
                    SpriteEffects.None,
                    0
                );
            }
        }
    }
}
