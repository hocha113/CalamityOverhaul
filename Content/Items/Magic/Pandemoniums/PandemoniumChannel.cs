using CalamityMod.Dusts;
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
    /// <summary>
    /// 引导法阵的核心控制器
    /// </summary>
    internal class PandemoniumChannel : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        private Player Owner => Main.player[Projectile.owner];

        private ref float ChargeTimer => ref Projectile.ai[0];
        private ref float CurrentTier => ref Projectile.ai[1];

        private const int Tier1Time = 120;  //2秒到达1层
        private const int Tier2Time = 300;  //5秒到达2层
        private const int Tier3Time = 540;  //9秒到达3层

        //攻击发射间隔
        private int attackCooldown = 0;
        private const int BaseAttackInterval = 50;

        //连击系统
        private int comboCounter = 0;

        //符文动画数据 - 多层系统
        private List<RuneData>[] runeLayers = new List<RuneData>[3];
        private List<EnergyOrbData> orbs = new List<EnergyOrbData>();
        private List<LightningArcData> lightningArcs = new List<LightningArcData>();
        private List<CircleRingData> circleRings = new List<CircleRingData>();
        private List<BrimstoneEmberData> brimstoneEmbers = new List<BrimstoneEmberData>();

        //平滑过渡变量
        private float visualTier = 0f;
        private float expandScale = 0f;
        private float tierTransitionProgress = 1f; //层级过渡进度，0=过渡中，1=稳定

        [VaultLoaden(CWRConstant.Masking + "Fire")]
        private static Asset<Texture2D> RuneAsset = null;
        [VaultLoaden(CWRConstant.Masking)]
        private static Asset<Texture2D> StarTexture = null;
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
            //火焰动画相关
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


            //持续消耗法力（更合理的消耗）
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

            //初始化符文层
            if (ChargeTimer == 1) {
                SoundEngine.PlaySound(SoundID.DD2_EtherianPortalOpen with { Volume = 1.2f, Pitch = -0.8f }, Projectile.Center);
                for (int i = 0; i < runeLayers.Length; i++) {
                    runeLayers[i] = new List<RuneData>();
                }
                InitializeRuneLayer(0, 18, 220f);
                SpawnCircleRing(220f, new Color(255, 100, 50), 3f, 60);
                SpawnTierUpEffect(0);
            }

            //平滑视觉层级过渡
            visualTier = MathHelper.Lerp(visualTier, CurrentTier, 0.05f);
            expandScale = MathHelper.Lerp(expandScale, 1f + CurrentTier * 0.3f, 0.08f);

            //层级过渡进度更新
            if (tierTransitionProgress < 1f) {
                tierTransitionProgress = Math.Min(tierTransitionProgress + 0.015f, 1f);
            }

            //层级提升逻辑 - 改进过渡效果
            if (ChargeTimer == Tier1Time && CurrentTier < 1) {
                tierTransitionProgress = 0f; //开始过渡
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

            //持续攻击逻辑（根据层级调整间隔和攻击模式）
            int attackInterval = BaseAttackInterval - (int)CurrentTier * 8;
            if (attackCooldown <= 0 && CurrentTier >= 1) {
                PerformTieredAttack();
                attackCooldown = attackInterval;
            }

            //更新所有符文层
            for (int i = 0; i <= (int)CurrentTier && i < runeLayers.Length; i++) {
                UpdateRuneLayer(i);
            }

            //生成和更新效果
            SpawnEnergyOrbs();
            UpdateEnergyOrbs();
            UpdateLightningArcs();
            UpdateCircleRings();
            SpawnBrimstoneEmbers();
            UpdateBrimstoneEmbers();
            SpawnChargeParticles();

            //动态照明（硫磺火风格）
            float lightIntensity = (1.5f + visualTier) * 2.5f;
            float flicker = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 12f) * 0.15f + 0.85f;
            Lighting.AddLight(Projectile.Center,
                2.0f * lightIntensity * flicker,  //红色分量增强
                0.6f * lightIntensity * flicker,  //绿色分量
                0.3f * lightIntensity * flicker); //蓝色分量降低，呈现橙红色

            //屏幕震动（更平滑）
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
            //硫磺火风格的层级提升效果
            for (int i = 0; i < 80; i++) {
                float angle = MathHelper.TwoPi * i / 80f;
                float distance = 150f + tier * 80f;
                Vector2 pos = Projectile.Center + angle.ToRotationVector2() * distance;
                Vector2 vel = (pos - Projectile.Center).SafeNormalize(Vector2.Zero) * (5f + tier * 2.5f);

                //硫磺火粒子
                Dust d = Dust.NewDustPerfect(Projectile.Center, (int)CalamityDusts.Brimstone, vel, 100, default, 2.5f + tier * 0.5f);
                d.noGravity = true;
                d.fadeIn = 1.5f;
            }

            //红色火焰核心
            for (int i = 0; i < 40; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(8f, 8f);
                Dust fire = Dust.NewDustPerfect(Projectile.Center, DustID.Torch, vel, 100, Color.Red, 2f + tier * 0.5f);
                fire.noGravity = true;
            }

            //地狱火环
            for (int j = 0; j < 3; j++) {
                for (int i = 0; i < 24; i++) {
                    float angle = MathHelper.TwoPi * i / 24f;
                    float radius = 40f + j * 30f + tier * 50f;
                    Vector2 spawnPos = Projectile.Center + angle.ToRotationVector2() * radius;

                    Dust ring = Dust.NewDustPerfect(spawnPos, (int)CalamityDusts.Brimstone,
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
                //淡入效果 - 在过渡期间加速
                float fadeSpeed = tierTransitionProgress < 0.5f ? 0.06f : 0.03f;
                rune.Alpha = MathHelper.Lerp(rune.Alpha, 1f, fadeSpeed);
                rune.CoreGlowAlpha = MathHelper.Lerp(rune.CoreGlowAlpha, 1f, fadeSpeed * 0.5f);

                //火焰帧动画更新
                rune.FireFrameCounter += 0.3f + layerIntensity * 0.1f;
                if (rune.FireFrameCounter >= 1f) {
                    rune.FireFrameCounter = 0;
                    rune.FireFrame = (rune.FireFrame + 1) % 16;//4x4=16帧循环
                }

                //强度脉冲（用于火焰闪烁效果）
                rune.IntensityPulse += 0.15f * layerIntensity;

                //基础旋转（更平滑）
                rune.Rotation += rune.RotationSpeed * layerIntensity;
                rune.PulsePhase += 0.06f * layerIntensity;
                rune.NoisePhase += 0.04f;

                //轨道运动
                rune.OrbitPhase += rune.OrbitSpeed;

                //Lissajous曲线（参数优化）
                float a = 2.5f + layer * 0.8f;
                float b = 1.8f + layer * 0.6f;
                float delta = layer * VaultUtils.PiOver3;

                float lissajousX = (float)Math.Sin(a * rune.OrbitPhase + delta);
                float lissajousY = (float)Math.Sin(b * rune.OrbitPhase);

                //螺旋调制（更温和）
                float spiral = rune.SpiralAmount * (float)Math.Sin(time * 1.5f + rune.OrbitPhase * 2.5f);

                //改进的噪声
                float noise1 = (float)Math.Sin(rune.NoisePhase * 2.2f) * 0.25f;
                float noise2 = (float)Math.Cos(rune.NoisePhase * 3.7f + layer) * 0.18f;
                float noiseModulation = (noise1 + noise2) * 15f;

                //呼吸效果（更自然）
                rune.DistanceModifier = 1f + (float)Math.Sin(time * 1.2f + layer * MathHelper.TwoPi / 3 + rune.OrbitPhase) * 0.12f;

                //组合运动
                Vector2 basePos = rune.OrbitPhase.ToRotationVector2() * rune.BaseDistance * rune.DistanceModifier;
                Vector2 lissajousOffset = new Vector2(lissajousX, lissajousY) * 25f * (1f + layer * 0.25f);
                Vector2 spiralOffset = basePos.RotatedBy(spiral) - basePos;

                //平滑的随机扰动
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

                //硫磺火色彩
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
                    //硫磺火汇聚效果
                    for (int j = 0; j < 6; j++) {
                        Dust d = Dust.NewDustPerfect(orb.Position, (int)CalamityDusts.Brimstone,
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
                        Color = new Color(255, 140, 80, 200), //硫磺火色彩
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
            //硫磺火余烬粒子
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
                ember.Velocity.Y -= 0.02f; //轻微上升
                ember.Rotation += ember.RotationSpeed;

                //逐渐消失
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

            //根据连击数选择攻击模式，形成连贯的攻击节奏
            int attackPattern = (comboCounter % 4);

            switch (tier) {
                case 1: //第一层：基础镰刀螺旋
                    if (attackPattern == 0 || attackPattern == 2) {
                        ReleaseSpiralScytheWave(tier, 6);
                    }
                    else {
                        ReleaseHomingFireball(2);
                    }
                    break;

                case 2: //第二层：添加追踪镰刀和集束火球
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

                case 3: //第三层：全面攻击组合
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

            //更新连击计数
            comboCounter++;
        }

        //改进的螺旋镰刀波 - 镰刀会螺旋展开并互相追踪
        private void ReleaseSpiralScytheWave(int tier, int count) {
            SoundEngine.PlaySound(SoundID.Item71 with { Volume = 1.1f, Pitch = -0.5f }, Projectile.Center);

            float speedBase = 11f + tier * 2f;

            for (int i = 0; i < count; i++) {
                float angle = MathHelper.TwoPi / count * i;
                float spiralPhase = i * 0.5f;

                //螺旋轨迹的初始速度
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

        //追踪镰刀环 - 镰刀会主动寻找并锁定目标
        private void ReleaseHomingScytheRing(int tier, int count) {
            SoundEngine.PlaySound(SoundID.Item71 with { Volume = 1.2f, Pitch = -0.3f }, Projectile.Center);

            NPC[] targets = new NPC[count];
            float searchRadius = 900f;

            //先找出最近的几个敌人
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
                    targetIndex, //将目标索引传递给镰刀
                    i
                );

                Main.projectile[scythe].localAI[1] = 2; //标记为强追踪模式
            }
        }

        //改进的火球 - 追踪玩家鼠标位置并预判
        private void ReleaseHomingFireball(int count) {
            SoundEngine.PlaySound(SoundID.DD2_BetsyFireballShot with { Volume = 1.3f, Pitch = -0.3f }, Projectile.Center);

            Vector2 targetPos = Main.MouseWorld;

            for (int i = 0; i < count; i++) {
                float delay = i * 5f;

                //预判目标位置（如果玩家在移动）
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

        //集束火球 - 火球会在空中形成阵型然后一起爆炸
        private void ReleaseClusterFireball(int clusterCount) {
            SoundEngine.PlaySound(SoundID.DD2_BetsyFireballShot with { Volume = 1.4f, Pitch = -0.4f }, Projectile.Center);

            Vector2 targetPos = Main.MouseWorld;

            //在目标位置周围生成一圈火球
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

        //闪电链 - 在法阵边缘生成闪电球，会在敌人之间跳跃
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

        //硫磺血雨 - 从法阵上方落下大量硫磺火球
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

            //硫磺火粒子
            if (Main.rand.NextBool(particleChance)) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float distance = Main.rand.NextFloat(350f, 550f);
                Vector2 spawnPos = Projectile.Center + angle.ToRotationVector2() * distance;
                Vector2 velocity = (Projectile.Center - spawnPos).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(5f, 10f);

                Dust d = Dust.NewDustPerfect(spawnPos, (int)CalamityDusts.Brimstone, velocity, 100, default, Main.rand.NextFloat(1.5f, 2.5f));
                d.noGravity = true;
                d.fadeIn = 1.3f;
            }

            //红色火焰粒子
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

            //硫磺火色彩方案 - 黑红暗色系
            Color coreColor = new Color(255, 80, 40);        //核心橙红
            Color midColor = new Color(200, 50, 30);         //中层深红
            Color edgeColor = new Color(120, 30, 20);        //边缘暗红
            Color darkColor = new Color(80, 20, 15);         //深黑红
            Color voidColor = new Color(40, 10, 10);         //虚空暗红
            Color highlightColor = new Color(255, 100, 50);  //高光橙红

            //计算过渡效果 - 使用缓动函数平滑过渡
            float transitionEase = CWRUtils.EaseOutCubic(tierTransitionProgress);
            float visualAlpha = expandScale * transitionEase;

            //绘制扩展环（硫磺火色彩）
            foreach (var ring in circleRings) {
                float ringAlpha = (1f - (ring.Life / ring.MaxLife)) * transitionEase;
                DrawCircleRing(sb, center, ring.Radius, ring.Thickness, ring.Color * ringAlpha);
            }

            //绘制外层暗影光环
            for (int i = 0; i <= tier; i++) {
                float ringSize = 520f + i * 130f;
                float alpha = (0.7f - i * 0.12f) * visualAlpha;
                DrawVoidRing(sb, center, ringSize, voidColor, alpha, time * (1f + i * 0.25f));
            }

            //绘制连接线网络
            if (tier >= 1) {
                DrawConnectionWeb(sb, center, 330f + tier * 70f, visualAlpha, edgeColor, time);
            }

            //绘制多重几何图形（过渡期间淡入）
            DrawHexagram(sb, center, 280f + tier * 55f, 5f, Color.Lerp(darkColor, edgeColor, 0.75f) * visualAlpha * transitionEase, time * 1.1f);

            if (tier >= 1) {
                DrawPentagram(sb, center, 240f + tier * 45f, 3.5f, midColor * visualAlpha * transitionEase, -time * 1.4f);
                DrawHexagram(sb, center, 360f, 6f, darkColor * visualAlpha * transitionEase * 0.8f, time * 1.6f);
            }

            if (tier >= 2) {
                DrawPentagram(sb, center, 170f, 2.5f, coreColor * visualAlpha * transitionEase, time * 2f);
                DrawOctagon(sb, center, 420f, 4f, edgeColor * visualAlpha * transitionEase * 0.7f, -time * 1.2f);
            }

            //绘制硫磺火余烬
            DrawBrimstoneEmbers(sb);

            //绘制符文（带过渡淡入）
            if (RuneAsset?.IsLoaded ?? false) {
                for (int layer = 0; layer <= tier && layer < runeLayers.Length; layer++) {
                    DrawAnimatedRunes(sb, RuneAsset.Value, center, layer, coreColor, midColor, darkColor, transitionEase);
                }
            }

            //绘制能量球
            DrawEnergyOrbs(sb);

            //绘制闪电弧
            DrawLightningArcsVisual(sb);

            //绘制中心核心辉光
            if (GlowAsset?.IsLoaded ?? false) {
                DrawCoreGlow(sb, GlowAsset.Value, center, visualAlpha, coreColor, midColor, edgeColor, highlightColor, time);
            }

            return false;
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
            Texture2D pixel = CWRAsset.Placeholder_White.Value;

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

        private void DrawBrimstoneEmbers(SpriteBatch sb) {
            Texture2D glow = GlowAsset?.Value;
            if (glow == null) return;

            foreach (var ember in brimstoneEmbers) {
                Vector2 drawPos = ember.Position - Main.screenPosition;
                float scale = ember.Scale * ember.Alpha;

                sb.Draw(glow, drawPos, null, ember.Color with { A = 0 } * ember.Alpha * 0.8f, ember.Rotation,
                    glow.Size() / 2, scale * 0.3f, SpriteEffects.None, 0);
            }
        }

        private static void DrawCircleRing(SpriteBatch sb, Vector2 center, float radius, float thickness, Color color) {
            Texture2D pixel = CWRAsset.Placeholder_White.Value;
            int segments = (int)(radius / 10f);

            for (int i = 0; i < segments; i++) {
                float angle = MathHelper.TwoPi * i / segments;
                float nextAngle = MathHelper.TwoPi * (i + 1) / segments;

                Vector2 start = center + angle.ToRotationVector2() * radius;
                Vector2 end = center + nextAngle.ToRotationVector2() * radius;

                DrawLine(sb, pixel, start, end, thickness, color);
            }
        }

        private static void DrawOctagon(SpriteBatch sb, Vector2 center, float radius, float thickness, Color color, float rotation) {
            Texture2D pixel = CWRAsset.Placeholder_White.Value;
            DrawPolygon(sb, pixel, center, 8, radius, thickness, color, rotation);
        }

        private static void DrawVoidRing(SpriteBatch sb, Vector2 center, float radius, Color color, float alpha, float time) {
            if (!(GlowAsset?.IsLoaded ?? false)) return;

            float pulse = (float)Math.Sin(time * 2.5f) * 0.25f + 0.75f;
            float rotation = time * 0.3f;
            sb.Draw(GlowAsset.Value, center, null,
                color with { A = 0 } * alpha * 0.25f * pulse,
                rotation,
                GlowAsset.Value.Size() / 2,
                radius / GlowAsset.Value.Width * 2.2f,
                SpriteEffects.None, 0);
        }

        private static void DrawConnectionWeb(SpriteBatch sb, Vector2 center, float radius, float alpha, Color color, float time) {
            if (alpha < 0.25f) return;
            Texture2D pixel = CWRAsset.Placeholder_White.Value;

            int points = 14;
            for (int i = 0; i < points; i++) {
                float angle1 = time * 1.8f + MathHelper.TwoPi * i / points;
                Vector2 pos1 = center + angle1.ToRotationVector2() * radius;

                //连接到相邻点
                for (int j = i + 1; j < Math.Min(i + 3, points); j++) {
                    float angle2 = time * 1.8f + MathHelper.TwoPi * j / points;
                    Vector2 pos2 = center + angle2.ToRotationVector2() * radius;

                    float pulse = (float)Math.Sin(time * 8f + i + j) * 0.4f + 0.6f;
                    DrawLine(sb, pixel, pos1, pos2, 0.8f, color * alpha * 0.25f * pulse);
                }
            }
        }

        private static void DrawHexagram(SpriteBatch sb, Vector2 center, float radius, float thickness, Color color, float rotation) {
            Texture2D pixel = CWRAsset.Placeholder_White.Value;
            DrawPolygon(sb, pixel, center, 3, radius, thickness, color, rotation);
            DrawPolygon(sb, pixel, center, 3, radius, thickness, color, rotation + MathHelper.Pi);
        }

        private static void DrawPentagram(SpriteBatch sb, Vector2 center, float radius, float thickness, Color color, float rotation) {
            Texture2D pixel = CWRAsset.Placeholder_White.Value;
            int points = 5;
            Vector2[] vertices = new Vector2[points];

            for (int i = 0; i < points; i++) {
                float angle = rotation + i * MathHelper.TwoPi / points - MathHelper.PiOver2;
                vertices[i] = center + angle.ToRotationVector2() * radius;
            }

            for (int i = 0; i < points; i++) {
                DrawLine(sb, pixel, vertices[i], vertices[(i + 2) % points], thickness, color);
            }
        }

        private static void DrawPolygon(SpriteBatch sb, Texture2D pixel, Vector2 center, int sides, float radius, float thickness, Color color, float rotation) {
            if (sides < 3) return;
            Vector2 prev = center + rotation.ToRotationVector2() * radius;
            for (int i = 1; i <= sides; i++) {
                float ang = rotation + i * MathHelper.TwoPi / sides;
                Vector2 curr = center + ang.ToRotationVector2() * radius;
                DrawLine(sb, pixel, prev, curr, thickness, color);
                prev = curr;
            }
        }

        private static void DrawLine(SpriteBatch sb, Texture2D pixel, Vector2 start, Vector2 end, float thickness, Color color) {
            Vector2 diff = end - start;
            float length = diff.Length();
            if (length < 1f) return;
            sb.Draw(pixel, start, new Rectangle(0, 0, 1, 1), color, diff.ToRotation(), Vector2.Zero, new Vector2(length, thickness), SpriteEffects.None, 0f);
        }

        private void DrawAnimatedRunes(SpriteBatch sb, Texture2D runeTex, Vector2 center, int layer, Color c1, Color c2, Color c3, float transitionAlpha) {
            if (layer >= runeLayers.Length || runeLayers[layer] == null) return;

            Texture2D starTex = StarTexture?.Value;
            if (runeTex == null) return;

            //4x4火焰纹理的单帧尺寸
            int frameWidth = runeTex.Width / 4;
            int frameHeight = runeTex.Height / 4;

            foreach (var rune in runeLayers[layer]) {
                if (rune.Alpha < 0.01f) continue;

                Vector2 pos = center + rune.Offset * expandScale;

                //火焰强度脉冲（更剧烈的火焰效果）
                float intensityPulse = (float)Math.Sin(rune.IntensityPulse) * 0.3f + 0.7f;
                float fireFlicker = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 15f + rune.PulsePhase) * 0.2f + 0.8f;

                //计算火焰帧的矩形区域
                int frameX = rune.FireFrame % 4;
                int frameY = rune.FireFrame / 4;
                Rectangle fireFrame = new Rectangle(frameX * frameWidth, frameY * frameHeight, frameWidth, frameHeight);

                //硫磺火色彩渐变（黑红暗色系 - 从暗红到深黑红）
                Color baseFireColor = rune.Type switch {
                    0 => Color.Lerp(new Color(180, 60, 40), new Color(100, 30, 20), intensityPulse),   //暗橙红到深红
                    1 => Color.Lerp(new Color(200, 70, 45), new Color(120, 35, 25), intensityPulse),   //橙红到暗红
                    2 => Color.Lerp(new Color(150, 50, 35), new Color(80, 25, 18), intensityPulse),    //深红到黑红
                    3 => Color.Lerp(new Color(220, 80, 50), new Color(140, 40, 28), intensityPulse),   //亮橙红到暗橙红
                    4 => Color.Lerp(new Color(160, 55, 38), new Color(90, 28, 20), intensityPulse),    //中红到深暗红
                    _ => Color.Lerp(new Color(140, 45, 32), new Color(70, 22, 16), intensityPulse)     //默认暗红渐变
                };

                //添加黑暗基调
                float darknessBlend = 0.3f + layer * 0.1f; //外层更暗
                baseFireColor = Color.Lerp(baseFireColor, new Color(30, 10, 8), darknessBlend * (1f - intensityPulse * 0.5f));

                float layerAlpha = (1f - layer * 0.18f) * rune.Alpha * transitionAlpha;
                baseFireColor *= layerAlpha * expandScale * intensityPulse * fireFlicker;
                baseFireColor.A = 0;//加法混合

                float finalScale = rune.Scale * (0.9f + intensityPulse * 0.4f) * expandScale;

                //绘制火焰阴影底层（增强暗黑感）
                Color shadowColor = new Color(20, 8, 6) with { A = 0 } * layerAlpha * 0.8f;
                sb.Draw(runeTex, pos, fireFrame, shadowColor, rune.Rotation,
                    new Vector2(frameWidth, frameHeight) / 2f, finalScale * 1.5f, SpriteEffects.None, 0f);

                //绘制火焰主体（稍大的底层光晕）
                sb.Draw(runeTex, pos, fireFrame, baseFireColor * 0.6f, rune.Rotation,
                    new Vector2(frameWidth, frameHeight) / 2f, finalScale * 1.3f, SpriteEffects.None, 0f);

                //绘制火焰核心（较亮）
                sb.Draw(runeTex, pos, fireFrame, baseFireColor * 1.2f, rune.Rotation,
                    new Vector2(frameWidth, frameHeight) / 2f, finalScale, SpriteEffects.None, 0f);

                //绘制额外的火焰细节层（旋转角度不同，增加动感）
                sb.Draw(runeTex, pos, fireFrame, baseFireColor * 0.4f, rune.Rotation + MathHelper.PiOver4,
                    new Vector2(frameWidth, frameHeight) / 2f, finalScale * 0.8f, SpriteEffects.None, 0f);

                //绘制星星核心闪光（暗红橙色调）
                if (starTex != null && rune.CoreGlowAlpha > 0.3f) {
                    float corePulse = (float)Math.Sin(rune.PulsePhase * 2f) * 0.5f + 0.5f;
                    float coreIntensity = intensityPulse * corePulse * fireFlicker;

                    //核心暗红光（不再是白光）
                    Color coreColor = new Color(255, 90, 50) with { A = 0 } * rune.CoreGlowAlpha * coreIntensity * layerAlpha * 0.7f;
                    sb.Draw(starTex, pos, null, coreColor, rune.Rotation,
                        starTex.Size() / 2f, finalScale * 0.3f * (0.8f + corePulse * 0.4f), SpriteEffects.None, 0f);

                    //核心深橙红光
                    Color coreGlow = new Color(200, 70, 40) with { A = 0 } * rune.CoreGlowAlpha * coreIntensity * layerAlpha * 0.5f;
                    sb.Draw(starTex, pos, null, coreGlow, rune.Rotation + MathHelper.PiOver4,
                        starTex.Size() / 2f, finalScale * 0.4f * (0.7f + corePulse * 0.5f), SpriteEffects.None, 0f);

                    //外层脉冲光环（暗红色）
                    if (corePulse > 0.6f) {
                        Color pulseRing = new Color(180, 60, 35) with { A = 0 } * rune.CoreGlowAlpha * (corePulse - 0.6f) * 2f * layerAlpha * 0.3f;
                        sb.Draw(starTex, pos, null, pulseRing, rune.Rotation,
                            starTex.Size() / 2f, finalScale * 0.6f * corePulse, SpriteEffects.None, 0f);
                    }
                }
            }
        }

        private static void DrawCoreGlow(SpriteBatch sb, Texture2D glow, Vector2 center, float scale, Color c1, Color c2, Color c3, Color c4, float time) {
            float pulse1 = (float)Math.Sin(time * 13f) * 0.4f + 0.6f;
            float pulse2 = (float)Math.Sin(time * 10f + 1f) * 0.4f + 0.6f;
            float pulse3 = (float)Math.Sin(time * 16f + 2f) * 0.4f + 0.6f;

            //硫磺火核心辉光（暗红色调）
            //最外层 - 深暗红
            sb.Draw(glow, center, null, new Color(80, 25, 18) with { A = 0 } * scale * 0.5f, time * 1.8f,
                glow.Size() / 2, scale * 3.2f, SpriteEffects.None, 0);

            //中层 - 深红
            sb.Draw(glow, center, null, c2 with { A = 0 } * scale * 0.75f * pulse1, -time * 1.3f,
                glow.Size() / 2, scale * (2.4f + pulse1 * 0.4f), SpriteEffects.None, 0);

            //内层 - 橙红
            sb.Draw(glow, center, null, c1 with { A = 0 } * scale * pulse2, time * 0.9f,
                glow.Size() / 2, scale * (1.8f + pulse2 * 0.5f), SpriteEffects.None, 0);

            //高光层 - 亮橙红
            sb.Draw(glow, center, null, c4 with { A = 0 } * scale * 0.6f * pulse2, -time * 2f,
                glow.Size() / 2, scale * (1.3f + pulse2 * 0.3f), SpriteEffects.None, 0);

            //核心 - 暗橙红（不再是纯白）
            sb.Draw(glow, center, null, new Color(220, 80, 50) with { A = 0 } * scale * 0.4f * pulse3, 0,
                glow.Size() / 2, scale * 1.1f * (1f + pulse3 * 0.3f), SpriteEffects.None, 0);
        }
    }
}
