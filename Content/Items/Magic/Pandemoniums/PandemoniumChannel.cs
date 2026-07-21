using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.Pandemoniums
{
    /// 引导法阵控制器
    internal class PandemoniumChannel : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        private Player Owner => Main.player[Projectile.owner];

        private ref float ChargeTimer => ref Projectile.ai[0];
        private ref float CurrentTier => ref Projectile.ai[1];

        private const int Tier1Time = 120;  //2秒到达1层
        private const int Tier2Time = 300;  //5秒到达2层
        private const int Tier3Time = 540;  //9秒到达3层

        private int attackCooldown = 0;
        private const int BaseAttackInterval = 50;

        private int comboCounter = 0;

        private List<RuneData>[] runeLayers = new List<RuneData>[3];
        private List<EnergyOrbData> orbs = new List<EnergyOrbData>();
        private List<LightningArcData> lightningArcs = new List<LightningArcData>();
        private List<CircleRingData> circleRings = new List<CircleRingData>();
        private List<BrimstoneEmberData> brimstoneEmbers = new List<BrimstoneEmberData>();

        private float visualTier = 0f;
        private float expandScale = 0f;
        private float tierTransitionProgress = 1f; //层级过渡 0=中 1=稳

        [VaultLoaden(CWRConstant.Masking + "SoftGlow")]
        private static Asset<Texture2D> GlowAsset = null;

        private class RuneData
        {
            public Vector2 Offset;
            public float Rotation;
            public float Scale;
            public float RotationSpeed;
            public float PulsePhase;
            public int Type;
            public float OrbitSpeed;
            public float OrbitPhase;
            public float SpiralAmount;
            public Vector2 Velocity;
            public float NoisePhase;
            public float DistanceModifier;
            public float BaseDistance;
            public float Alpha = 0f;
            public int FireFrame = 0;
            public float FireFrameCounter = 0;
            public float IntensityPulse = 0;
            public float CoreGlowAlpha = 0;
        }

        private class EnergyOrbData
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Life;
            public float MaxLife;
            public Color Color;
            public float Scale;
            public float RotationSpeed;
            public float Alpha = 0f;
        }

        private class LightningArcData
        {
            public Vector2 StartPos;
            public Vector2 EndPos;
            public float Life;
            public float MaxLife;
            public Color Color;
            public float Intensity;
            public List<Vector2> SegmentPoints;
        }

        private class CircleRingData
        {
            public float Radius;
            public float Life;
            public float MaxLife;
            public Color Color;
            public float Thickness;
        }

        private class BrimstoneEmberData
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Life;
            public float MaxLife;
            public float Scale;
            public float Rotation;
            public float RotationSpeed;
            public Color Color;
            public float Alpha = 1f;
        }

        public override void SetDefaults() {
            Projectile.width = 600;
            Projectile.height = 600;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 120;
            Projectile.alpha = 255;
        }

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }

            if (Owner.channel) {
                Projectile.timeLeft = 120;
            }


            if (ChargeTimer > 1 && ChargeTimer % 8 == 0) {
                int manaCost = 2 + (int)CurrentTier;
                if (!Owner.CheckMana(Owner.inventory[Owner.selectedItem], -manaCost, true)) {
                    Projectile.Kill();
                    return;
                }
            }

            Projectile.Center = Owner.Center - new Vector2(0, 100);
            Owner.itemTime = Owner.itemAnimation = 2;
            Owner.heldProj = Projectile.whoAmI;

            ChargeTimer++;
            attackCooldown--;

            if (ChargeTimer == 1) {
                SoundEngine.PlaySound(SoundID.DD2_EtherianPortalOpen with { Volume = 1.2f, Pitch = -0.8f }, Projectile.Center);
                for (int i = 0; i < runeLayers.Length; i++) {
                    runeLayers[i] = new List<RuneData>();
                }
                InitializeRuneLayer(0, 18, 220f);
                SpawnCircleRing(220f, new Color(255, 100, 50), 3f, 60);
                SpawnTierUpEffect(0);
            }

            visualTier = MathHelper.Lerp(visualTier, CurrentTier, 0.05f);
            expandScale = MathHelper.Lerp(expandScale, 1f + CurrentTier * 0.3f, 0.08f);

            if (tierTransitionProgress < 1f) {
                tierTransitionProgress = Math.Min(tierTransitionProgress + 0.015f, 1f);
            }

            if (ChargeTimer == Tier1Time && CurrentTier < 1) {
                tierTransitionProgress = 0f;
                CurrentTier = 1;
                SoundEngine.PlaySound(SoundID.DD2_DarkMageHealImpact with { Volume = 1.3f, Pitch = -0.2f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.8f, Pitch = -0.4f }, Projectile.Center);
                InitializeRuneLayer(1, 28, 320f);
                SpawnCircleRing(320f, new Color(255, 120, 60), 4f, 45);
                SpawnTierUpEffect(1);
                ExpandProjectileSize(750);
            }

            if (ChargeTimer == Tier2Time && CurrentTier < 2) {
                tierTransitionProgress = 0f;
                CurrentTier = 2;
                SoundEngine.PlaySound(SoundID.DD2_DarkMageHealImpact with { Volume = 1.5f, Pitch = 0f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.9f, Pitch = -0.3f }, Projectile.Center);
                InitializeRuneLayer(2, 42, 440f);
                SpawnCircleRing(440f, new Color(255, 140, 70), 5f, 30);
                SpawnTierUpEffect(2);
                ExpandProjectileSize(900);
            }

            if (ChargeTimer == Tier3Time && CurrentTier < 3) {
                tierTransitionProgress = 0f;
                CurrentTier = 3;
                SoundEngine.PlaySound(SoundID.DD2_DarkMageHealImpact with { Volume = 1.7f, Pitch = 0.2f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item74 with { Volume = 1.0f, Pitch = -0.2f }, Projectile.Center);
                SpawnCircleRing(560f, new Color(255, 160, 80), 6f, 20);
                SpawnTierUpEffect(3);
                ExpandProjectileSize(1000);
            }

            int attackInterval = BaseAttackInterval - (int)CurrentTier * 8;
            if (attackCooldown <= 0 && CurrentTier >= 1) {
                PerformTieredAttack();
                attackCooldown = attackInterval;
            }

            for (int i = 0; i <= (int)CurrentTier && i < runeLayers.Length; i++) {
                UpdateRuneLayer(i);
            }

            SpawnEnergyOrbs();
            UpdateEnergyOrbs();
            UpdateLightningArcs();
            UpdateCircleRings();
            SpawnBrimstoneEmbers();
            UpdateBrimstoneEmbers();
            SpawnChargeParticles();

            float lightIntensity = (1.5f + visualTier) * 2.5f;
            float flicker = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 12f) * 0.15f + 0.85f;
            Lighting.AddLight(Projectile.Center,
                2.0f * lightIntensity * flicker,
                0.6f * lightIntensity * flicker,
                0.3f * lightIntensity * flicker);

            if (CurrentTier >= 2) {
                float shakeValue = (CurrentTier - 1) * 0.8f * (float)Math.Sin(ChargeTimer * 0.05f);
                Owner.GetModPlayer<CWRPlayer>().ScreenShakeValue = Math.Max(
                    Owner.GetModPlayer<CWRPlayer>().ScreenShakeValue,
                    shakeValue);
            }
        }

        private void ExpandProjectileSize(int newSize) {
            int targetWidth = Math.Max(Projectile.width, newSize);
            int targetHeight = Math.Max(Projectile.height, newSize);
            Projectile.width = targetWidth;
            Projectile.height = targetHeight;
        }

        private void SpawnTierUpEffect(int tier) {
            for (int i = 0; i < 80; i++) {
                float angle = MathHelper.TwoPi * i / 80f;
                float distance = 150f + tier * 80f;
                Vector2 pos = Projectile.Center + angle.ToRotationVector2() * distance;
                Vector2 vel = (pos - Projectile.Center).SafeNormalize(Vector2.Zero) * (5f + tier * 2.5f);

                Dust d = Dust.NewDustPerfect(Projectile.Center, CWRID.Dust_Brimstone, vel, 100, default, 2.5f + tier * 0.5f);
                d.noGravity = true;
                d.fadeIn = 1.5f;
            }

            for (int i = 0; i < 40; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(8f, 8f);
                Dust fire = Dust.NewDustPerfect(Projectile.Center, DustID.Torch, vel, 100, Color.Red, 2f + tier * 0.5f);
                fire.noGravity = true;
            }

            for (int j = 0; j < 3; j++) {
                for (int i = 0; i < 24; i++) {
                    float angle = MathHelper.TwoPi * i / 24f;
                    float radius = 40f + j * 30f + tier * 50f;
                    Vector2 spawnPos = Projectile.Center + angle.ToRotationVector2() * radius;

                    Dust ring = Dust.NewDustPerfect(spawnPos, CWRID.Dust_Brimstone,
                        angle.ToRotationVector2() * 5f, 0, default, 2.5f);
                    ring.noGravity = true;
                }
            }
        }

        private void SpawnCircleRing(float radius, Color color, float thickness, int lifetime) {
            circleRings.Add(new CircleRingData {
                Radius = 0,
                Life = 0,
                MaxLife = lifetime,
                Color = color,
                Thickness = thickness
            });
        }

        private void UpdateCircleRings() {
            for (int i = circleRings.Count - 1; i >= 0; i--) {
                var ring = circleRings[i];
                ring.Life++;
                ring.Radius = MathHelper.Lerp(0, 600f, ring.Life / ring.MaxLife);

                if (ring.Life >= ring.MaxLife) {
                    circleRings.RemoveAt(i);
                }
            }
        }

        private void InitializeRuneLayer(int layer, int count, float baseDistance) {
            runeLayers[layer].Clear();
            for (int i = 0; i < count; i++) {
                float angle = MathHelper.TwoPi * i / count;
                float distance = baseDistance + Main.rand.NextFloat(-30f, 30f);

                runeLayers[layer].Add(new RuneData {
                    Offset = angle.ToRotationVector2() * distance,
                    Rotation = Main.rand.NextFloat(MathHelper.TwoPi),
                    Scale = Main.rand.NextFloat(0.6f, 1.0f) * (1f + layer * 0.05f),
                    RotationSpeed = Main.rand.NextFloat(-0.025f, 0.025f) * (1f + layer * 0.4f),
                    PulsePhase = Main.rand.NextFloat(MathHelper.TwoPi),
                    Type = Main.rand.Next(6),
                    OrbitSpeed = Main.rand.NextFloat(0.008f, 0.02f) * (layer % 2 == 0 ? 1 : -1),
                    OrbitPhase = angle,
                    SpiralAmount = Main.rand.NextFloat(0.08f, 0.25f),
                    Velocity = Vector2.Zero,
                    NoisePhase = Main.rand.NextFloat(MathHelper.TwoPi),
                    DistanceModifier = 1f,
                    BaseDistance = distance,
                    Alpha = 0f,
                    FireFrame = Main.rand.Next(16),//随机初始帧
                    FireFrameCounter = 0,
                    IntensityPulse = Main.rand.NextFloat(MathHelper.TwoPi),
                    CoreGlowAlpha = 0
                });
            }
        }

        private void UpdateRuneLayer(int layer) {
            if (layer >= runeLayers.Length || runeLayers[layer] == null) return;

            float time = Main.GlobalTimeWrappedHourly;
            float layerIntensity = 1f + layer * 0.4f;

            foreach (var rune in runeLayers[layer]) {
                float fadeSpeed = tierTransitionProgress < 0.5f ? 0.06f : 0.03f;
                rune.Alpha = MathHelper.Lerp(rune.Alpha, 1f, fadeSpeed);
                rune.CoreGlowAlpha = MathHelper.Lerp(rune.CoreGlowAlpha, 1f, fadeSpeed * 0.5f);

                rune.FireFrameCounter += 0.3f + layerIntensity * 0.1f;
                if (rune.FireFrameCounter >= 1f) {
                    rune.FireFrameCounter = 0;
                    rune.FireFrame = (rune.FireFrame + 1) % 16;//4x4=16帧循环
                }

                rune.IntensityPulse += 0.15f * layerIntensity;

                rune.Rotation += rune.RotationSpeed * layerIntensity;
                rune.PulsePhase += 0.06f * layerIntensity;
                rune.NoisePhase += 0.04f;

                rune.OrbitPhase += rune.OrbitSpeed;

                float a = 2.5f + layer * 0.8f;
                float b = 1.8f + layer * 0.6f;
                float delta = layer * VaultUtils.PiOver3;

                float lissajousX = (float)Math.Sin(a * rune.OrbitPhase + delta);
                float lissajousY = (float)Math.Sin(b * rune.OrbitPhase);

                float spiral = rune.SpiralAmount * (float)Math.Sin(time * 1.5f + rune.OrbitPhase * 2.5f);

                float noise1 = (float)Math.Sin(rune.NoisePhase * 2.2f) * 0.25f;
                float noise2 = (float)Math.Cos(rune.NoisePhase * 3.7f + layer) * 0.18f;
                float noiseModulation = (noise1 + noise2) * 15f;

                rune.DistanceModifier = 1f + (float)Math.Sin(time * 1.2f + layer * MathHelper.TwoPi / 3 + rune.OrbitPhase) * 0.12f;

                Vector2 basePos = rune.OrbitPhase.ToRotationVector2() * rune.BaseDistance * rune.DistanceModifier;
                Vector2 lissajousOffset = new Vector2(lissajousX, lissajousY) * 25f * (1f + layer * 0.25f);
                Vector2 spiralOffset = basePos.RotatedBy(spiral) - basePos;

                if (Main.rand.NextBool(180 - layer * 30)) {
                    rune.Velocity += Main.rand.NextVector2Circular(8f, 8f);
                }
                rune.Velocity *= 0.92f;

                Vector2 noiseOffset = rune.Velocity + new Vector2(
                    (float)Math.Sin(rune.NoisePhase) * noiseModulation,
                    (float)Math.Cos(rune.NoisePhase * 1.3f) * noiseModulation
                );

                rune.Offset = basePos + lissajousOffset + spiralOffset + noiseOffset;
            }
        }

        private void SpawnEnergyOrbs() {
            int spawnChance = Math.Max(1, 6 - (int)CurrentTier * 2);

            if (Main.rand.NextBool(spawnChance)) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float distance = Main.rand.NextFloat(450f, 650f);
                Vector2 spawnPos = Projectile.Center + angle.ToRotationVector2() * distance;

                Color[] orbColors = {
                    new Color(255, 120, 60),   //亮橙
                    new Color(255, 80, 40),    //橙红
                    new Color(200, 50, 30),    //深红
                    new Color(255, 140, 70),   //金橙
                    new Color(180, 60, 30)     //暗橙
                };

                orbs.Add(new EnergyOrbData {
                    Position = spawnPos,
                    Velocity = (Projectile.Center - spawnPos).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(3f, 7f),
                    Life = 0,
                    MaxLife = Main.rand.NextFloat(60f, 90f),
                    Color = Main.rand.Next(orbColors),
                    Scale = Main.rand.NextFloat(0.9f, 1.6f),
                    RotationSpeed = Main.rand.NextFloat(-0.15f, 0.15f),
                    Alpha = 0f
                });
            }
        }

        private void UpdateEnergyOrbs() {
            for (int i = orbs.Count - 1; i >= 0; i--) {
                var orb = orbs[i];
                orb.Life++;
                orb.Alpha = MathHelper.Lerp(orb.Alpha, 1f, 0.05f);
                orb.Position += orb.Velocity;

                Vector2 toCenter = Projectile.Center - orb.Position;
                float distanceToCenter = toCenter.Length();
                orb.Velocity = Vector2.Lerp(orb.Velocity, toCenter.SafeNormalize(Vector2.Zero) * MathHelper.Clamp(distanceToCenter * 0.02f, 3f, 12f), 0.06f);

                if (orb.Life > orb.MaxLife || distanceToCenter < 50f) {
                    for (int j = 0; j < 6; j++) {
                        Dust d = Dust.NewDustPerfect(orb.Position, CWRID.Dust_Brimstone,
                            Main.rand.NextVector2Circular(3f, 3f), 100, default, 1.5f);
                        d.noGravity = true;
                    }
                    orbs.RemoveAt(i);
                }
            }
        }

        private void UpdateLightningArcs() {
            for (int i = lightningArcs.Count - 1; i >= 0; i--) {
                var arc = lightningArcs[i];
                arc.Life++;
                if (arc.Life >= arc.MaxLife) {
                    lightningArcs.RemoveAt(i);
                }
            }

            if (CurrentTier >= 2 && Main.rand.NextBool(15 - (int)CurrentTier * 3)) {
                int arcCount = 1 + (int)CurrentTier / 2;
                for (int i = 0; i < arcCount; i++) {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    float distance = 200f + CurrentTier * 100f;
                    Vector2 endPos = Projectile.Center + angle.ToRotationVector2() * distance;

                    List<Vector2> points = GenerateLightningPath(Projectile.Center, endPos, 5);

                    lightningArcs.Add(new LightningArcData {
                        StartPos = Projectile.Center,
                        EndPos = endPos,
                        Life = 0,
                        MaxLife = 18,
                        Color = new Color(255, 140, 80, 200),
                        Intensity = Main.rand.NextFloat(0.7f, 1f),
                        SegmentPoints = points
                    });
                }
            }
        }

        private List<Vector2> GenerateLightningPath(Vector2 start, Vector2 end, int segments) {
            List<Vector2> points = new List<Vector2> { start };
            Vector2 direction = end - start;
            float segmentLength = direction.Length() / segments;

            for (int i = 1; i < segments; i++) {
                float progress = i / (float)segments;
                Vector2 basePos = Vector2.Lerp(start, end, progress);
                Vector2 offset = Main.rand.NextVector2Circular(segmentLength * 0.4f, segmentLength * 0.4f);
                points.Add(basePos + offset);
            }

            points.Add(end);
            return points;
        }

        private void SpawnBrimstoneEmbers() {
            if (Main.rand.NextBool(4)) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float distance = Main.rand.NextFloat(100f, 400f);
                Vector2 spawnPos = Projectile.Center + angle.ToRotationVector2() * distance;

                brimstoneEmbers.Add(new BrimstoneEmberData {
                    Position = spawnPos,
                    Velocity = new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-3f, -1f)),
                    Life = 0,
                    MaxLife = Main.rand.NextFloat(80f, 120f),
                    Scale = Main.rand.NextFloat(1.5f, 3f),
                    Rotation = Main.rand.NextFloat(MathHelper.TwoPi),
                    RotationSpeed = Main.rand.NextFloat(-0.08f, 0.08f),
                    Color = Main.rand.Next(3) switch {
                        0 => new Color(255, 140, 70),
                        1 => new Color(255, 100, 50),
                        _ => new Color(200, 60, 30)
                    },
                    Alpha = 0f
                });
            }
        }

        private void UpdateBrimstoneEmbers() {
            for (int i = brimstoneEmbers.Count - 1; i >= 0; i--) {
                var ember = brimstoneEmbers[i];
                ember.Life++;
                ember.Alpha = Math.Min(ember.Alpha + 0.08f, 1f);
                ember.Position += ember.Velocity;
                ember.Velocity.Y -= 0.02f;
                ember.Rotation += ember.RotationSpeed;

                if (ember.Life > ember.MaxLife * 0.7f) {
                    ember.Alpha = MathHelper.Lerp(1f, 0f, (ember.Life - ember.MaxLife * 0.7f) / (ember.MaxLife * 0.3f));
                }

                if (ember.Life >= ember.MaxLife || ember.Alpha <= 0) {
                    brimstoneEmbers.RemoveAt(i);
                }
                else {
                    brimstoneEmbers[i] = ember;
                }
            }
        }

        private void ReleaseFinalBlast() {
            SoundEngine.PlaySound(SoundID.DD2_ExplosiveTrapExplode with { Volume = 1.4f, Pitch = -0.6f }, Projectile.Center);

            Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero,
                ModContent.ProjectileType<PandemoniumBlastWave>(), (int)(Projectile.damage * 8.2f), Projectile.knockBack * 2f, Owner.whoAmI);
        }

        private void PerformTieredAttack() {
            if (Owner.whoAmI != Main.myPlayer) return;

            int tier = (int)CurrentTier;

            int attackPattern = (comboCounter % 4);

            switch (tier) {
                case 1://镰刀螺旋
                    if (attackPattern == 0 || attackPattern == 2) {
                        ReleaseSpiralScytheWave(tier, 6);
                    }
                    else {
                        ReleaseHomingFireball(2);
                    }
                    break;

                case 2://追踪镰+集束球
                    if (attackPattern == 0) {
                        ReleaseSpiralScytheWave(tier, 8);
                    }
                    else if (attackPattern == 1) {
                        ReleaseClusterFireball(3);
                    }
                    else if (attackPattern == 2) {
                        ReleaseHomingScytheRing(tier, 10);
                    }
                    else {
                        ReleaseLightningChain();
                    }
                    break;

                case 3://全组合
                    if (attackPattern == 0) {
                        ReleaseSpiralScytheWave(tier, 12);
                        if (Main.rand.NextBool(2)) {
                            ReleaseHomingFireball(2);
                        }
                    }
                    else if (attackPattern == 1) {
                        ReleaseClusterFireball(4);
                        ReleaseLightningChain();
                    }
                    else if (attackPattern == 2) {
                        ReleaseHomingScytheRing(tier, 14);
                        ReleaseBrimstoneRain();
                    }
                    else {
                        ReleaseFinalBlast();
                    }
                    break;
            }

            comboCounter++;
        }

        //螺旋镰刀波
        private void ReleaseSpiralScytheWave(int tier, int count) {
            SoundEngine.PlaySound(SoundID.Item71 with { Volume = 1.1f, Pitch = -0.5f }, Projectile.Center);

            float speedBase = 11f + tier * 2f;

            for (int i = 0; i < count; i++) {
                float angle = MathHelper.TwoPi / count * i;
                float spiralPhase = i * 0.5f;

                Vector2 velocity = angle.ToRotationVector2() * speedBase;

                int damage = (int)(Projectile.damage * (2f + tier * 0.1f));
                int scythe = Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    Projectile.Center,
                    velocity,
                    ModContent.ProjectileType<PandemoniumScythe>(),
                    damage,
                    Projectile.knockBack,
                    Owner.whoAmI,
                    tier,
                    spiralPhase
                );

                Main.projectile[scythe].localAI[0] = 1; //标记为可追踪模式
            }
        }

        //追踪镰刀环
        private void ReleaseHomingScytheRing(int tier, int count) {
            SoundEngine.PlaySound(SoundID.Item71 with { Volume = 1.2f, Pitch = -0.3f }, Projectile.Center);

            NPC[] targets = new NPC[count];
            float searchRadius = 900f;

            List<NPC> potentialTargets = new List<NPC>();
            foreach (NPC npc in Main.npc) {
                if (npc.CanBeChasedBy(this) && npc.Distance(Projectile.Center) < searchRadius) {
                    potentialTargets.Add(npc);
                }
            }

            for (int i = 0; i < count; i++) {
                float angle = MathHelper.TwoPi * i / count;
                Vector2 velocity = angle.ToRotationVector2() * 8f;

                int targetIndex = -1;
                if (potentialTargets.Count > 0) {
                    targetIndex = potentialTargets[i % potentialTargets.Count].whoAmI;
                }

                int damage = (int)(Projectile.damage * 2.3f);
                int scythe = Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    Projectile.Center,
                    velocity,
                    ModContent.ProjectileType<PandemoniumScythe>(),
                    damage,
                    Projectile.knockBack,
                    Owner.whoAmI,
                    tier,
                    targetIndex,
                    i
                );

                Main.projectile[scythe].localAI[1] = 2; //标记为强追踪模式
            }
        }

        //火球预判鼠标
        private void ReleaseHomingFireball(int count) {
            SoundEngine.PlaySound(SoundID.DD2_BetsyFireballShot with { Volume = 1.3f, Pitch = -0.3f }, Projectile.Center);

            Vector2 targetPos = Main.MouseWorld;

            for (int i = 0; i < count; i++) {
                float delay = i * 5f;

                Vector2 predictedPos = targetPos;
                if (Owner != null) {
                    predictedPos += Owner.velocity * (delay / 60f) * 20f;
                }

                Vector2 toTarget = (predictedPos - Projectile.Center).SafeNormalize(Vector2.UnitY);
                Vector2 spreadOffset = toTarget.RotatedBy(Main.rand.NextFloat(-0.15f, 0.15f));

                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    Projectile.Center,
                    spreadOffset * 0.1f,
                    ModContent.ProjectileType<PandemoniumFireball>(),
                    (int)(Projectile.damage * 1.4f),
                    Projectile.knockBack,
                    Owner.whoAmI,
                    delay,
                    0 //标记为普通火球
                );
            }
        }

        //集束火球齐爆
        private void ReleaseClusterFireball(int clusterCount) {
            SoundEngine.PlaySound(SoundID.DD2_BetsyFireballShot with { Volume = 1.4f, Pitch = -0.4f }, Projectile.Center);

            Vector2 targetPos = Main.MouseWorld;

            for (int i = 0; i < clusterCount; i++) {
                float angle = MathHelper.TwoPi * i / clusterCount;
                Vector2 clusterOffset = angle.ToRotationVector2() * 150f;
                Vector2 spawnPoint = targetPos + clusterOffset;

                Vector2 direction = (spawnPoint - Projectile.Center).SafeNormalize(Vector2.UnitY);

                float delay = 10f + i * 3f;

                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    Projectile.Center,
                    direction * 0.1f,
                    ModContent.ProjectileType<PandemoniumFireball>(),
                    (int)(Projectile.damage * 1.3f),
                    Projectile.knockBack,
                    Owner.whoAmI,
                    delay,
                    1 //标记为集束火球
                );
            }
        }

        //闪电链边缘跳
        private void ReleaseLightningChain() {
            SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1.2f, Pitch = -0.2f }, Projectile.Center);

            int lightningCount = 3 + (int)CurrentTier;

            for (int i = 0; i < lightningCount; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float distance = 300f + CurrentTier * 50f;
                Vector2 spawnPos = Projectile.Center + angle.ToRotationVector2() * distance;

                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    spawnPos,
                    Vector2.Zero,
                    ModContent.ProjectileType<PandemoniumLightning>(),
                    (int)(Projectile.damage * 0.8f),
                    Projectile.knockBack * 0.5f,
                    Owner.whoAmI,
                    0,
                    CurrentTier
                );
            }
        }

        //硫磺血雨
        private void ReleaseBrimstoneRain() {
            SoundEngine.PlaySound(SoundID.Item73 with { Volume = 1.3f, Pitch = -0.5f }, Projectile.Center);

            int rainCount = 20 + (int)CurrentTier * 5;

            for (int i = 0; i < rainCount; i++) {
                Vector2 spawnPos = Projectile.Center + new Vector2(
                    Main.rand.NextFloat(-400f, 400f),
                    -Main.rand.NextFloat(300f, 500f)
                );

                Vector2 targetPos = Main.MouseWorld + Main.rand.NextVector2Circular(200f, 200f);
                Vector2 velocity = (targetPos - spawnPos).SafeNormalize(Vector2.UnitY) * Main.rand.NextFloat(8f, 14f);

                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    spawnPos,
                    velocity,
                    ModContent.ProjectileType<PandemoniumRainDrop>(),
                    (int)(Projectile.damage * 0.7f),
                    Projectile.knockBack * 0.3f,
                    Owner.whoAmI
                );
            }
        }

        private void SpawnChargeParticles() {
            int particleChance = Math.Max(1, 5 - (int)CurrentTier);

            if (Main.rand.NextBool(particleChance)) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float distance = Main.rand.NextFloat(350f, 550f);
                Vector2 spawnPos = Projectile.Center + angle.ToRotationVector2() * distance;
                Vector2 velocity = (Projectile.Center - spawnPos).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(5f, 10f);

                Dust d = Dust.NewDustPerfect(spawnPos, CWRID.Dust_Brimstone, velocity, 100, default, Main.rand.NextFloat(1.5f, 2.5f));
                d.noGravity = true;
                d.fadeIn = 1.3f;
            }

            if (Main.rand.NextBool(3)) {
                float angle = Main.GlobalTimeWrappedHourly * 5f + Main.rand.NextFloat(MathHelper.TwoPi);
                float radius = 80f + visualTier * 25f;
                Vector2 pos = Projectile.Center + angle.ToRotationVector2() * radius;

                Dust d = Dust.NewDustPerfect(pos, DustID.Torch, Vector2.Zero, 100, Color.Red, 1.2f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Vector2 center = Projectile.Center - Main.screenPosition;
            float time = Main.GlobalTimeWrappedHourly;
            int tier = (int)CurrentTier;

            float transitionEase = VaultUtils.EaseOutCubic(tierTransitionProgress);

            DrawBrimstoneDomainShader(sb, center, time, tier, transitionEase);

            DrawEnergyOrbs(sb);

            DrawLightningArcsVisual(sb);

            return false;
        }

        private void DrawBrimstoneDomainShader(SpriteBatch sb, Vector2 center, float time, int tier, float transitionEase) {
            Effect shader = EffectLoader.BrimstoneDomain?.Value;
            if (shader == null) return;

            Texture2D canvas = VaultAsset.placeholder2.Value;
            Texture2D noise = CWRAsset.Extra_193.Value;
            if (canvas == null || noise == null) return;

            
            float baseRadius = 300f + tier * 120f;
            float drawRadius = baseRadius * expandScale * 1.15f;//辉光余量
            float drawDiameter = drawRadius * 2f;

            shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.016f);
            shader.Parameters["fadeAlpha"]?.SetValue(Math.Min(expandScale, 1f) * transitionEase);
            shader.Parameters["tierLevel"]?.SetValue(visualTier);
            shader.Parameters["expandProgress"]?.SetValue(MathHelper.Clamp(expandScale, 0f, 1f));
            shader.Parameters["pulseIntensity"]?.SetValue(0.6f + (float)Math.Sin(time * 3f) * 0.4f);

            shader.Parameters["coreColor"]?.SetValue(new Vector3(1f, 0.31f, 0.16f));    //255,80,40
            shader.Parameters["midColor"]?.SetValue(new Vector3(0.78f, 0.2f, 0.12f));   //200,50,30
            shader.Parameters["edgeColor"]?.SetValue(new Vector3(0.47f, 0.12f, 0.08f)); //120,30,20
            shader.Parameters["voidColor"]?.SetValue(new Vector3(0.16f, 0.04f, 0.04f)); //40,10,10
            shader.Parameters["uNoiseTex"]?.SetValue(noise);

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive,
                SamplerState.LinearWrap, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);

            shader.CurrentTechnique.Passes[0].Apply();

            sb.Draw(canvas, center, null, Color.White,
                0f, canvas.Size() * 0.5f, new Vector2(drawDiameter, drawDiameter),
                SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);
        }

        private void DrawEnergyOrbs(SpriteBatch sb) {
            if (!(GlowAsset?.IsLoaded ?? false)) return;

            foreach (var orb in orbs) {
                Vector2 drawPos = orb.Position - Main.screenPosition;
                float lifeRatio = 1f - (orb.Life / orb.MaxLife);
                float scale = lifeRatio * orb.Scale * 0.45f * orb.Alpha;

                Color drawColor = orb.Color with { A = 0 };
                sb.Draw(GlowAsset.Value, drawPos, null, drawColor * lifeRatio * orb.Alpha, 0,
                    GlowAsset.Value.Size() / 2, scale, SpriteEffects.None, 0);

                sb.Draw(GlowAsset.Value, drawPos, null, Color.White with { A = 0 } * lifeRatio * orb.Alpha * 0.4f, 0,
                    GlowAsset.Value.Size() / 2, scale * 0.5f, SpriteEffects.None, 0);
            }
        }

        private void DrawLightningArcsVisual(SpriteBatch sb) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            foreach (var arc in lightningArcs) {
                float alpha = 1f - (arc.Life / arc.MaxLife);

                if (arc.SegmentPoints != null && arc.SegmentPoints.Count > 1) {
                    for (int i = 0; i < arc.SegmentPoints.Count - 1; i++) {
                        Vector2 start = arc.SegmentPoints[i] - Main.screenPosition;
                        Vector2 end = arc.SegmentPoints[i + 1] - Main.screenPosition;

                        DrawLine(sb, pixel, start, end, 2.5f, arc.Color * alpha * arc.Intensity);
                        DrawLine(sb, pixel, start, end, 5f, arc.Color * alpha * arc.Intensity * 0.25f);
                    }
                }
            }
        }

        private static void DrawLine(SpriteBatch sb, Texture2D pixel, Vector2 start, Vector2 end, float thickness, Color color) {
            Vector2 diff = end - start;
            float length = diff.Length();
            if (length < 1f) return;
            sb.Draw(pixel, start, new Rectangle(0, 0, 1, 1), color, diff.ToRotation(), Vector2.Zero, new Vector2(length, thickness), SpriteEffects.None, 0f);
        }
    }
}
