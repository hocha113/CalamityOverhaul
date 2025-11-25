using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.AriaofTheCosmoses
{
    /// <summary>
    /// 寰宇咏叹调R技能 - 伽马暴击
    /// 向鼠标方向释放多道伽马射线束，造成大范围毁灭性伤害
    /// </summary>
    internal class AriaRSkill : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        private const int ChargeTime = 60; // 1秒蓄力
        private const int FireTime = 90; // 1.5秒持续时间
        private const int BeamCount = 9; // 射线数量
        
        private List<int> beamIndices = new();
        private float chargeProgress;
        private bool isFiring;

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = ChargeTime + FireTime;
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

            // 跟随玩家
            Projectile.Center = player.Center;

            // 蓄力阶段
            if (Projectile.timeLeft > FireTime) {
                ChargePhase(player);
            }
            // 发射阶段
            else if (!isFiring) {
                FirePhase(player);
                isFiring = true;
            }
            // 维持阶段
            else {
                MaintainPhase(player);
            }
        }

        private void ChargePhase(Player player) {
            int currentTime = ChargeTime - (Projectile.timeLeft - FireTime);
            chargeProgress = MathHelper.Clamp(currentTime / (float)ChargeTime, 0f, 1f);

            // 蓄力音效
            if (currentTime == 1) {
                SoundEngine.PlaySound(SoundID.DD2_DarkMageHealImpact with { 
                    Volume = 0.8f, 
                    Pitch = -0.3f 
                }, Projectile.Center);
            }
            else if (currentTime == ChargeTime / 2) {
                SoundEngine.PlaySound(SoundID.DD2_WitherBeastAuraPulse with { 
                    Volume = 0.9f, 
                    Pitch = 0f 
                }, Projectile.Center);
            }

            // 蓄力粒子
            if (currentTime % 2 == 0) {
                SpawnChargeParticles(player);
            }

            // 蓄力能量环
            if (currentTime % 8 == 0) {
                SpawnChargeRing(player);
            }

            // 屏幕震动
            if (chargeProgress > 0.5f) {
                player.GetModPlayer<CWRPlayer>().GetScreenShake(chargeProgress * 3f);
            }

            // 发光效果
            float lightIntensity = chargeProgress * 1.5f;
            Lighting.AddLight(Projectile.Center, 
                new Vector3(0.3f, 0.6f, 1f) * lightIntensity);
        }

        private void FirePhase(Player player) {
            // 播放发射音效
            SoundEngine.PlaySound(SoundID.Item109 with { 
                Volume = 1.2f, 
                Pitch = 0.5f 
            }, Projectile.Center);
            
            SoundEngine.PlaySound(SoundID.DD2_ExplosiveTrapExplode with { 
                Volume = 1f, 
                Pitch = 0.3f 
            }, Projectile.Center);

            // 强烈屏幕震动
            player.GetModPlayer<CWRPlayer>().GetScreenShake(15f);

            // 创建多道伽马射线
            Vector2 mouseDirection = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.Zero);
            float spreadAngle = MathHelper.ToRadians(45f); // 45度扩散角

            for (int i = 0; i < BeamCount; i++) {
                float angleOffset = MathHelper.Lerp(-spreadAngle / 2, spreadAngle / 2, i / (float)(BeamCount - 1));
                Vector2 beamDirection = mouseDirection.RotatedBy(angleOffset);

                int beamIndex = Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    player.Center,
                    beamDirection * 20f,
                    ModContent.ProjectileType<AriaRSkillBeam>(),
                    (int)(Projectile.damage * 1.2f),
                    Projectile.knockBack * 1.5f,
                    Projectile.owner,
                    i / (float)BeamCount
                );

                beamIndices.Add(beamIndex);
            }

            // 发射特效
            SpawnFireEffect(player, mouseDirection);
        }

        private void MaintainPhase(Player player) {
            // 维持射线存活
            foreach (int beamIndex in beamIndices) {
                if (beamIndex >= 0 && Main.projectile[beamIndex].active) {
                    if (Main.projectile[beamIndex].timeLeft < 10)
                    Main.projectile[beamIndex].timeLeft = 10;
                }
            }

            // 持续粒子效果
            if (Projectile.timeLeft % 3 == 0) {
                SpawnMaintainParticles(player);
            }

            // 发光
            Lighting.AddLight(Projectile.Center, new Vector3(0.5f, 0.8f, 1.2f) * 1.2f);

            // 淡出效果
            if (Projectile.timeLeft < 20) {
                float fadeProgress = Projectile.timeLeft / 20f;
                foreach (int beamIndex in beamIndices) {
                    if (beamIndex >= 0 && Main.projectile[beamIndex].active) {
                        Main.projectile[beamIndex].alpha = (int)(255 * (1f - fadeProgress));
                    }
                }
            }
        }

        private void SpawnChargeParticles(Player player) {
            if (VaultUtils.isServer) {
                return;
            }

            int particleCount = (int)(3 + chargeProgress * 5);
            for (int i = 0; i < particleCount; i++) {
                Vector2 particlePos = player.Center + Main.rand.NextVector2Circular(80, 80);
                Vector2 particleVel = (player.Center - particlePos).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(3f, 8f);

                Color particleColor = Color.Lerp(Color.Cyan, Color.White, chargeProgress);

                BasePRT particle = new PRT_AccretionDiskImpact(
                    particlePos,
                    particleVel,
                    particleColor,
                    Main.rand.NextFloat(0.4f, 0.8f),
                    Main.rand.Next(15, 25),
                    Main.rand.NextFloat(-0.2f, 0.2f),
                    false,
                    Main.rand.NextFloat(0.15f, 0.25f)
                );
                PRTLoader.AddParticle(particle);
            }
        }

        private void SpawnChargeRing(Player player) {
            if (VaultUtils.isServer) {
                return;
            }

            int segments = 48;
            float radius = 40 + chargeProgress * 80;

            for (int i = 0; i < segments; i++) {
                float angle = MathHelper.TwoPi * i / segments;
                Vector2 offset = angle.ToRotationVector2() * radius;
                Vector2 particlePos = player.Center + offset;
                Vector2 particleVel = offset.SafeNormalize(Vector2.Zero) * 2.5f;

                BasePRT particle = new PRT_AccretionDiskImpact(
                    particlePos,
                    particleVel,
                    Color.Lerp(Color.Cyan, Color.DeepSkyBlue, chargeProgress),
                    Main.rand.NextFloat(0.5f, 1f),
                    Main.rand.Next(20, 30),
                    Main.rand.NextFloat(-0.3f, 0.3f),
                    false,
                    Main.rand.NextFloat(0.18f, 0.28f)
                );
                PRTLoader.AddParticle(particle);
            }
        }

        private void SpawnFireEffect(Player player, Vector2 direction) {
            if (VaultUtils.isServer) {
                return;
            }

            // 爆发粒子
            for (int i = 0; i < 100; i++) {
                float angle = MathHelper.TwoPi * i / 100f;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(8f, 20f);

                BasePRT particle = new PRT_GammaImpact(
                    player.Center,
                    velocity,
                    Color.Lerp(Color.Cyan, Color.White, Main.rand.NextFloat()),
                    Main.rand.NextFloat(0.8f, 1.5f),
                    Main.rand.Next(30, 50),
                    Main.rand.NextFloat(-0.5f, 0.5f),
                    false,
                    0.3f
                );
                PRTLoader.AddParticle(particle);
            }

            // 方向性冲击波
            for (int ring = 0; ring < 3; ring++) {
                int segments = 32;
                float radius = 50f + ring * 80f;

                for (int i = 0; i < segments; i++) {
                    float angle = MathHelper.TwoPi * i / segments;
                    Vector2 offset = angle.ToRotationVector2() * radius;
                    Vector2 particlePos = player.Center + offset;
                    Vector2 particleVel = offset.SafeNormalize(Vector2.Zero) * 5f;

                    BasePRT particle = new PRT_AccretionDiskImpact(
                        particlePos,
                        particleVel,
                        new Color(100, 200, 255),
                        Main.rand.NextFloat(0.7f, 1.3f),
                        Main.rand.Next(35, 50),
                        Main.rand.NextFloat(-0.4f, 0.4f),
                        false,
                        Main.rand.NextFloat(0.25f, 0.35f)
                    );
                    PRTLoader.AddParticle(particle);
                }
            }
        }

        private void SpawnMaintainParticles(Player player) {
            if (VaultUtils.isServer) {
                return;
            }

            for (int i = 0; i < 3; i++) {
                Vector2 particlePos = player.Center + Main.rand.NextVector2Circular(40, 40);
                Vector2 particleVel = Main.rand.NextVector2Circular(3f, 3f);

                BasePRT particle = new PRT_Spark(
                    particlePos,
                    particleVel,
                    false,
                    Main.rand.Next(10, 18),
                    Main.rand.NextFloat(0.8f, 1.2f),
                    Color.Cyan,
                    player
                );
                PRTLoader.AddParticle(particle);
            }
        }

        public override void OnKill(int timeLeft) {
            // 清理所有射线
            foreach (int beamIndex in beamIndices) {
                if (beamIndex >= 0 && Main.projectile[beamIndex].active) {
                    Main.projectile[beamIndex].Kill();
                }
            }

            // 消失特效
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item62 with { 
                    Volume = 0.7f, 
                    Pitch = 0.4f 
                }, Projectile.Center);

                for (int i = 0; i < 40; i++) {
                    Vector2 velocity = Main.rand.NextVector2Circular(10f, 10f);
                    BasePRT particle = new PRT_GammaImpact(
                        Projectile.Center,
                        velocity,
                        Color.Cyan,
                        Main.rand.NextFloat(0.5f, 1f),
                        Main.rand.Next(25, 40),
                        Main.rand.NextFloat(-0.4f, 0.4f),
                        true,
                        0.25f
                    );
                    PRTLoader.AddParticle(particle);
                }
            }
        }
    }

    /// <summary>
    /// R技能的伽马射线束
    /// </summary>
    internal class AriaRSkillBeam : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        private const int MaxTrailLength = 30;
        private float beamWidth = 35f;
        private float maxBeamWidth = 80f;
        private float beamLength = 0f;
        private float maxBeamLength = 2800f;

        private float pulseIntensity = 1f;
        private float coreIntensity = 1f;
        private float distortionStrength = 0.2f;

        public ref float BeamIndex => ref Projectile.ai[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = MaxTrailLength;
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 3000;
        }

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.alpha = 0;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 1;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float p = 0f;
            return Collision.CheckAABBvLineCollision(
                targetHitbox.TopLeft(), 
                targetHitbox.Size(), 
                Projectile.Center, 
                Projectile.Center + Projectile.rotation.ToRotationVector2() * beamLength, 
                beamWidth * 1.2f, 
                ref p);
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            Projectile.position -= Projectile.velocity;

            float lifeRatio = 1f - Projectile.timeLeft / 300f;

            //展开阶段
            if (lifeRatio < 0.15f) {
                float expandProgress = lifeRatio / 0.15f;
                beamWidth = MathHelper.Lerp(6f, maxBeamWidth, CWRUtils.EaseOutCubic(expandProgress));
                beamLength = MathHelper.Lerp(0f, maxBeamLength, CWRUtils.EaseOutQuad(expandProgress));
                coreIntensity = MathHelper.Lerp(0.8f, 2f, expandProgress);
            }
            //收缩阶段
            else if (lifeRatio > 0.85f) {
                float collapseProgress = (lifeRatio - 0.85f) / 0.15f;
                beamWidth = MathHelper.Lerp(maxBeamWidth, 6f, CWRUtils.EaseInQuad(collapseProgress));
                coreIntensity = MathHelper.Lerp(2f, 0f, collapseProgress);
            }
            //稳定阶段
            else {
                beamWidth = maxBeamWidth;
                beamLength = maxBeamLength;

                float pulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 10f) * 0.15f + 0.85f;
                pulseIntensity = pulse;
                coreIntensity = 1.8f + pulse * 0.4f;
            }

            // 生成能量粒子
            SpawnEnergyParticles();

            // 发光效果
            Lighting.AddLight(Projectile.Center,
                0.3f * coreIntensity,
                0.8f * coreIntensity,
                1.2f * coreIntensity);

            // 音效
            if (Projectile.timeLeft % 40 == 0) {
                SoundEngine.PlaySound(SoundID.Item15 with {
                    Volume = 0.35f,
                    Pitch = 0.7f,
                    SoundLimitBehavior = SoundLimitBehavior.ReplaceOldest
                }, Projectile.Center);
            }

            Vector2 toMus = Main.player[Projectile.owner].Center.To(Main.MouseWorld).UnitVector();
            Projectile.Center = Main.player[Projectile.owner].Center;
            if (Projectile.localAI[0] == 0) {
                Projectile.localAI[0] = Projectile.rotation - toMus.ToRotation();
            }
            Projectile.rotation = toMus.ToRotation() + Projectile.localAI[0];
        }

        private void SpawnEnergyParticles() {
            if (VaultUtils.isServer || Projectile.timeLeft % 2 != 0) {
                return;
            }

            // 星光闪烁
            if (Main.rand.NextBool(4)) {
                Vector2 sparkPos = Projectile.Center + Main.rand.NextVector2Circular(beamWidth * 0.5f, beamWidth * 0.5f);
                Vector2 sparkVel = Main.rand.NextVector2Circular(1.5f, 1.5f);
                
                BasePRT spark = new PRT_Spark(
                    sparkPos + sparkVel * 10,
                    sparkVel,
                    false,
                    Main.rand.Next(6, 10),
                    Main.rand.NextFloat(1f, 1.5f),
                    Color.White,
                    Main.player[Projectile.owner]
                );
                PRTLoader.AddParticle(spark);
            }

            // 能量流线
            if (Main.rand.NextBool(3)) {
                Vector2 lineStart = Projectile.Center + Main.rand.NextVector2Circular(beamWidth * 0.4f, beamWidth * 0.4f);
                Vector2 lineVel = Projectile.rotation.ToRotationVector2() * Main.rand.NextFloat(6f, 12f);
                
                BasePRT line = new PRT_Line(
                    lineStart + lineVel * 10,
                    lineVel,
                    false,
                    Main.rand.Next(5, 8),
                    Main.rand.NextFloat(0.6f, 1.2f),
                    Color.Lerp(Color.Cyan, new Color(180, 230, 255), Main.rand.NextFloat())
                );
                PRTLoader.AddParticle(line);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Projectile.numHits % 5 != 0) {
                return;
            }

            // 击中音效
            SoundEngine.PlaySound(SoundID.Item94 with {
                Volume = 0.4f,
                Pitch = 0.5f
            }, target.Center);

            if (!VaultUtils.isServer) {
                // 伽马冲击粒子
                for (int i = 0; i < 10; i++) {
                    float angle = MathHelper.TwoPi * i / 10f;
                    Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(5f, 12f);

                    BasePRT impactBurst = new PRT_GammaImpact(
                        target.Center,
                        velocity,
                        Color.Lerp(Color.Cyan, Color.White, Main.rand.NextFloat()),
                        Main.rand.NextFloat(0.8f, 1.5f),
                        Main.rand.Next(25, 40),
                        Main.rand.NextFloat(-0.4f, 0.4f),
                        false,
                        0.3f
                    );
                    PRTLoader.AddParticle(impactBurst);
                }

                // 光芒粒子
                for (int i = 0; i < 12; i++) {
                    Vector2 velocity = Main.rand.NextVector2Circular(25f, 25f);

                    BasePRT light = new PRT_Light(
                        target.Center,
                        velocity,
                        Main.rand.NextFloat(1f, 1.8f),
                        Color.Lerp(Color.Cyan, Color.White, Main.rand.NextFloat()),
                        Main.rand.Next(30, 45),
                        1.8f,
                        2.5f,
                        hueShift: 0.03f
                    );
                    PRTLoader.AddParticle(light);
                }
            }

            // 穿透伤害衰减
            Projectile.damage = (int)(Projectile.damage * 0.92f);
        }

        public override void OnKill(int timeLeft) {
            // 消失爆炸效果
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item62 with {
                    Volume = 0.6f,
                    Pitch = 0.4f
                }, Projectile.Center);

                // 放射状粒子爆发
                for (int i = 0; i < 30; i++) {
                    float angle = MathHelper.TwoPi * i / 30f;
                    Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(8f, 16f);

                    PRT_GammaImpact burst = new PRT_GammaImpact(
                        Projectile.Center,
                        velocity,
                        Color.Lerp(Color.Cyan, Color.White, Main.rand.NextFloat()),
                        Main.rand.NextFloat(0.6f, 1f),
                        Main.rand.Next(35, 50),
                        Main.rand.NextFloat(-0.5f, 0.5f),
                        false,
                        0.35f
                    );
                    burst.inOwner = Main.player[Projectile.owner].whoAmI;
                    PRTLoader.AddParticle(burst);
                }

                // 内爆收缩粒子
                for (int i = 0; i < 20; i++) {
                    Vector2 spawnPos = Projectile.Center + Main.rand.NextVector2Circular(120f, 120f);
                    Vector2 velocity = (Projectile.Center - spawnPos).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(12f, 22f);

                    BasePRT implosion = new PRT_Spark(
                        spawnPos,
                        velocity,
                        false,
                        Main.rand.Next(25, 35),
                        Main.rand.NextFloat(1.2f, 2f),
                        Color.White,
                        Main.player[Projectile.owner]
                    );
                    PRTLoader.AddParticle(implosion);
                }
            }
        }

        public override Color? GetAlpha(Color lightColor) {
            float colorShift = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 8f + BeamIndex * MathHelper.Pi) * 0.5f + 0.5f;
            return Color.Lerp(Color.Cyan, Color.White, colorShift * coreIntensity);
        }

        public override bool PreDraw(ref Color lightColor) {
            DrawGammaBeam();
            return false;
        }

        private void DrawGammaBeam() {
            if (VaultUtils.isServer) {
                return;
            }

            SpriteBatch sb = Main.spriteBatch;

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Effect shader = EffectLoader.GammaRayBeam.Value;

            shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly + BeamIndex);
            shader.Parameters["uOpacity"]?.SetValue(1f - Projectile.alpha / 255f);
            shader.Parameters["uIntensity"]?.SetValue(pulseIntensity);
            shader.Parameters["uBeamWidth"]?.SetValue(beamWidth);
            shader.Parameters["uBeamLength"]?.SetValue(beamLength);
            shader.Parameters["uPulseSpeed"]?.SetValue(6f);
            shader.Parameters["uDistortionStrength"]?.SetValue(distortionStrength);
            shader.Parameters["uCoreIntensity"]?.SetValue(coreIntensity);

            shader.Parameters["uImage1"]?.SetValue(CWRAsset.Extra_193.Value);
            shader.Parameters["uImage2"]?.SetValue(CWRAsset.StarTexture.Value);
            shader.Parameters["uImage3"]?.SetValue(CWRAsset.Placeholder_White.Value);

            shader.CurrentTechnique.Passes["GammaRayPass"].Apply();

            Texture2D beamTexture = CWRAsset.Placeholder_White.Value;
            Vector2 beamOrigin = new Vector2(0, beamTexture.Height / 2f);
            Vector2 beamScale = new Vector2(beamLength / beamTexture.Width, beamWidth / beamTexture.Height);

            sb.Draw(
                beamTexture,
                Projectile.Center - Main.screenPosition,
                null,
                new Color(100, 200, 255) * (1f - Projectile.alpha / 255f),
                Projectile.rotation,
                beamOrigin,
                beamScale,
                SpriteEffects.None,
                0f
            );

            DrawCoreHighlight(sb);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }

        private void DrawCoreHighlight(SpriteBatch sb) {
            Texture2D glowTexture = CWRAsset.StarTexture.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            for (int i = 0; i < 15; i++) {
                float scale = (beamWidth / glowTexture.Width) * (1.4f - i * 0.25f) * coreIntensity;
                float alpha = (1f - i * 0.35f) * pulseIntensity;

                Color glowColor = Color.Lerp(
                    new Color(100, 220, 255), 
                    new Color(150, 180, 255), 
                    i / 5f) * alpha;

                sb.Draw(
                    glowTexture,
                    drawPos,
                    null,
                    glowColor,
                    Projectile.rotation,
                    new Vector2(0, glowTexture.Height / 2f),
                    new Vector2(beamLength / glowTexture.Width * 0.9f, scale),
                    SpriteEffects.None,
                    0f
                );
            }
        }
    }
}
