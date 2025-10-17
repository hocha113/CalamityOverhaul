using CalamityMod;
using CalamityMod.Projectiles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    /// <summary>
    /// 海洋洪流子弹 - 具有真实液体物理和海洋生物特效的弹幕
    /// </summary>
    internal class TorrentialBullet : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        // 液体状态
        private enum OceanState
        {
            Streaming,      // 洪流状态
            Splashing,      // 飞溅状态
            Dispersing      // 消散状态
        }

        private OceanState State {
            get => (OceanState)Projectile.ai[0];
            set => Projectile.ai[0] = (float)value;
        }

        private ref float StreamLife => ref Projectile.ai[1];

        // 海洋液体粒子系统
        private readonly List<OceanDroplet> waterDroplets = new();
        private const int MaxDroplets = 120;

        // 海洋泡沫粒子
        private readonly List<SeaFoam> foamParticles = new();
        private const int MaxFoam = 80;

        // 海洋生物粒子（鱼群、海藻）
        private readonly List<MarineLife> marineLifeParticles = new();
        private const int MaxMarineLife = 15;

        // 核心水流拖尾
        private readonly List<Vector2> coreTrail = new();
        private const int MaxCoreTrail = 20;

        // 物理参数
        private const float WaterViscosity = 0.985f;
        private const float Gravity = 0.28f;
        private const float SurfaceTension = 0.18f;
        private const float WaterDensity = 1.0f;
        private const float BuoyancyForce = -0.05f;

        // 视觉效果
        private float glowPulse = 0f;
        private float wavePhase = 0f;
        private int particleSpawnCounter = 0;

        // 海洋颜色主题
        private static readonly Color DeepOcean = new Color(15, 50, 90);
        private static readonly Color ShallowOcean = new Color(40, 120, 180);
        private static readonly Color OceanFoam = new Color(200, 230, 255);
        private static readonly Color BioluminescentBlue = new Color(80, 180, 255);

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.alpha = 0;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 240;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
            Projectile.arrow = true;
            Projectile.Calamity().pointBlankShotDuration = CalamityGlobalProjectile.DefaultPointBlankDuration;
        }

        public override void AI() {
            StreamLife++;

            // 状态机
            switch (State) {
                case OceanState.Streaming:
                    StreamingPhaseAI();
                    break;
                case OceanState.Splashing:
                    SplashingPhaseAI();
                    break;
                case OceanState.Dispersing:
                    DispersingPhaseAI();
                    break;
            }

            // 更新所有粒子系统
            UpdateCoreTrail();
            UpdateWaterDroplets();
            UpdateFoamParticles();
            UpdateMarineLife();

            // 动画效果
            glowPulse = (float)Math.Sin(StreamLife * 0.2f) * 0.4f + 0.6f;
            wavePhase += 0.15f;
            if (wavePhase > MathHelper.TwoPi) wavePhase -= MathHelper.TwoPi;

            // 海洋蓝色照明
            float lightIntensity = MathHelper.Lerp(0.4f, 0.9f, glowPulse);
            Lighting.AddLight(Projectile.Center,
                0.3f * lightIntensity,
                0.7f * lightIntensity,
                1.2f * lightIntensity);

            // 水下音效
            if (StreamLife % 35 == 0 && State == OceanState.Streaming) {
                SoundEngine.PlaySound(SoundID.Splash with {
                    Volume = 0.3f,
                    Pitch = Main.rand.NextFloat(-0.3f, 0.1f)
                }, Projectile.Center);
            }
        }

        // 洪流状态AI
        private void StreamingPhaseAI() {
            // 应用重力和水流浮力
            Projectile.velocity.Y += Gravity * WaterDensity / 2f;
            Projectile.velocity.Y += BuoyancyForce; // 模拟浮力

            // 粘性阻力
            //Projectile.velocity *= WaterViscosity;

            // 生成水滴粒子
            if (particleSpawnCounter++ % 2 == 0 && waterDroplets.Count < MaxDroplets) {
                SpawnWaterDroplet();
            }

            // 生成泡沫粒子
            if (StreamLife % 3 == 0 && foamParticles.Count < MaxFoam) {
                SpawnFoamParticle();
            }

            // 周期性生成海洋生物粒子
            if (StreamLife % 25 == 0 && marineLifeParticles.Count < MaxMarineLife) {
                SpawnMarineLife();
            }

            // 水流波纹效果
            if (StreamLife % 6 == 0) {
                SpawnWaterRipple();
            }

            // 转换到飞溅状态
            if (StreamLife > 180 || Projectile.velocity.Length() < 1.5f) {
                EnterSplashState();
            }

            // 旋转效果（模拟水流旋涡）
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
        }

        // 飞溅状态AI
        private void SplashingPhaseAI() {
            Projectile.velocity *= 0.92f;
            Projectile.alpha += 10;

            if (Projectile.alpha >= 200) {
                State = OceanState.Dispersing;
            }

            // 继续生成少量粒子
            if (StreamLife % 4 == 0 && waterDroplets.Count < MaxDroplets / 2) {
                SpawnWaterDroplet();
            }
        }

        // 消散状态AI
        private void DispersingPhaseAI() {
            Projectile.alpha += 20;
            if (Projectile.alpha >= 255) {
                Projectile.Kill();
            }

            Projectile.velocity *= 0.85f;
        }

        // 生成水滴粒子
        private void SpawnWaterDroplet() {
            Vector2 baseVel = Projectile.velocity;
            Vector2 particleVel = baseVel + Main.rand.NextVector2Circular(3f, 3f);

            // 添加波动效果
            float waveOffset = (float)Math.Sin(wavePhase + waterDroplets.Count * 0.3f) * 0.5f;
            particleVel += Projectile.velocity.RotatedBy(MathHelper.PiOver2) * waveOffset;

            OceanDroplet droplet = new OceanDroplet {
                Position = Projectile.Center + Main.rand.NextVector2Circular(10f, 10f),
                Velocity = particleVel * Main.rand.NextFloat(0.6f, 1.2f),
                Size = Main.rand.NextFloat(1.5f, 3.5f),
                Life = 0,
                MaxLife = Main.rand.Next(30, 55),
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi),
                RotationSpeed = Main.rand.NextFloat(-0.18f, 0.18f),
                Opacity = 1f,
                IsSplash = false,
                ColorVariant = Main.rand.NextFloat(0f, 1f)
            };

            waterDroplets.Add(droplet);
        }

        // 生成泡沫粒子
        private void SpawnFoamParticle() {
            Vector2 offset = Main.rand.NextVector2Circular(12f, 12f);
            Vector2 foamVel = -Projectile.velocity * 0.3f + Main.rand.NextVector2Circular(2f, 2f);
            foamVel.Y -= Main.rand.NextFloat(0.5f, 1.5f); // 泡沫向上漂浮

            SeaFoam foam = new SeaFoam {
                Position = Projectile.Center + offset,
                Velocity = foamVel,
                Size = Main.rand.NextFloat(1.2f, 2.8f),
                Life = 0,
                MaxLife = Main.rand.Next(40, 70),
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi),
                RotationSpeed = Main.rand.NextFloat(-0.12f, 0.12f),
                Opacity = 1f,
                PopPhase = 0f
            };

            foamParticles.Add(foam);
        }

        // 生成海洋生物粒子（鱼、海藻等）
        private void SpawnMarineLife() {
            // 随机选择生物类型
            MarineLifeType type = Main.rand.NextBool() ? MarineLifeType.Fish : MarineLifeType.Seaweed;

            Vector2 offset = Main.rand.NextVector2Circular(20f, 20f);
            Vector2 lifeVel = Projectile.velocity * Main.rand.NextFloat(0.4f, 0.8f);
            lifeVel += Main.rand.NextVector2Circular(1.5f, 1.5f);

            MarineLife life = new MarineLife {
                Position = Projectile.Center + offset,
                Velocity = lifeVel,
                Size = Main.rand.NextFloat(1.5f, 3f),
                Life = 0,
                MaxLife = Main.rand.Next(50, 90),
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi),
                RotationSpeed = Main.rand.NextFloat(-0.08f, 0.08f),
                Opacity = 0.8f,
                Type = type,
                SwimPhase = Main.rand.NextFloat(MathHelper.TwoPi),
                FlickerPhase = Main.rand.NextFloat(MathHelper.TwoPi)
            };

            marineLifeParticles.Add(life);
        }

        // 生成水流波纹
        private void SpawnWaterRipple() {
            for (int i = 0; i < 6; i++) {
                float angle = MathHelper.TwoPi * i / 6f;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(2f, 4f);

                Dust ripple = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Water,
                    velocity,
                    100,
                    default,
                    Main.rand.NextFloat(1.2f, 2f)
                );
                ripple.noGravity = true;
                ripple.fadeIn = 1.3f;
            }
        }

        // 更新水滴粒子
        private void UpdateWaterDroplets() {
            for (int i = waterDroplets.Count - 1; i >= 0; i--) {
                OceanDroplet droplet = waterDroplets[i];
                droplet.Life++;

                // 物理更新
                if (!droplet.IsSplash) {
                    // 正常水流粒子
                    droplet.Velocity.Y += Gravity * WaterDensity;
                    droplet.Velocity.Y += BuoyancyForce * 0.5f;
                    droplet.Velocity *= WaterViscosity;

                    // 表面张力
                    Vector2 toCore = Projectile.Center - droplet.Position;
                    float distToCore = toCore.Length();
                    if (distToCore > 18f && distToCore < 80f) {
                        droplet.Velocity += toCore.SafeNormalize(Vector2.Zero) * SurfaceTension;
                    }
                }
                else {
                    // 飞溅粒子
                    droplet.Velocity.Y += Gravity * 1.8f;
                    droplet.Velocity.X *= 0.96f;

                    // 地面碰撞
                    if (Framing.GetTileSafely(droplet.Position.ToTileCoordinates()).HasTile) {
                        droplet.Velocity.Y *= -0.35f;
                        droplet.Velocity.X *= 0.6f;
                        if (Math.Abs(droplet.Velocity.Y) < 0.8f) {
                            droplet.Velocity.Y = 0;
                        }
                    }
                }

                droplet.Position += droplet.Velocity;
                droplet.Rotation += droplet.RotationSpeed;

                // 透明度衰减
                float lifeRatio = droplet.Life / (float)droplet.MaxLife;
                droplet.Opacity = 1f - lifeRatio;

                // 尺寸变化
                if (droplet.IsSplash) {
                    droplet.Size *= 0.97f;
                }

                // 移除消逝的粒子
                if (droplet.Life >= droplet.MaxLife || droplet.Opacity <= 0.05f) {
                    waterDroplets.RemoveAt(i);
                    continue;
                }

                waterDroplets[i] = droplet;
            }
        }

        // 更新泡沫粒子
        private void UpdateFoamParticles() {
            for (int i = foamParticles.Count - 1; i >= 0; i--) {
                SeaFoam foam = foamParticles[i];
                foam.Life++;

                // 泡沫向上漂浮
                foam.Velocity.Y -= 0.08f;
                foam.Velocity *= 0.98f;
                foam.Position += foam.Velocity;
                foam.Rotation += foam.RotationSpeed;

                // 透明度和尺寸
                float lifeRatio = foam.Life / (float)foam.MaxLife;
                foam.Opacity = (1f - lifeRatio) * 0.9f;
                foam.Size *= 1.01f; // 泡沫膨胀

                // 破裂阶段
                if (lifeRatio > 0.8f) {
                    foam.PopPhase = (lifeRatio - 0.8f) / 0.2f;
                }

                // 移除消逝的粒子
                if (foam.Life >= foam.MaxLife || foam.Opacity <= 0.05f) {
                    foamParticles.RemoveAt(i);
                    continue;
                }

                foamParticles[i] = foam;
            }
        }

        // 更新海洋生物粒子
        private void UpdateMarineLife() {
            for (int i = marineLifeParticles.Count - 1; i >= 0; i--) {
                MarineLife life = marineLifeParticles[i];
                life.Life++;

                // 游动物理
                life.Velocity.Y += Gravity * 0.5f;
                life.Velocity.Y += BuoyancyForce * 2f; // 更强浮力
                life.Velocity *= 0.97f;

                // 游动动画
                life.SwimPhase += 0.15f;
                if (life.SwimPhase > MathHelper.TwoPi) life.SwimPhase -= MathHelper.TwoPi;

                // 添加游动波动
                float swimOffset = (float)Math.Sin(life.SwimPhase) * 0.8f;
                Vector2 swimDirection = life.Velocity.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2);
                life.Position += life.Velocity + swimDirection * swimOffset;

                life.Rotation += life.RotationSpeed;

                // 发光闪烁（生物发光）
                life.FlickerPhase += 0.12f;
                if (life.FlickerPhase > MathHelper.TwoPi) life.FlickerPhase -= MathHelper.TwoPi;

                // 透明度衰减
                float lifeRatio = life.Life / (float)life.MaxLife;
                life.Opacity = (1f - lifeRatio) * 0.8f;

                // 移除消逝的粒子
                if (life.Life >= life.MaxLife || life.Opacity <= 0.05f) {
                    marineLifeParticles.RemoveAt(i);
                    continue;
                }

                marineLifeParticles[i] = life;
            }
        }

        // 更新核心拖尾
        private void UpdateCoreTrail() {
            coreTrail.Insert(0, Projectile.Center);
            if (coreTrail.Count > MaxCoreTrail) {
                coreTrail.RemoveAt(coreTrail.Count - 1);
            }
        }

        // 进入飞溅状态
        private void EnterSplashState() {
            State = OceanState.Splashing;
            Projectile.velocity *= 0.4f;
            Projectile.timeLeft = 80;
        }

        // 碰撞处理
        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (State == OceanState.Streaming) {
                CreateOceanSplash(Projectile.Center, oldVelocity);

                // 飞溅音效
                SoundEngine.PlaySound(SoundID.Splash with {
                    Volume = 0.7f,
                    Pitch = -0.1f
                }, Projectile.Center);

                SoundEngine.PlaySound(SoundID.Item96 with {
                    Volume = 0.4f,
                    Pitch = -0.4f
                }, Projectile.Center);

                EnterSplashState();
                return false;
            }

            return true;
        }

        // 击中NPC
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            CreateOceanSplash(Projectile.Center, Projectile.velocity);

            // 潮湿debuff
            target.AddBuff(BuffID.Wet, 300);

            // 击中音效
            SoundEngine.PlaySound(SoundID.Item85 with {
                Volume = 0.6f,
                Pitch = 0.1f
            }, Projectile.Center);
        }

        // 创建海洋飞溅效果
        private void CreateOceanSplash(Vector2 hitPosition, Vector2 impactVelocity) {
            Vector2 normal = -impactVelocity.SafeNormalize(Vector2.Zero);
            float impactSpeed = impactVelocity.Length();
            float mainAngle = normal.ToRotation();

            int splashCount = (int)MathHelper.Clamp(impactSpeed * 4f, 30, 80);

            for (int i = 0; i < splashCount; i++) {
                float spreadAngle = Main.rand.NextFloat(-MathHelper.PiOver2, MathHelper.PiOver2);
                float angle = mainAngle + spreadAngle;
                float speedRatio = 1f - Math.Abs(spreadAngle) / MathHelper.PiOver2;
                float speed = Main.rand.NextFloat(4f, 14f) * speedRatio * (impactSpeed / 25f);
                Vector2 velocity = angle.ToRotationVector2() * speed;

                if (waterDroplets.Count < MaxDroplets * 2) {
                    OceanDroplet splash = new OceanDroplet {
                        Position = hitPosition + Main.rand.NextVector2Circular(10f, 10f),
                        Velocity = velocity,
                        Size = Main.rand.NextFloat(2f, 4f),
                        Life = 0,
                        MaxLife = Main.rand.Next(40, 70),
                        Rotation = Main.rand.NextFloat(MathHelper.TwoPi),
                        RotationSpeed = Main.rand.NextFloat(-0.25f, 0.25f),
                        Opacity = 1f,
                        IsSplash = true,
                        ColorVariant = Main.rand.NextFloat(0f, 1f)
                    };
                    waterDroplets.Add(splash);
                }

                // 原版水尘埃
                if (i % 3 == 0) {
                    Dust water = Dust.NewDustPerfect(
                        hitPosition,
                        DustID.Water,
                        velocity * 0.5f,
                        100,
                        default,
                        Main.rand.NextFloat(1.5f, 2.5f)
                    );
                    water.noGravity = false;
                    water.fadeIn = 1.4f;
                }
            }

            // 飞溅泡沫
            for (int i = 0; i < 20; i++) {
                float angle = mainAngle + Main.rand.NextFloat(-1f, 1f);
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(3f, 8f);

                if (foamParticles.Count < MaxFoam * 2) {
                    SeaFoam foam = new SeaFoam {
                        Position = hitPosition,
                        Velocity = velocity,
                        Size = Main.rand.NextFloat(2f, 4f),
                        Life = 0,
                        MaxLife = Main.rand.Next(35, 60),
                        Rotation = Main.rand.NextFloat(MathHelper.TwoPi),
                        RotationSpeed = Main.rand.NextFloat(-0.2f, 0.2f),
                        Opacity = 1f,
                        PopPhase = 0f
                    };
                    foamParticles.Add(foam);
                }
            }

            // 飞溅环
            CreateSplashRing(hitPosition, mainAngle, impactSpeed);
        }

        // 创建飞溅环
        private void CreateSplashRing(Vector2 center, float direction, float intensity) {
            int ringCount = (int)(intensity * 1.2f);
            ringCount = Math.Clamp(ringCount, 20, 40);

            for (int i = 0; i < ringCount; i++) {
                float angle = direction + MathHelper.Lerp(-MathHelper.Pi, MathHelper.Pi, i / (float)ringCount);
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(5f, 10f);

                Dust ring = Dust.NewDustPerfect(
                    center,
                    DustID.Water,
                    velocity,
                    100,
                    default,
                    Main.rand.NextFloat(2f, 3f)
                );
                ring.noGravity = true;
                ring.fadeIn = 1.5f;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (State == OceanState.Dispersing && Projectile.alpha > 220) {
                DrawMarineLife();
                DrawFoamParticles();
                DrawWaterDroplets();
                return false;
            }

            SpriteBatch sb = Main.spriteBatch;

            // 绘制海洋生物
            DrawMarineLife();

            // 绘制泡沫
            DrawFoamParticles();

            // 绘制水滴
            DrawWaterDroplets();

            // 绘制核心水流
            if (State == OceanState.Streaming) {
                DrawStreamCore(sb);
            }

            return false;
        }

        // 绘制水滴粒子
        private void DrawWaterDroplets() {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D glowTex = CWRAsset.Spray.Value;//原来是CWRAsset.StarTexture_White.Value;
            Texture2D streamTex = CWRAsset.LightShot.Value;

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            foreach (var droplet in waterDroplets) {
                Vector2 drawPos = droplet.Position - Main.screenPosition;
                float scale = droplet.Size * 0.09f;

                // 海洋蓝色渐变
                Color deepColor = Color.Lerp(DeepOcean, ShallowOcean, droplet.ColorVariant);
                Color waterColor = deepColor * droplet.Opacity * 0.9f;

                // 水滴主体
                sb.Draw(
                    glowTex,
                    drawPos,
                    null,
                    waterColor,
                    droplet.Rotation,
                    glowTex.Size() / 2f,
                    scale * 1.3f,
                    SpriteEffects.None,
                    0
                );

                // 水滴高光
                Color highlightColor = BioluminescentBlue * droplet.Opacity * 0.7f;
                sb.Draw(
                    glowTex,
                    drawPos,
                    null,
                    highlightColor,
                    droplet.Rotation * 1.5f,
                    glowTex.Size() / 2f,
                    scale * 0.7f,
                    SpriteEffects.None,
                    0
                );

                // 拉伸效果（快速移动的水滴）
                if (!droplet.IsSplash && droplet.Velocity.Length() > 5f) {
                    float rotation = droplet.Velocity.ToRotation();
                    Color trailColor = waterColor * 0.5f;
                    sb.Draw(
                        streamTex,
                        drawPos,
                        null,
                        trailColor,
                        rotation,
                        streamTex.Size() / 2f,
                        new Vector2(scale * 0.12f, scale * 0.25f),
                        SpriteEffects.None,
                        0
                    );
                }
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }

        // 绘制泡沫粒子
        private void DrawFoamParticles() {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D glowTex = CWRAsset.StarTexture_White.Value;

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            foreach (var foam in foamParticles) {
                Vector2 drawPos = foam.Position - Main.screenPosition;
                float scale = foam.Size * 0.06f;

                // 泡沫白色
                Color foamColor = OceanFoam * foam.Opacity * (1f - foam.PopPhase * 0.5f);

                // 泡沫主体
                sb.Draw(
                    glowTex,
                    drawPos,
                    null,
                    foamColor * 0.8f,
                    foam.Rotation,
                    glowTex.Size() / 2f,
                    scale * (1f + foam.PopPhase * 0.3f),
                    SpriteEffects.None,
                    0
                );

                // 泡沫边缘
                Color edgeColor = new Color(150, 200, 255) * foam.Opacity * 0.5f;
                sb.Draw(
                    glowTex,
                    drawPos,
                    null,
                    edgeColor,
                    foam.Rotation * 1.3f,
                    glowTex.Size() / 2f,
                    scale * 1.4f * (1f + foam.PopPhase * 0.4f),
                    SpriteEffects.None,
                    0
                );
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }

        // 绘制海洋生物
        private void DrawMarineLife() {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D glowTex = CWRAsset.StarTexture_White.Value;

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            foreach (var life in marineLifeParticles) {
                Vector2 drawPos = life.Position - Main.screenPosition;
                float scale = life.Size * 0.05f;

                // 生物发光效果
                float flicker = (float)Math.Sin(life.FlickerPhase) * 0.3f + 0.7f;
                Color lifeColor = life.Type == MarineLifeType.Fish
                    ? BioluminescentBlue * life.Opacity * flicker
                    : new Color(50, 150, 100) * life.Opacity * flicker;

                // 主体
                sb.Draw(
                    glowTex,
                    drawPos,
                    null,
                    lifeColor * 0.9f,
                    life.Rotation,
                    glowTex.Size() / 2f,
                    scale * 1.5f,
                    SpriteEffects.None,
                    0
                );

                // 发光核心
                Color coreColor = Color.White * life.Opacity * flicker * 0.6f;
                sb.Draw(
                    glowTex,
                    drawPos,
                    null,
                    coreColor,
                    life.Rotation * 1.8f,
                    glowTex.Size() / 2f,
                    scale * 0.8f,
                    SpriteEffects.None,
                    0
                );

                // 游动拖尾（仅鱼类）
                if (life.Type == MarineLifeType.Fish) {
                    Vector2 tailOffset = -life.Velocity.SafeNormalize(Vector2.Zero) * scale * 30f;
                    Color tailColor = lifeColor * 0.4f;
                    sb.Draw(
                        glowTex,
                        drawPos + tailOffset,
                        null,
                        tailColor,
                        life.Rotation,
                        glowTex.Size() / 2f,
                        scale * 0.8f,
                        SpriteEffects.None,
                        0
                    );
                }
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }

        // 绘制水流核心
        private void DrawStreamCore(SpriteBatch sb) {
            if (coreTrail.Count < 2) return;

            Texture2D coreTex = CWRAsset.LightShot.Value;

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            // 绘制核心拖尾
            for (int i = 0; i < coreTrail.Count - 1; i++) {
                float progress = 1f - i / (float)coreTrail.Count;
                Vector2 drawPos = coreTrail[i] - Main.screenPosition;
                Vector2 toNext = coreTrail[i + 1] - coreTrail[i];
                float rotation = toNext.ToRotation();

                // 海洋蓝色渐变
                Color coreColor = Color.Lerp(
                    BioluminescentBlue,
                    ShallowOcean,
                    progress
                ) * progress * glowPulse * 0.9f;

                float scale = progress * 0.12f;

                sb.Draw(
                    coreTex,
                    drawPos,
                    null,
                    coreColor,
                    rotation,
                    coreTex.Size() / 2f,
                    new Vector2(scale * 3f, scale * 1.2f),
                    SpriteEffects.None,
                    0
                );

                // 外层光晕
                Color glowColor = new Color(100, 180, 255) * progress * glowPulse * 0.5f;
                sb.Draw(
                    coreTex,
                    drawPos,
                    null,
                    glowColor,
                    rotation,
                    coreTex.Size() / 2f,
                    new Vector2(scale * 5f, scale * 2.5f),
                    SpriteEffects.None,
                    0
                );
            }

            // 绘制水流头部
            Vector2 headPos = Projectile.Center - Main.screenPosition;
            Color headColor = BioluminescentBlue * glowPulse;

            sb.Draw(
                coreTex,
                headPos,
                null,
                headColor,
                Projectile.velocity.ToRotation(),
                coreTex.Size() / 2f,
                new Vector2(0.18f, 0.14f),
                SpriteEffects.None,
                0
            );

            // 头部强光核心（波动效果）
            Texture2D starTex = CWRAsset.StarTexture_White.Value;
            float pulseScale = 0.1f + (float)Math.Sin(wavePhase) * 0.02f;
            Color starColor = Color.White * glowPulse * 0.9f;
            sb.Draw(
                starTex,
                headPos,
                null,
                starColor,
                StreamLife * 0.08f,
                starTex.Size() / 2f,
                pulseScale,
                SpriteEffects.None,
                0
            );

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }

        public override void OnKill(int timeLeft) {
            // 死亡时残留飞溅
            if (State == OceanState.Streaming) {
                CreateOceanSplash(Projectile.Center, Projectile.velocity * 0.6f);
            }

            // 残留水效果
            for (int i = 0; i < 25; i++) {
                Dust water = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(25f, 25f),
                    DustID.Water,
                    Main.rand.NextVector2Circular(5f, 5f),
                    100,
                    default,
                    Main.rand.NextFloat(1.5f, 2.5f)
                );
                water.noGravity = Main.rand.NextBool();
                water.fadeIn = 1.3f;
            }

            // 泡沫爆发
            for (int i = 0; i < 15; i++) {
                Dust foam = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Smoke,
                    Main.rand.NextVector2Circular(4f, 4f),
                    100,
                    OceanFoam,
                    Main.rand.NextFloat(1f, 2f)
                );
                foam.noGravity = true;
                foam.fadeIn = 1.1f;
            }

            Projectile.Explode(90, default, false);
        }

        public override Color? GetAlpha(Color lightColor) {
            float alphaMult = 1f - (Projectile.alpha / 255f);
            return Color.Lerp(ShallowOcean, BioluminescentBlue, glowPulse) * alphaMult;
        }
    }

    #region 粒子数据结构

    /// <summary>
    /// 海洋水滴粒子
    /// </summary>
    internal struct OceanDroplet
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Size;
        public int Life;
        public int MaxLife;
        public float Rotation;
        public float RotationSpeed;
        public float Opacity;
        public bool IsSplash;
        public float ColorVariant; // 0-1，用于颜色变化
    }

    /// <summary>
    /// 海洋泡沫粒子
    /// </summary>
    internal struct SeaFoam
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Size;
        public int Life;
        public int MaxLife;
        public float Rotation;
        public float RotationSpeed;
        public float Opacity;
        public float PopPhase; // 0-1，破裂阶段
    }

    /// <summary>
    /// 海洋生物类型
    /// </summary>
    internal enum MarineLifeType
    {
        Fish,       // 小鱼
        Seaweed     // 海藻
    }

    /// <summary>
    /// 海洋生物粒子
    /// </summary>
    internal struct MarineLife
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Size;
        public int Life;
        public int MaxLife;
        public float Rotation;
        public float RotationSpeed;
        public float Opacity;
        public MarineLifeType Type;
        public float SwimPhase;     // 游动动画相位
        public float FlickerPhase;  // 闪烁相位（生物发光）
    }

    #endregion
}
