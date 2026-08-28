using CalamityOverhaul.Common;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    internal class FishCloud : FishSkill
    {
        [VaultLoaden(CWRConstant.Masking)]
        public static Texture2D Fog = null;//256×256 灰度雾图，CPU 叠云用

        public override int UnlockFishID => ItemID.Cloudfish;

        public override int DefaultCooldown => 60 * (25 - HalibutData.GetDomainLayer() * 2); //25-2*领域 秒冷却
        public override int ResearchDuration => 60 * 16;
        public override bool? AltFunctionUse(Item item, Player player) => true;

        public override bool? CanUseItem(Item item, Player player) {
            if (player.altFunctionUse == 2) {
                if (Cooldown > 0) return false;
                item.UseSound = null;
                Use(item, player);
                return false;
            }
            return base.CanUseItem(item, player);
        }

        public override void Use(Item item, Player player) {
            SetCooldown();
            Projectile.NewProjectile(
                player.GetSource_ItemUse(item),
                player.Center + new Vector2(0, -100),
                Vector2.Zero,
                ModContent.ProjectileType<CloudRide>(),
                0,
                0f,
                player.whoAmI
            );
            SoundEngine.PlaySound(SoundID.Item96 with { Volume = 0.8f, Pitch = 0.2f }, player.Center);
            SoundEngine.PlaySound(SoundID.DD2_BetsyWindAttack with { Volume = 0.5f, Pitch = 0.5f }, player.Center);
        }

        public override bool UpdateCooldown(HalibutPlayer halibutPlayer, Player player) {
            //玩家更新阶段 ownedProjectileCounts 是上一拍完整快照，O(1) 代替全表扫描
            return player.ownedProjectileCounts[ModContent.ProjectileType<CloudRide>()] == 0;
        }
    }

    internal class CloudRide : ModProjectile, IOverlayDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private Player Owner => Main.player[Projectile.owner];

        private ref float LifeTimer => ref Projectile.ai[0];

        /// <summary>阶段，0=飞向玩家脚下，1=载着玩家飞行，2=消散</summary>
        private int Phase {
            get => (int)Projectile.ai[1];
            set => Projectile.ai[1] = value;
        }

        private const int MaxDuration = 60 * 8; //8s

        private List<CloudFishParticle> cloudFishParticles = new();

        private int rainTimer = 0;

        private float cloudScale = 0f;

        private float cloudAlpha = 0f;

        private Vector2 targetPosition = Vector2.Zero;

        /// <summary>玩家原始重力（结束时恢复）</summary>
        private float originalGravity = 0f;

        /// <summary>成形包络，0=散逸 1=完整云体，驱动 shader 聚拢/蚀散</summary>
        private float cloudGrow = 0f;

        /// <summary>乘云瞬间的聚拢过冲脉冲，14 帧衰减</summary>
        private int mountPulse = 0;

        /// <summary>平滑风矢量（速度/极速），喂给 shader 做内部剪切与尾蚀</summary>
        private Vector2 windSmooth = Vector2.Zero;

        /// <summary>雨幕强度包络，Phase1 缓升</summary>
        private float rainVeil = 0f;

        /// <summary>shader 实例随机相位</summary>
        private float drawSeed = 0f;

        /// <summary>散逸拍是否已放（Phase2 入场一次性云絮外扑）</summary>
        private bool dissipateBurst = false;

        private const int CloudFishCount = 15;

        public override void SetDefaults() {
            Projectile.width = 140;
            Projectile.height = 60;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = MaxDuration + 120; //额外帧消散

            drawSeed = Main.rand.NextFloat(10f);
        }

        /// <summary>初始化云鱼 boids</summary>
        private void InitializeCloudFish() {
            cloudFishParticles.Clear();
            for (int i = 0; i < CloudFishCount; i++) {
                //在云朵周围随机位置生成云鱼
                float angle = MathHelper.TwoPi * i / CloudFishCount + Main.rand.NextFloat(-0.25f, 0.25f);
                float distance = Main.rand.NextFloat(90f, 150f);

                Vector2 spawnOffset = new Vector2(
                    (float)Math.Cos(angle) * distance,
                    (float)Math.Sin(angle) * distance * 0.55f //扁平分布
                );

                cloudFishParticles.Add(new CloudFishParticle {
                    Position = Projectile.Center + spawnOffset,
                    Velocity = Main.rand.NextVector2Circular(2f, 1f),
                    Scale = Main.rand.NextFloat(0.8f, 1.25f), //增大基础尺寸
                    Rotation = angle,
                    Alpha = 0f, //初始透明，逐渐淡入
                    FishID = i,
                    BehaviorRandomness = Main.rand.NextFloat(0.85f, 1.25f),
                    PhaseOffset = Main.rand.NextFloat(MathHelper.TwoPi),
                    Color = Color.Lerp(new Color(206, 216, 228), new Color(240, 244, 250), Main.rand.NextFloat()) //雾灰白，与云同语系
                });
            }
        }

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Phase = 2;
            }

            if (LifeTimer == 0) {
                InitializeCloudFish();
            }

            LifeTimer++;

            //风矢量平滑
            windSmooth = Vector2.Lerp(windSmooth, Projectile.velocity / 25f, 0.10f);
            //雨幕包络，乘骑时缓升，离云缓落
            rainVeil = MathHelper.Lerp(rainVeil, Phase == 1 ? 1f : 0f, 0.05f);
            if (mountPulse > 0) {
                mountPulse--;
            }

            UpdateCloudFishParticles();

            switch (Phase) {
                case 0: //飞向玩家脚下
                    FlyToPlayerPhase();
                    break;
                case 1: //载着玩家飞行
                    RidingPhase();
                    break;
                case 2: //消散
                    DissipatePhase();
                    break;
            }

            if (Phase == 1) {
                SpawnRain();
            }

            if (Phase == 1 && LifeTimer % 120 == 0) {
                SoundEngine.PlaySound(SoundID.LiquidsWaterLava with { Volume = 0.3f, Pitch = 0.3f }, Projectile.Center);
            }
        }

        /// <summary>云絮灰白配色，0=亮顶 1=暗底</summary>
        private static Color WispColor(float shade) => Color.Lerp(new Color(233, 237, 243), new Color(158, 166, 180), shade);

        /// <summary>云鱼 boids tick</summary>
        private void UpdateCloudFishParticles() {
            for (int i = 0; i < cloudFishParticles.Count; i++) {
                CloudFishParticle fish = cloudFishParticles[i];

                if (Phase == 0 || Phase == 1) {
                    if (fish.Alpha < 1f) {
                        fish.Alpha += 0.05f;
                        if (fish.Alpha > 1f) fish.Alpha = 1f;
                    }
                }
                else if (Phase == 2) {
                    //消散阶段淡出
                    fish.Alpha -= 0.08f;
                }

                Vector2 separationForce = Vector2.Zero;
                Vector2 alignmentForce = Vector2.Zero;
                Vector2 cohesionForce = Vector2.Zero;

                Vector2 centerOfMass = Projectile.Center;
                Vector2 averageVelocity = Vector2.Zero;
                int nearbyFishCount = 0;

                for (int j = 0; j < cloudFishParticles.Count; j++) {
                    if (i == j) continue;

                    CloudFishParticle otherFish = cloudFishParticles[j];
                    float distance = Vector2.Distance(fish.Position, otherFish.Position);

                    //分离力避免碰撞
                    if (distance < 40f && distance > 0.1f) {
                        Vector2 awayFromOther = (fish.Position - otherFish.Position).SafeNormalize(Vector2.Zero);
                        separationForce += awayFromOther / distance;
                    }

                    if (distance < 120f) {
                        centerOfMass += otherFish.Position;
                        averageVelocity += otherFish.Velocity;
                        nearbyFishCount++;
                    }
                }

                if (nearbyFishCount > 0) {
                    centerOfMass /= nearbyFishCount;
                    averageVelocity /= nearbyFishCount;

                    //对齐力向平均方向移动
                    alignmentForce = (averageVelocity - fish.Velocity) * 0.1f;

                    //聚合力向群体中心移动
                    cohesionForce = (centerOfMass - fish.Position).SafeNormalize(Vector2.Zero) * 0.3f;
                }

                //围绕云朵运动的核心力
                Vector2 toCloud = Projectile.Center - fish.Position;
                float distanceToCloud = toCloud.Length();

                //维持在云朵周围的目标距离（椭圆形轨道）
                float targetDistanceX = 100f;
                float targetDistanceY = 60f;

                //计算理想位置（椭圆轨道）
                Vector2 directionToCloud = toCloud.SafeNormalize(Vector2.Zero);
                float currentAngle = (float)Math.Atan2(directionToCloud.Y, directionToCloud.X);

                float idealDistance = (float)Math.Sqrt(
                    Math.Pow(targetDistanceX * Math.Sin(currentAngle), 2) +
                    Math.Pow(targetDistanceY * Math.Cos(currentAngle), 2)
                );

                //向心力和离心力的平衡
                Vector2 orbitForce = Vector2.Zero;
                if (distanceToCloud < idealDistance - 20f) {
                    //太近了，向外推
                    orbitForce = -directionToCloud * 1.5f;
                }
                else if (distanceToCloud > idealDistance + 20f) {
                    //太远了，向内拉
                    orbitForce = directionToCloud * 2.0f;
                }

                //切向速度（围绕云朵旋转）
                //根据云朵移动方向调整旋转方向
                Vector2 tangentialDirection = new Vector2(-directionToCloud.Y, directionToCloud.X);

                //当云朵高速移动时，云鱼在后方加速追赶
                float cloudSpeed = Projectile.velocity.Length();
                bool isBehindCloud = Vector2.Dot(toCloud, Projectile.velocity) > 0;
                float catchUpBoost = (isBehindCloud && cloudSpeed > 5f) ? 2.0f : 1.0f;

                Vector2 tangentialForce = tangentialDirection * (2.5f + (float)Math.Sin(LifeTimer * 0.05f + fish.PhaseOffset) * 0.8f) * catchUpBoost;

                //跟随云朵的速度同步
                Vector2 velocitySync = Projectile.velocity * 0.6f;

                //波动效果（上下摆动）
                float waveTime = LifeTimer * 0.08f + fish.PhaseOffset;
                Vector2 waveForce = new Vector2(0, (float)Math.Sin(waveTime) * 0.4f * fish.BehaviorRandomness);

                //随机游动
                Vector2 randomWander = new Vector2(
                    (float)Math.Sin(waveTime * 1.3f),
                    (float)Math.Cos(waveTime * 1.7f)
                ) * 0.3f * fish.BehaviorRandomness;

                Vector2 totalForce = Vector2.Zero;
                totalForce += separationForce * 2.5f; //分离力（避免重叠）
                totalForce += alignmentForce * 1.2f; //对齐力
                totalForce += cohesionForce * 0.8f; //聚合力
                totalForce += orbitForce * 1.5f; //轨道力
                totalForce += tangentialForce; //切向运动
                totalForce += velocitySync; //速度同步
                totalForce += waveForce; //波动
                totalForce += randomWander; //随机游动

                fish.Velocity += totalForce * 0.15f;

                float maxSpeed = 8f * fish.BehaviorRandomness;
                float minSpeed = 2f;
                float currentSpeed = fish.Velocity.Length();

                if (currentSpeed > maxSpeed) {
                    fish.Velocity = fish.Velocity.SafeNormalize(Vector2.Zero) * maxSpeed;
                }
                else if (currentSpeed < minSpeed && currentSpeed > 0.1f) {
                    fish.Velocity = fish.Velocity.SafeNormalize(Vector2.Zero) * minSpeed;
                }

                fish.Position += fish.Velocity;
                fish.Position += Projectile.velocity * 0.64f;

                if (fish.Velocity.LengthSquared() > 0.1f) {
                    fish.Rotation = MathHelper.Lerp(fish.Rotation, fish.Velocity.ToRotation(), 0.2f);
                }

                //轻微的游动摆尾效果
                fish.Rotation += (float)Math.Sin(LifeTimer * 0.2f + fish.PhaseOffset) * 0.08f;

                cloudFishParticles[i] = fish;
            }
        }

        /// <summary>阶段0，飞向玩家脚下</summary>
        private void FlyToPlayerPhase() {
            cloudAlpha += 0.08f;
            if (cloudAlpha > 1f) cloudAlpha = 1f;

            cloudScale += 0.05f;
            if (cloudScale > 1f) cloudScale = 1f;

            //聚拢成形
            cloudGrow = Math.Min(cloudGrow + 0.045f, 0.85f);

            //计算目标位置（玩家脚下）
            targetPosition = Owner.Bottom + new Vector2(0, 15);

            //飞向目标
            Vector2 toTarget = targetPosition - Projectile.Center;
            float distance = toTarget.Length();

            if (distance > 20f) {
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget.SafeNormalize(Vector2.Zero) * 20f, 0.15f);
            }
            else {
                //到达目标，进入乘骑阶段
                Phase = 1;
                Projectile.velocity = Vector2.Zero;
                originalGravity = Owner.gravity;
                mountPulse = 14;

                //乘云英雄拍
                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 10; i++) {
                        float ang = MathHelper.TwoPi * i / 10f;
                        Vector2 dir = new Vector2(MathF.Cos(ang), MathF.Sin(ang) * 0.4f);
                        PRTLoader.NewParticle<PRT_FishCloudWisp>(Projectile.Center + dir * 30f,
                            dir * Main.rand.NextFloat(2.2f, 3.6f),
                            WispColor(Main.rand.NextFloat(0.2f, 0.7f)), Main.rand.NextFloat(0.18f, 0.30f))
                            ?.Configure(Main.rand.Next(26, 40), 0.012f, 0.996f);
                    }
                    PRTLoader.NewParticle<PRT_FishCloudSplash>(Projectile.Center + new Vector2(0, 10f), Vector2.Zero,
                        new Color(196, 208, 226), 1f)?.Configure(15, 0.24f);
                }

                SoundEngine.PlaySound(SoundID.Item45 with { Volume = 0.5f, Pitch = 0.4f }, Projectile.Center);
            }

            //凝聚吸入，云絮从外围被卷进云心
            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(3)) {
                Vector2 off = Main.rand.NextVector2CircularEdge(110f, 55f);
                PRTLoader.NewParticle<PRT_FishCloudWisp>(Projectile.Center + off,
                    -off * 0.045f + Projectile.velocity * 0.3f,
                    WispColor(Main.rand.NextFloat()), Main.rand.NextFloat(0.14f, 0.24f))
                    ?.Configure(Main.rand.Next(20, 30), 0.010f, 0.995f);
            }
        }

        /// <summary>阶段1</summary>
        private void RidingPhase() {
            cloudAlpha = 1f;
            cloudScale = 1f + (float)Math.Sin(LifeTimer * 0.08f) * 0.06f; //轻微呼吸效果
            cloudGrow = Math.Min(cloudGrow + 0.03f, 1f);

            //计算朝向光标的方向
            Vector2 toMouse = (Main.MouseWorld - Projectile.Center).SafeNormalize(Vector2.Zero);

            //加速飞行
            float acceleration = 1.2f;
            float maxSpeed = 25f;

            Projectile.velocity += toMouse * acceleration;

            float currentSpeed = Projectile.velocity.Length();
            if (currentSpeed > maxSpeed) {
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * maxSpeed;
            }

            //玩家位置跟随云朵（脚部位于云朵顶部）
            Owner.position = Projectile.Center + new Vector2(0, -25) - Owner.Size / 2f;
            Owner.velocity = Projectile.velocity; Owner.fallStart = (int)(Owner.position.Y / 16f); Owner.gravity = 0f; Owner.noFallDmg = true;

            //玩家朝向飞行方向
            if (currentSpeed > 2f) {
                //根据水平速度设置朝向
                if (Projectile.velocity.X > 1f) {
                    Owner.direction = 1;
                }
                else if (Projectile.velocity.X < -1f) {
                    Owner.direction = -1;
                }

                //计算玩家倾斜角度（根据飞行方向）
                float targetRotation = Projectile.velocity.ToRotation();
                if (Owner.direction == -1) {
                    targetRotation = MathHelper.Pi - targetRotation;
                }

                //平滑过渡到目标角度
                Owner.fullRotation = targetRotation * Owner.direction;
                Owner.fullRotationOrigin = Owner.Size / 2f;
            }
            else {
                //低速时恢复水平
                Owner.fullRotation = MathHelper.Lerp(Owner.fullRotation, 0f, 0.2f) * Owner.direction;
            }

            //蜕云
            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(3)) {
                Vector2 off = new Vector2(Main.rand.NextFloat(-95f, 95f), Main.rand.NextFloat(-16f, 26f));
                PRTLoader.NewParticle<PRT_FishCloudWisp>(Projectile.Center + off,
                    -Projectile.velocity * 0.10f + new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(-0.2f, 0.5f)),
                    WispColor(Main.rand.NextFloat(0.25f, 0.8f)), Main.rand.NextFloat(0.15f, 0.27f))
                    ?.Configure(Main.rand.Next(30, 48));
            }

            //高速尾撕
            if (Main.netMode != NetmodeID.Server && currentSpeed > 12f && Main.rand.NextBool(2)) {
                Vector2 tailDir = -Projectile.velocity.SafeNormalize(Vector2.Zero);
                Vector2 pos = Projectile.Center + tailDir * Main.rand.NextFloat(60f, 100f)
                    + tailDir.RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(-24f, 24f);
                PRTLoader.NewParticle<PRT_FishCloudWisp>(pos, tailDir * Main.rand.NextFloat(1.2f, 2.6f),
                    WispColor(Main.rand.NextFloat(0.3f, 0.9f)), Main.rand.NextFloat(0.13f, 0.22f))
                    ?.Configure(Main.rand.Next(20, 34), 0.014f, 0.990f);
            }

            //云尘底噪
            if (Main.rand.NextBool(4)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(80f, 30f), DustID.Cloud,
                    -Projectile.velocity * 0.2f, Scale: Main.rand.NextFloat(1.0f, 1.6f));
                d.noGravity = true;
                d.alpha = 160;
            }

            if (LifeTimer > MaxDuration) {
                Phase = 2;
            }
        }

        /// <summary>阶段2</summary>
        private void DissipatePhase() {
            cloudAlpha -= 0.030f;
            cloudScale += 0.006f;
            //蚀散
            cloudGrow = Math.Max(cloudGrow - 0.033f, 0f);

            //散逸拍，入场一次性云絮外扑
            if (!dissipateBurst) {
                dissipateBurst = true;
                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 8; i++) {
                        float ang = MathHelper.TwoPi * i / 8f + Main.rand.NextFloat(-0.3f, 0.3f);
                        Vector2 dir = new Vector2(MathF.Cos(ang), MathF.Sin(ang) * 0.5f);
                        PRTLoader.NewParticle<PRT_FishCloudWisp>(Projectile.Center + dir * 40f,
                            dir * Main.rand.NextFloat(1.4f, 2.6f) + new Vector2(0, -0.3f),
                            WispColor(Main.rand.NextFloat()), Main.rand.NextFloat(0.18f, 0.32f))
                            ?.Configure(Main.rand.Next(40, 65));
                    }
                }
            }

            //恢复玩家状态
            if (Owner.active) {
                Owner.gravity = Player.defaultGravity;
                Owner.fullRotation = MathHelper.Lerp(Owner.fullRotation, 0f, 0.25f);
            }

            //减速
            Projectile.velocity *= 0.95f;

            //持续散逸，碎云外飘上浮
            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(2)) {
                Vector2 off = Main.rand.NextVector2Circular(85f, 40f);
                PRTLoader.NewParticle<PRT_FishCloudWisp>(Projectile.Center + off,
                    off * 0.03f + new Vector2(0, -0.3f),
                    WispColor(Main.rand.NextFloat()), Main.rand.NextFloat(0.16f, 0.30f))
                    ?.Configure(Main.rand.Next(35, 60));
            }

            if (cloudAlpha <= 0f) {
                Projectile.Kill();
            }
        }

        /// <summary>生成雨滴</summary>
        private void SpawnRain() {
            rainTimer++;

            //每2帧生成一滴雨
            if (rainTimer % 2 == 0) {
                //在云朵底部随机位置生成雨滴（扁平分布）
                Vector2 rainSpawnPos = Projectile.Center + new Vector2(
                    Main.rand.NextFloat(-70f, 70f),
                    Main.rand.NextFloat(25f, 35f)
                );

                //生成雨滴弹幕
                int rainProj = Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    rainSpawnPos,
                    new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(8f, 12f)),
                    ModContent.ProjectileType<CloudRain>(),
                    (int)(Owner.GetShootState().WeaponDamage * (0.5f + HalibutData.GetDomainLayer() / 5)),
                    2f,
                    Owner.whoAmI
                );
            }

            //雨雾效果
            if (Main.rand.NextBool(4)) {
                Dust mist = Dust.NewDustPerfect(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-80f, 80f), 30f),
                    DustID.Water,
                    new Vector2(0, Main.rand.NextFloat(3f, 6f)),
                    Scale: Main.rand.NextFloat(0.8f, 1.4f)
                );
                mist.noGravity = true;
                mist.alpha = 150;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Vector2 center = Projectile.Center - Main.screenPosition;

            if (cloudAlpha > 0.01f) {
                //环境光压暗
                float light = MathF.Max(MathF.Max(lightColor.R, MathF.Max(lightColor.G, lightColor.B)) / 255f, 0.35f);
                Effect fx = FishCloudAssets.FishCloudPuff;
                Texture2D noise = CWRAsset.PerlinNoise?.Value;
                if (fx != null && noise != null) {
                    DrawCloudShader(sb, center, fx, noise, light);
                }
                else {
                    DrawCloudFallback(sb, center, light);
                }
            }

            //伴飞云鱼
            DrawCloudFishParticles(sb);

            return false;
        }

        /// <summary>shader 云体，单 quad 内画 6 瓣积云 + 蚀边翻卷 + 雨幕</summary>
        private void DrawCloudShader(SpriteBatch sb, Vector2 center, Effect fx, Texture2D noise, float light) {
            //乘云过冲
            float pulse = mountPulse > 0 ? 0.20f * (mountPulse / 14f) : 0f;
            float grow = MathHelper.Clamp(cloudGrow + pulse, 0f, 1.2f);

            fx.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects / 60f);
            fx.Parameters["uSeed"]?.SetValue(drawSeed);
            fx.Parameters["uGrow"]?.SetValue(grow);
            fx.Parameters["uAlpha"]?.SetValue(cloudAlpha);
            fx.Parameters["uRain"]?.SetValue(rainVeil);
            fx.Parameters["uWind"]?.SetValue(windSmooth);
            fx.Parameters["uTopCol"]?.SetValue(new Vector3(0.94f, 0.96f, 0.99f) * light);
            fx.Parameters["uBotCol"]?.SetValue(new Vector3(0.52f, 0.56f, 0.63f) * light);
            fx.Parameters["uNoiseTex"]?.SetValue(noise);

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, fx, Main.GameViewMatrix.TransformationMatrix);

            //1×1 白像素拉成整块云 quad，shader 在其中成形，下段留给雨幕
            Texture2D px = VaultAsset.placeholder2.Value;
            //速度拉伸按分量拆轴
            float stretchX = 1f + MathHelper.Clamp(MathF.Abs(windSmooth.X) * 0.35f, 0f, 0.35f);
            float stretchY = 1f + MathHelper.Clamp(MathF.Abs(windSmooth.Y) * 0.22f, 0f, 0.22f);
            Vector2 size = new Vector2(360f * stretchX, 300f * stretchY) * cloudScale;
            sb.Draw(px, center, new Rectangle(0, 0, 1, 1), Color.White, 0f, new Vector2(0.5f, 0.36f), size, SpriteEffects.None, 0f);

            sb.End();
            //回到原版实体批次参数，后续弹幕绘制不受影响
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>兜底云体</summary>
        private static readonly Vector2[] FallbackOffsets = [
            new(0, -6), new(-52, 6), new(50, 4), new(-24, -20), new(26, -18), new(78, 12), new(-80, 12)
        ];
        private static readonly float[] FallbackScales = [0.95f, 0.70f, 0.72f, 0.66f, 0.68f, 0.45f, 0.46f];

        private void DrawCloudFallback(SpriteBatch sb, Vector2 center, float light) {
            Texture2D fog = FishCloud.Fog;
            if (fog == null) return;
            Vector2 origin = fog.Size() / 2f;
            float gather = MathHelper.Clamp(cloudGrow, 0.2f, 1f);
            for (int i = 0; i < FallbackOffsets.Length; i++) {
                Vector2 off = FallbackOffsets[i] * cloudScale * gather;
                float shade = MathHelper.Lerp(0.95f, 0.60f, (off.Y + 24f) / 48f);
                Color c = new Color(shade * light, shade * light, MathF.Min(shade * 1.05f, 1f) * light) * (cloudAlpha * 0.85f);
                sb.Draw(fog, center + off, null, c, i * 0.9f, origin, FallbackScales[i] * cloudScale * gather, SpriteEffects.None, 0f);
            }
        }

        //前唇云絮
        private static readonly Vector2[] FrontLipOffsets = [new(-34, 0), new(2, 8), new(36, -2)];

        void IOverlayDrawable.DrawOverlay(SpriteBatch spriteBatch) {
            if (cloudAlpha <= 0.05f || cloudGrow <= 0.1f || FishCloud.Fog == null) return;
            Texture2D fog = FishCloud.Fog;
            Vector2 center = Projectile.Center - Main.screenPosition;
            Color lc = Lighting.GetColor(Projectile.Center.ToTileCoordinates());
            float light = MathF.Max(MathF.Max(lc.R, MathF.Max(lc.G, lc.B)) / 255f, 0.35f);
            Color tint = new Color(0.80f * light, 0.83f * light, 0.87f * light);
            float t = (float)Main.timeForVisualEffects * 0.02f;
            for (int i = 0; i < FrontLipOffsets.Length; i++) {
                Vector2 off = FrontLipOffsets[i] + new Vector2(MathF.Sin(t + i * 2.1f) * 4f, MathF.Cos(t * 0.8f + i) * 2f);
                float a = cloudAlpha * 0.34f * MathHelper.Clamp(cloudGrow, 0f, 1f);
                spriteBatch.Draw(fog, center + off * cloudScale, null, tint * a, i * 1.7f + t * 0.3f,
                    fog.Size() / 2f, new Vector2(0.62f, 0.40f) * cloudScale * (0.8f + i * 0.12f), SpriteEffects.None, 0f);
            }
        }

        /// <summary>伴飞云鱼群</summary>
        private void DrawCloudFishParticles(SpriteBatch sb) {
            //加载鱼的纹理
            Main.instance.LoadItem(ItemID.Cloudfish);
            Texture2D tex = TextureAssets.Item[ItemID.Cloudfish].Value;

            foreach (var fish in cloudFishParticles) {
                if (fish.Alpha <= 0.02f) continue;
                Vector2 pos = fish.Position - Main.screenPosition;
                Rectangle src = tex.GetRectangle();
                Vector2 origin = src.Size() / 2f;

                //根据速度判断朝向
                int dir = fish.Velocity.X >= 0 ? 1 : -1;
                SpriteEffects fx = dir > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically;

                float rot = fish.Rotation + (dir > 0 ? MathHelper.PiOver4 : -MathHelper.PiOver4);
                float alpha = fish.Alpha * (0.6f + 0.4f * cloudAlpha); //独立透明度，不完全受云体影响

                //拖尾
                if (fish.Velocity.Length() > 4f) {
                    for (int t = 1; t <= 3; t++) {
                        Vector2 trail = -fish.Velocity.SafeNormalize(Vector2.Zero) * t * 7f;
                        float trailA = alpha * (1f - t / 3f) * 0.5f;
                        sb.Draw(tex, pos + trail, src, fish.Color * trailA, rot, origin, fish.Scale * (0.9f - t * 0.05f), fx, 0f);
                    }
                }
                //主体
                sb.Draw(tex, pos, src, fish.Color * alpha, rot, origin, fish.Scale, fx, 0f);
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.netMode != NetmodeID.Server) {
                //残云余韵
                for (int i = 0; i < 12; i++) {
                    Vector2 off = new Vector2(Main.rand.NextFloat(-85f, 85f), Main.rand.NextFloat(-30f, 30f));
                    PRTLoader.NewParticle<PRT_FishCloudWisp>(Projectile.Center + off,
                        off * 0.02f + new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(-0.5f, -0.1f)),
                        WispColor(Main.rand.NextFloat()), Main.rand.NextFloat(0.20f, 0.36f))
                        ?.Configure(Main.rand.Next(45, 75));
                }
                //云尘底噪
                for (int i = 0; i < 10; i++) {
                    Vector2 pos = Projectile.Center + new Vector2(Main.rand.NextFloat(-80f, 80f), Main.rand.NextFloat(-35f, 35f));
                    Dust d = Dust.NewDustPerfect(pos, DustID.Cloud, Main.rand.NextVector2Circular(3f, 2f), Scale: Main.rand.NextFloat(1.5f, 2.6f));
                    d.noGravity = true;
                    d.alpha = 120;
                }
                //云鱼散作雾点
                foreach (var fish in cloudFishParticles) {
                    Dust d = Dust.NewDustPerfect(fish.Position, DustID.Cloud, Main.rand.NextVector2Circular(2f, 2f), Scale: Main.rand.NextFloat(0.8f, 1.2f));
                    d.noGravity = true;
                    d.color = fish.Color;
                    d.alpha = 120;
                }
            }

            //恢复玩家状态
            if (Owner.active) {
                Owner.gravity = Player.defaultGravity;
                Owner.fullRotation = 0f;
            }

            //音效
            SoundEngine.PlaySound(SoundID.Item96 with { Volume = 0.6f, Pitch = -0.2f }, Projectile.Center);
        }
    }

    /// <summary>云鱼粒子数据结构（伴飞的云鱼）</summary>
    internal struct CloudFishParticle
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Scale;
        public float Rotation;
        public float Alpha;
        public int FishID;
        public float BehaviorRandomness;
        public float PhaseOffset;
        public Color Color;
    }

    /// <summary>雨滴弹幕</summary>
    internal class CloudRain : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override void SetDefaults() {
            Projectile.width = 4;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 180;
            Projectile.alpha = 50;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            //重力加速
            Projectile.velocity.Y += 0.3f;
            if (Projectile.velocity.Y > 16f) {
                Projectile.velocity.Y = 16f;
            }

            //雨滴轨迹
            if (Main.rand.NextBool(3)) {
                Dust rainDust = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Water,
                    Projectile.velocity * 0.2f,
                    Scale: Main.rand.NextFloat(0.4f, 0.8f)
                );
                rainDust.noGravity = true;
                rainDust.alpha = 150;
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.netMode != NetmodeID.Server) {
                //溅斑
                PRTLoader.NewParticle<PRT_FishCloudSplash>(Projectile.Center, Vector2.Zero,
                    new Color(178, 198, 222), 1f)?.Configure(Main.rand.Next(12, 17), Main.rand.NextFloat(0.10f, 0.15f));
                //迸起水珠
                for (int i = 0; i < 3; i++) {
                    Dust splash = Dust.NewDustPerfect(
                        Projectile.Center,
                        DustID.Water,
                        new Vector2(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-3f, -1f)),
                        Scale: Main.rand.NextFloat(0.6f, 1f)
                    );
                    splash.noGravity = false;
                }
            }

            SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.3f, Pitch = Main.rand.NextFloat(-0.2f, 0.2f) }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            //雨线
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float fade = 1f - Projectile.alpha / 255f;
            Texture2D px = VaultAsset.placeholder2.Value;
            float len = MathHelper.Clamp(Projectile.velocity.Length() * 1.5f, 8f, 22f);

            Main.spriteBatch.Draw(px, drawPos, new Rectangle(0, 0, 1, 1), new Color(128, 150, 178) * (0.42f * fade),
                Projectile.rotation, new Vector2(0.5f, 0f), new Vector2(1.3f, len), SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(px, drawPos, new Rectangle(0, 0, 1, 1), new Color(198, 214, 234) * (0.85f * fade),
                Projectile.rotation, new Vector2(0.5f, 0f), new Vector2(1.6f, len * 0.32f), SpriteEffects.None, 0f);

            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //击中水花特效
            for (int i = 0; i < 3; i++) {
                Dust hitSplash = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Water,
                    Main.rand.NextVector2Circular(2f, 2f),
                    Scale: Main.rand.NextFloat(0.8f, 1.2f)
                );
                hitSplash.noGravity = true;
            }
        }
    }
}
