using InnoVault.GameContent.BaseEntity;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    internal class FishPrismite : FishSkill
    {
        public override int UnlockFishID => ItemID.Prismite;
        public override int DefaultCooldown => 60 - HalibutData.GetDomainLayer() * 3;

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (Cooldown > 0) {
                return null;
            }

            Vector2 shootVel = velocity.SafeNormalize(Vector2.UnitX) * 18f;

            int proj = Projectile.NewProjectile(
                source,
                position,
                shootVel,
                ModContent.ProjectileType<PrismiteWaveProjectile>(),
                (int)(damage * (1f + HalibutData.GetDomainLayer() * 0.25f)),
                knockback * 1.2f,
                player.whoAmI,
                0,
                Main.rand.Next(7)
            );

            if (proj >= 0 && proj < Main.maxProjectiles) {
                Main.projectile[proj].ai[1] = Main.rand.Next(7);
            }

            SetCooldown();
            SoundEngine.PlaySound(SoundID.Item105 with { Volume = 0.7f, Pitch = 0.3f }, position);

            return false;
        }
    }

    /// <summary>
    /// 七彩矿石冲击波弹幕
    /// </summary>
    internal class PrismiteWaveProjectile : BaseHeldProj
    {
        public override string Texture => CWRConstant.Placeholder;
        private const int MaxLifeTime = 240;

        private float scale = 1f;
        private readonly List<TrailPoint> trailPoints = new();
        private const int MaxTrailLength = 30;

        //能量粒子系统
        private readonly List<EnergyParticle> energyParticles = new();
        private int particleSpawnTimer = 0;

        //螺旋运动参数
        private float spiralPhase = 0f;
        private float spiralIntensity = 0f;
        private Vector2 baseVelocity;

        //七彩颜色方案 - 更加鲜艳的配色
        private static readonly Color[] PrismColors = new Color[]
        {
            new Color(255, 60, 120),   //深玫瑰红
            new Color(255, 150, 50),   //炽橙色
            new Color(255, 230, 60),   //金黄色
            new Color(80, 255, 120),   //翡翠绿
            new Color(60, 180, 255),   //深天蓝
            new Color(160, 80, 255),   //深紫罗兰
            new Color(255, 80, 200)    //亮品红
        };

        private Color primaryColor;
        private Color secondaryColor;
        private Color accentColor;
        private int generation;
        private int colorSeed;
        private float pulsePhase;
        private float energyWavePhase;

        //冲击波环效果
        private readonly List<ShockwaveRing> shockwaveRings = new();

        public override void SetDefaults() {
            Projectile.width = 36;
            Projectile.height = 36;
            Projectile.friendly = true;
            Projectile.penetrate = 3 + (int)(HalibutData.GetLevel() / 4f);
            Projectile.timeLeft = MaxLifeTime;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Projectile.extraUpdates = 2;
        }

        public override void AI() {
            //螺旋运动轨迹
            spiralPhase += 0.18f;
            float lifeProgress = 1f - Projectile.timeLeft / (float)MaxLifeTime;

            //根据生命周期调整螺旋强度
            if (lifeProgress < 0.2f) {
                spiralIntensity = MathHelper.Lerp(0f, 1f, lifeProgress / 0.2f);
            }
            else if (lifeProgress > 0.7f) {
                spiralIntensity = MathHelper.Lerp(1f, 0.3f, (lifeProgress - 0.7f) / 0.3f);
            }
            else {
                spiralIntensity = 1f;
            }

            //应用螺旋偏移
            Vector2 perpendicular = baseVelocity.RotatedBy(MathHelper.PiOver2).SafeNormalize(Vector2.Zero);
            float spiralOffset = (float)Math.Sin(spiralPhase) * 3f * spiralIntensity * (1f - generation * 0.3f);
            Projectile.velocity = baseVelocity * 0.99f + perpendicular * spiralOffset;
            baseVelocity = Projectile.velocity;

            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;
            pulsePhase += 0.2f;
            energyWavePhase += 0.12f;

            //记录拖尾点
            TrailPoint newPoint = new TrailPoint {
                Position = Projectile.Center,
                Velocity = Projectile.velocity,
                Scale = scale * Projectile.scale,
                Color = Color.Lerp(primaryColor, secondaryColor, (float)Math.Sin(pulsePhase) * 0.5f + 0.5f),
                TimeCreated = (int)Main.GameUpdateCount
            };
            trailPoints.Insert(0, newPoint);

            if (trailPoints.Count > MaxTrailLength) {
                trailPoints.RemoveAt(trailPoints.Count - 1);
            }

            //缩放动画 - 更有张力
            if (lifeProgress < 0.1f) {
                scale = CWRUtils.EaseOutBack(lifeProgress / 0.1f) * 1f;
            }
            else if (lifeProgress > 0.85f) {
                scale = MathHelper.Lerp(1f, 0.6f, (lifeProgress - 0.85f) / 0.15f);
            }
            else {
                float breathe = (float)Math.Sin(pulsePhase * 0.5f) * 0.15f;
                scale = 1f + breathe + lifeProgress * 0.2f;
            }

            //生成能量粒子
            particleSpawnTimer++;
            if (particleSpawnTimer >= 1) {
                SpawnEnergyParticle();
                particleSpawnTimer = 0;
            }

            //更新能量粒子
            for (int i = energyParticles.Count - 1; i >= 0; i--) {
                energyParticles[i].Update();
                if (energyParticles[i].ShouldRemove()) {
                    energyParticles.RemoveAt(i);
                }
            }

            //更新冲击波环
            for (int i = shockwaveRings.Count - 1; i >= 0; i--) {
                shockwaveRings[i].Update();
                if (shockwaveRings[i].ShouldRemove()) {
                    shockwaveRings.RemoveAt(i);
                }
            }

            //定期生成冲击波环效果
            if (Main.GameUpdateCount % 15 == 0) {
                shockwaveRings.Add(new ShockwaveRing(Projectile.Center, primaryColor, secondaryColor));
            }

            //持续粒子效果
            if (Main.rand.NextBool()) {
                SpawnTrailDust();
            }

            //增强发光
            Lighting.AddLight(Projectile.Center, primaryColor.ToVector3() * 0.8f);
        }

        public override void Initialize() {
            generation = (int)Projectile.ai[0];
            colorSeed = (int)Projectile.ai[1];

            primaryColor = PrismColors[colorSeed % PrismColors.Length];
            secondaryColor = PrismColors[(colorSeed + 2) % PrismColors.Length];
            accentColor = PrismColors[(colorSeed + 4) % PrismColors.Length];

            baseVelocity = Projectile.velocity;
            Projectile.scale = 1f - generation * 0.12f;

            //生成初始爆发粒子
            for (int i = 0; i < 12; i++) {
                float angle = MathHelper.TwoPi * i / 12f;
                energyParticles.Add(new EnergyParticle(
                    Projectile.Center,
                    angle.ToRotationVector2() * Main.rand.NextFloat(1f, 3f),
                    primaryColor,
                    1.2f
                ));
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            SplitOnImpact(Projectile.Center, Projectile.velocity);
            SpawnImpactEffect(Projectile.Center);
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            //更有力量感的反弹
            if (Math.Abs(Projectile.velocity.X - oldVelocity.X) > float.Epsilon) {
                Projectile.velocity.X = -oldVelocity.X * 0.9f;
                baseVelocity.X = Projectile.velocity.X;
            }
            if (Math.Abs(Projectile.velocity.Y - oldVelocity.Y) > float.Epsilon) {
                Projectile.velocity.Y = -oldVelocity.Y * 0.9f;
                baseVelocity.Y = Projectile.velocity.Y;
            }

            SplitOnImpact(Projectile.Center, -oldVelocity);
            SpawnImpactEffect(Projectile.Center);

            //生成冲击波环
            shockwaveRings.Add(new ShockwaveRing(Projectile.Center, primaryColor, secondaryColor, 2f));

            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.5f, Pitch = 0.3f }, Projectile.Center);

            return false;
        }

        private void SplitOnImpact(Vector2 impactPos, Vector2 impactDirection) {
            if (generation > 0 || Projectile.numHits > 0) {
                return;
            }

            int splitCount = 3 + HalibutData.GetDomainLayer() / 2;
            Vector2 baseDir = impactDirection.SafeNormalize(Vector2.UnitX);
            float spreadAngle = MathHelper.Pi * 0.8f;

            for (int i = 0; i < splitCount; i++) {
                float angle = -spreadAngle / 2f + (spreadAngle * i / (splitCount - 1));
                Vector2 splitVel = baseDir.RotatedBy(angle) * Main.rand.NextFloat(12f, 16f);

                int newColorSeed = (colorSeed + i + 1) % PrismColors.Length;

                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    impactPos,
                    splitVel,
                    Projectile.type,
                    (int)(Projectile.damage * 0.7f),
                    Projectile.knockBack * 0.75f,
                    Projectile.owner,
                    generation + 1,
                    newColorSeed
                );
            }
        }

        private void SpawnImpactEffect(Vector2 pos) {
            //强化爆炸效果
            for (int ring = 0; ring < 2; ring++) {
                for (int i = 0; i < 24; i++) {
                    float angle = MathHelper.TwoPi * i / 24f + ring * 0.13f;
                    Vector2 dustVel = angle.ToRotationVector2() * (4f + ring * 4f);

                    int dustType = Main.rand.Next(new int[] {
                        DustID.RainbowMk2,
                        DustID.PinkFairy,
                        DustID.YellowStarDust,
                        DustID.Firework_Blue
                    });

                    int dust = Dust.NewDust(pos, 1, 1, dustType, dustVel.X, dustVel.Y, 100,
                        ring == 0 ? primaryColor : secondaryColor, 1.8f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].fadeIn = 1.5f;
                }
            }

            //中心爆裂光晕
            for (int i = 0; i < 16; i++) {
                int dust = Dust.NewDust(pos, 1, 1, DustID.RainbowTorch, 0, 0, 0, Color.White, 2.5f);
                Main.dust[dust].velocity = Main.rand.NextVector2Circular(7f, 7f);
                Main.dust[dust].noGravity = true;
            }

            //星形粒子爆发
            for (int i = 0; i < 8; i++) {
                energyParticles.Add(new EnergyParticle(
                    pos,
                    Main.rand.NextVector2Circular(6f, 6f),
                    accentColor,
                    1.5f
                ));
            }
        }

        private void SpawnEnergyParticle() {
            Vector2 offset = Main.rand.NextVector2Circular(8f, 8f);
            Vector2 particleVel = -Projectile.velocity * 0.15f + Main.rand.NextVector2Circular(1f, 1f);

            energyParticles.Add(new EnergyParticle(
                Projectile.Center + offset,
                particleVel,
                Color.Lerp(primaryColor, secondaryColor, Main.rand.NextFloat()),
                0.8f + Main.rand.NextFloat(0.4f)
            ));
        }

        private void SpawnTrailDust() {
            Color dustColor = Color.Lerp(primaryColor, accentColor, (float)Math.Sin(pulsePhase * 1.5f) * 0.5f + 0.5f);
            int dustType = Main.rand.Next(new int[] { DustID.RainbowMk2, DustID.PinkFairy, DustID.FireworkFountain_Blue });

            int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height,
                dustType, 0, 0, 100, dustColor, 1.0f);
            Main.dust[dust].velocity = Projectile.velocity * 0.2f + Main.rand.NextVector2Circular(0.5f, 0.5f);
            Main.dust[dust].noGravity = true;
            Main.dust[dust].fadeIn = 1.1f;
        }

        public override bool PreDraw(ref Color lightColor) {
            DrawShockwaveRings();
            DrawEnergyWave();
            DrawTrail();
            DrawEnergyParticles();
            DrawPrismiteCore();
            return false;
        }

        private void DrawShockwaveRings() {
            foreach (var ring in shockwaveRings) {
                ring.Draw();
            }
        }

        private void DrawEnergyWave() {
            //绘制能量波纹效果
            Texture2D glowTex = CWRAsset.StarTexture.Value;
            float waveProgress = energyWavePhase % MathHelper.TwoPi / MathHelper.TwoPi;
            float waveScale = 0.3f + waveProgress * 0.8f;
            float waveAlpha = (1f - waveProgress) * 0.4f;

            Color waveColor = Color.Lerp(primaryColor, secondaryColor, waveProgress) * waveAlpha;
            waveColor.A = 0;

            Main.spriteBatch.Draw(
                glowTex,
                Projectile.Center - Main.screenPosition,
                null,
                waveColor,
                energyWavePhase,
                glowTex.Size() / 2f,
                waveScale * scale * Projectile.scale,
                SpriteEffects.None,
                0f
            );
        }

        private void DrawTrail() {
            if (trailPoints.Count < 2) {
                return;
            }

            Texture2D trailTex = VaultAsset.placeholder2.Value;
            Texture2D glowTex = CWRAsset.StarTexture.Value;

            for (int i = 0; i < trailPoints.Count - 1; i++) {
                float progress = i / (float)trailPoints.Count;
                float nextProgress = (i + 1) / (float)trailPoints.Count;

                TrailPoint current = trailPoints[i];
                TrailPoint next = trailPoints[i + 1];

                Vector2 diff = next.Position - current.Position;
                float length = diff.Length();

                if (length < 0.1f) continue;

                float trailRotation = diff.ToRotation();

                //渐变宽度 - 头部宽，尾部窄
                float width = MathHelper.Lerp(16f, 4f, progress) * current.Scale;

                //三层拖尾绘制
                //1. 外层辉光
                Color outerColor = Color.Lerp(current.Color, secondaryColor, 0.5f) * (1f - progress) * 0.6f;
                outerColor.A = 0;
                Main.spriteBatch.Draw(
                    trailTex,
                    current.Position - Main.screenPosition,
                    new Rectangle(0, 0, 1, 1),
                    outerColor,
                    trailRotation,
                    Vector2.Zero,
                    new Vector2(length, width * 1.8f),
                    SpriteEffects.None,
                    0f
                );

                //2. 中层主体
                Color midColor = current.Color * (1f - progress * 0.7f);
                midColor.A = 0;
                Main.spriteBatch.Draw(
                    trailTex,
                    current.Position - Main.screenPosition,
                    new Rectangle(0, 0, 1, 1),
                    midColor,
                    trailRotation,
                    Vector2.Zero,
                    new Vector2(length, width * 1.2f),
                    SpriteEffects.None,
                    0f
                );

                //3. 内层高光
                Color innerColor = Color.Lerp(Color.White, current.Color, 0.3f) * (1f - progress) * 0.9f;
                innerColor.A = 0;
                Main.spriteBatch.Draw(
                    trailTex,
                    current.Position - Main.screenPosition,
                    new Rectangle(0, 0, 1, 1),
                    innerColor,
                    trailRotation,
                    Vector2.Zero,
                    new Vector2(length, width * 0.6f),
                    SpriteEffects.None,
                    0f
                );

                //4. 星点装饰
                if (i % 3 == 0 && progress < 0.7f) {
                    float sparkScale = (1f - progress) * 0.15f * current.Scale;
                    Color sparkColor = Color.Lerp(accentColor, Color.White, 0.5f) * (1f - progress);
                    sparkColor.A = 0;
                    Main.spriteBatch.Draw(
                        glowTex,
                        current.Position - Main.screenPosition,
                        null,
                        sparkColor,
                        Main.GlobalTimeWrappedHourly * 3f + i,
                        glowTex.Size() / 2f,
                        sparkScale,
                        SpriteEffects.None,
                        0f
                    );
                }
            }
        }

        private void DrawEnergyParticles() {
            Texture2D particleTex = CWRAsset.StarTexture.Value;

            foreach (var particle in energyParticles) {
                particle.Draw(particleTex);
            }
        }

        private void DrawPrismiteCore() {
            Main.instance.LoadItem(ItemID.Prismite);
            Texture2D prismTex = Terraria.GameContent.TextureAssets.Item[ItemID.Prismite].Value;

            float pulse = (float)Math.Sin(pulsePhase) * 0.5f + 0.5f;
            float drawScale = scale * Projectile.scale * 0.9f;

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Rectangle sourceRect = prismTex.Bounds;
            Vector2 origin = sourceRect.Size() * 0.5f;

            //外层能量环
            for (int i = 0; i < 4; i++) {
                float ringRotation = Projectile.rotation + i * MathHelper.PiOver2 + energyWavePhase;
                float ringScale = drawScale * (1.6f + i * 0.2f + pulse * 0.3f);
                Color ringColor = Color.Lerp(secondaryColor, accentColor, i / 4f) * (0.35f - i * 0.07f);
                ringColor.A = 0;

                Main.spriteBatch.Draw(
                    prismTex,
                    drawPos,
                    sourceRect,
                    ringColor,
                    ringRotation,
                    origin,
                    ringScale,
                    SpriteEffects.None,
                    0f
                );
            }

            //中层主体 - 双层渲染
            Color mainColor = primaryColor * 0.95f;
            mainColor.A = 0;
            Main.spriteBatch.Draw(
                prismTex,
                drawPos,
                sourceRect,
                mainColor,
                Projectile.rotation,
                origin,
                drawScale * 1.3f,
                SpriteEffects.None,
                0f
            );

            //内层亮色
            Color brightColor = Color.Lerp(primaryColor, Color.White, 0.4f) * 0.8f;
            brightColor.A = 0;
            Main.spriteBatch.Draw(
                prismTex,
                drawPos,
                sourceRect,
                brightColor,
                Projectile.rotation * 0.8f,
                origin,
                drawScale * 1.0f,
                SpriteEffects.None,
                0f
            );

            //核心白色高光
            Main.spriteBatch.Draw(
                prismTex,
                drawPos,
                sourceRect,
                Color.White with { A = 0 } * (0.7f + pulse * 0.3f),
                Projectile.rotation * 0.5f,
                origin,
                drawScale * 0.75f,
                SpriteEffects.None,
                0f
            );

            //顶层星光爆发
            Texture2D starTex = CWRAsset.StarTexture.Value;
            float starIntensity = (float)Math.Pow(pulse, 2);
            if (starIntensity > 0.4f) {
                float starScale = drawScale * (starIntensity - 0.4f) * 3.5f;
                Color starColor = Color.Lerp(primaryColor, Color.White, starIntensity) * 0.7f;
                starColor.A = 0;

                //十字星光
                for (int i = 0; i < 2; i++) {
                    Main.spriteBatch.Draw(
                        starTex,
                        drawPos,
                        null,
                        starColor,
                        i * MathHelper.PiOver2 + Main.GlobalTimeWrappedHourly * 2f,
                        starTex.Size() / 2f,
                        starScale * (i == 0 ? 1f : 0.7f),
                        SpriteEffects.None,
                        0f
                    );
                }
            }
        }
    }

    #region 辅助数据结构

    internal struct TrailPoint
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Scale;
        public Color Color;
        public int TimeCreated;
    }

    internal class EnergyParticle
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public Color Color;
        public float Scale;
        public float Life;
        public float MaxLife;
        public float Rotation;

        public EnergyParticle(Vector2 pos, Vector2 vel, Color color, float scale) {
            Position = pos;
            Velocity = vel;
            Color = color;
            Scale = scale;
            Life = 0f;
            MaxLife = Main.rand.NextFloat(30f, 60f);
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
        }

        public void Update() {
            Life++;
            Position += Velocity;
            Velocity *= 0.95f;
            Rotation += 0.1f;
        }

        public bool ShouldRemove() => Life >= MaxLife;

        public void Draw(Texture2D texture) {
            float progress = Life / MaxLife;
            float alpha = (float)Math.Sin((1f - progress) * MathHelper.PiOver2);
            Color drawColor = Color * alpha;
            drawColor.A = 0;

            float drawScale = Scale * (1f - progress * 0.5f) * 0.15f;

            Main.spriteBatch.Draw(
                texture,
                Position - Main.screenPosition,
                null,
                drawColor,
                Rotation,
                texture.Size() / 2f,
                drawScale,
                SpriteEffects.None,
                0f
            );
        }
    }

    internal class ShockwaveRing
    {
        public Vector2 Center;
        public Color InnerColor;
        public Color OuterColor;
        public float Radius;
        public float MaxRadius;
        public float Life;
        public float MaxLife;
        public float Thickness;

        public ShockwaveRing(Vector2 center, Color inner, Color outer, float speedMultiplier = 1f) {
            Center = center;
            InnerColor = inner;
            OuterColor = outer;
            Radius = 0f;
            MaxRadius = 120f * speedMultiplier;
            Life = 0f;
            MaxLife = 30f / speedMultiplier;
            Thickness = 8f;
        }

        public void Update() {
            Life++;
            float progress = Life / MaxLife;
            Radius = CWRUtils.EaseOutCubic(progress) * MaxRadius;
        }

        public bool ShouldRemove() => Life >= MaxLife;

        public void Draw() {
            float progress = Life / MaxLife;
            float alpha = (float)Math.Sin((1f - progress) * MathHelper.PiOver2) * 0.6f;

            Texture2D pixel = VaultAsset.placeholder2.Value;
            int segments = 48;
            float angleStep = MathHelper.TwoPi / segments;

            for (int i = 0; i < segments; i++) {
                float angle1 = i * angleStep;
                float angle2 = (i + 1) * angleStep;

                Vector2 p1 = Center + angle1.ToRotationVector2() * Radius;
                Vector2 p2 = Center + angle2.ToRotationVector2() * Radius;

                Vector2 diff = p2 - p1;
                float length = diff.Length();
                if (length < 0.01f) continue;

                float rotation = diff.ToRotation();

                //渐变色
                float colorProgress = i / (float)segments;
                Color segmentColor = Color.Lerp(InnerColor, OuterColor, colorProgress) * alpha;
                segmentColor.A = 0;

                Main.spriteBatch.Draw(
                    pixel,
                    p1 - Main.screenPosition,
                    new Rectangle(0, 0, 1, 1),
                    segmentColor,
                    rotation,
                    Vector2.Zero,
                    new Vector2(length, Thickness * (1f + (float)Math.Sin(angle1 * 4f + Life * 0.3f) * 0.3f)),
                    SpriteEffects.None,
                    0f
                );
            }
        }
    }

    #endregion
}
