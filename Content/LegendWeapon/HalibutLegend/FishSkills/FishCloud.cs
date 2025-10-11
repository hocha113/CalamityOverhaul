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
        public static Texture2D Fog;//使用反射加载一个烟雾的灰度图，大小256*256，适合用于复合出云雾效果

        public override int UnlockFishID => ItemID.Cloudfish;

        public override int DefaultCooldown => 60 * (25 - HalibutData.GetDomainLayer() * 2); // 25-2*领域 秒冷却

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
            return player.CountProjectilesOfID<CloudRide>() == 0;
        }
    }

    /// <summary>
    /// 云朵乘骑弹幕 - 玩家骑乘的筋斗云
    /// </summary>
    internal class CloudRide : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        private Player Owner => Main.player[Projectile.owner];

        /// <summary>
        /// 生命计时器
        /// </summary>
        private ref float LifeTimer => ref Projectile.ai[0];

        /// <summary>
        /// 阶段：0=飞向玩家脚下，1=载着玩家飞行，2=消散
        /// </summary>
        private int Phase {
            get => (int)Projectile.ai[1];
            set => Projectile.ai[1] = value;
        }

        /// <summary>
        /// 最大持续时间（8秒）
        /// </summary>
        private const int MaxDuration = 60 * 8;

        /// <summary>
        /// 云朵粒子系统
        /// </summary>
        private List<CloudParticle> cloudParticles = new();

        /// <summary>
        /// 云鱼粒子系统（伴飞的云鱼）
        /// </summary>
        private List<CloudFishParticle> cloudFishParticles = new();

        /// <summary>
        /// 雨滴生成计时器
        /// </summary>
        private int rainTimer = 0;

        /// <summary>
        /// 云朵缩放
        /// </summary>
        private float cloudScale = 0f;

        /// <summary>
        /// 云朵透明度
        /// </summary>
        private float cloudAlpha = 0f;

        /// <summary>
        /// 目标位置（玩家脚下）
        /// </summary>
        private Vector2 targetPosition = Vector2.Zero;

        /// <summary>
        /// 云朵翻滚偏移数组 - 用于模拟云雾翻滚效果
        /// </summary>
        private float[] cloudRollOffsets = new float[12];

        /// <summary>
        /// 云朵翻滚速度数组
        /// </summary>
        private float[] cloudRollSpeeds = new float[12];

        /// <summary>
        /// 玩家原始重力值（用于恢复）
        /// </summary>
        private float originalGravity = 0f;

        /// <summary>
        /// 云鱼数量
        /// </summary>
        private const int CloudFishCount = 15;

        public override void SetDefaults() {
            Projectile.width = 140;
            Projectile.height = 60;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = MaxDuration + 120; // 额外时间用于消散

            //初始化云朵翻滚参数
            for (int i = 0; i < cloudRollOffsets.Length; i++) {
                cloudRollOffsets[i] = Main.rand.NextFloat(0f, MathHelper.TwoPi);
                cloudRollSpeeds[i] = Main.rand.NextFloat(0.02f, 0.05f);
            }
        }

        /// <summary>
        /// 初始化云鱼粒子
        /// </summary>
        private void InitializeCloudFish() {
            cloudFishParticles.Clear();
            for (int i = 0; i < CloudFishCount; i++) {
                //在云朵周围随机位置生成云鱼
                float angle = MathHelper.TwoPi * i / CloudFishCount + Main.rand.NextFloat(-0.25f, 0.25f);
                float distance = Main.rand.NextFloat(90f, 150f);

                Vector2 spawnOffset = new Vector2(
                    (float)Math.Cos(angle) * distance,
                    (float)Math.Sin(angle) * distance * 0.55f // 扁平分布
                );

                cloudFishParticles.Add(new CloudFishParticle {
                    Position = Projectile.Center + spawnOffset,
                    Velocity = Main.rand.NextVector2Circular(2f, 1f),
                    Scale = Main.rand.NextFloat(0.8f, 1.25f), // 增大基础尺寸
                    Rotation = angle,
                    Alpha = 0f, // 初始透明，逐渐淡入
                    FishID = i,
                    BehaviorRandomness = Main.rand.NextFloat(0.85f, 1.25f),
                    PhaseOffset = Main.rand.NextFloat(MathHelper.TwoPi),
                    Color = Color.Lerp(new Color(190, 225, 255), new Color(255, 255, 255), Main.rand.NextFloat())
                });
            }
        }

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Phase = 2;
            }

            if (LifeTimer == 0) {
                //初始化云鱼粒子
                InitializeCloudFish();
            }

            LifeTimer++;

            //更新云朵翻滚效果
            UpdateCloudRolling();

            //更新云朵粒子
            UpdateCloudParticles();

            //更新云鱼粒子
            UpdateCloudFishParticles();

            switch (Phase) {
                case 0: // 飞向玩家脚下
                    FlyToPlayerPhase();
                    break;
                case 1: // 载着玩家飞行
                    RidingPhase();
                    break;
                case 2: // 消散
                    DissipatePhase();
                    break;
            }

            //生成雨滴
            if (Phase == 1) {
                SpawnRain();
            }

            //环境音效
            if (Phase == 1 && LifeTimer % 120 == 0) {
                SoundEngine.PlaySound(SoundID.LiquidsWaterLava with { Volume = 0.3f, Pitch = 0.3f }, Projectile.Center);
            }
        }

        /// <summary>
        /// 更新云朵翻滚效果
        /// </summary>
        private void UpdateCloudRolling() {
            for (int i = 0; i < cloudRollOffsets.Length; i++) {
                cloudRollOffsets[i] += cloudRollSpeeds[i];
                if (cloudRollOffsets[i] > MathHelper.TwoPi) {
                    cloudRollOffsets[i] -= MathHelper.TwoPi;
                }
            }
        }

        /// <summary>
        /// 更新云鱼粒子（鱼群算法）
        /// </summary>
        private void UpdateCloudFishParticles() {
            for (int i = 0; i < cloudFishParticles.Count; i++) {
                CloudFishParticle fish = cloudFishParticles[i];

                //淡入效果
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

                //=== 鱼群算法实现 ===
                Vector2 separationForce = Vector2.Zero;
                Vector2 alignmentForce = Vector2.Zero;
                Vector2 cohesionForce = Vector2.Zero;

                Vector2 centerOfMass = Projectile.Center;
                Vector2 averageVelocity = Vector2.Zero;
                int nearbyFishCount = 0;

                //计算与其他云鱼的关系
                for (int j = 0; j < cloudFishParticles.Count; j++) {
                    if (i == j) continue;

                    CloudFishParticle otherFish = cloudFishParticles[j];
                    float distance = Vector2.Distance(fish.Position, otherFish.Position);

                    //分离力：避免碰撞
                    if (distance < 40f && distance > 0.1f) {
                        Vector2 awayFromOther = (fish.Position - otherFish.Position).SafeNormalize(Vector2.Zero);
                        separationForce += awayFromOther / distance;
                    }

                    //对齐和聚合计算
                    if (distance < 120f) {
                        centerOfMass += otherFish.Position;
                        averageVelocity += otherFish.Velocity;
                        nearbyFishCount++;
                    }
                }

                if (nearbyFishCount > 0) {
                    centerOfMass /= nearbyFishCount;
                    averageVelocity /= nearbyFishCount;

                    //对齐力：向平均方向移动
                    alignmentForce = (averageVelocity - fish.Velocity) * 0.1f;

                    //聚合力：向群体中心移动
                    cohesionForce = (centerOfMass - fish.Position).SafeNormalize(Vector2.Zero) * 0.3f;
                }

                //=== 围绕云朵运动的核心力 ===
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

                //=== 切向速度（围绕云朵旋转） ===
                //根据云朵移动方向调整旋转方向
                Vector2 tangentialDirection = new Vector2(-directionToCloud.Y, directionToCloud.X);
                
                //当云朵高速移动时，云鱼在后方加速追赶
                float cloudSpeed = Projectile.velocity.Length();
                bool isBehindCloud = Vector2.Dot(toCloud, Projectile.velocity) > 0;
                float catchUpBoost = (isBehindCloud && cloudSpeed > 5f) ? 2.0f : 1.0f;

                Vector2 tangentialForce = tangentialDirection * (2.5f + (float)Math.Sin(LifeTimer * 0.05f + fish.PhaseOffset) * 0.8f) * catchUpBoost;

                //=== 跟随云朵的速度同步 ===
                Vector2 velocitySync = Projectile.velocity * 0.6f;

                //=== 波动效果（上下摆动） ===
                float waveTime = LifeTimer * 0.08f + fish.PhaseOffset;
                Vector2 waveForce = new Vector2(0, (float)Math.Sin(waveTime) * 0.4f * fish.BehaviorRandomness);

                //=== 随机游动 ===
                Vector2 randomWander = new Vector2(
                    (float)Math.Sin(waveTime * 1.3f),
                    (float)Math.Cos(waveTime * 1.7f)
                ) * 0.3f * fish.BehaviorRandomness;

                //=== 合成所有力 ===
                Vector2 totalForce = Vector2.Zero;
                totalForce += separationForce * 2.5f; // 分离力（避免重叠）
                totalForce += alignmentForce * 1.2f; // 对齐力
                totalForce += cohesionForce * 0.8f; // 聚合力
                totalForce += orbitForce * 1.5f; // 轨道力
                totalForce += tangentialForce; // 切向运动
                totalForce += velocitySync; // 速度同步
                totalForce += waveForce; // 波动
                totalForce += randomWander; // 随机游动

                //应用力
                fish.Velocity += totalForce * 0.15f;

                //速度限制
                float maxSpeed = 8f * fish.BehaviorRandomness;
                float minSpeed = 2f;
                float currentSpeed = fish.Velocity.Length();

                if (currentSpeed > maxSpeed) {
                    fish.Velocity = fish.Velocity.SafeNormalize(Vector2.Zero) * maxSpeed;
                }
                else if (currentSpeed < minSpeed && currentSpeed > 0.1f) {
                    fish.Velocity = fish.Velocity.SafeNormalize(Vector2.Zero) * minSpeed;
                }

                //更新位置
                fish.Position += fish.Velocity;
                fish.Position += Projectile.velocity * 0.8f;

                //更新旋转（面向移动方向）
                if (fish.Velocity.LengthSquared() > 0.1f) {
                    fish.Rotation = MathHelper.Lerp(fish.Rotation, fish.Velocity.ToRotation(), 0.2f);
                }

                //轻微的游动摆尾效果
                fish.Rotation += (float)Math.Sin(LifeTimer * 0.2f + fish.PhaseOffset) * 0.08f;

                //更新回列表
                cloudFishParticles[i] = fish;
            }
        }

        /// <summary>
        /// 阶段0：飞向玩家脚下
        /// </summary>
        private void FlyToPlayerPhase() {
            //淡入
            cloudAlpha += 0.08f;
            if (cloudAlpha > 1f) cloudAlpha = 1f;

            cloudScale += 0.05f;
            if (cloudScale > 1f) cloudScale = 1f;

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

                //播放到位音效
                SoundEngine.PlaySound(SoundID.Item45 with { Volume = 0.5f, Pitch = 0.4f }, Projectile.Center);
            }

            //生成飞行粒子
            if (Main.rand.NextBool(2)) {
                SpawnCloudParticle(Main.rand.NextVector2Circular(70f, 35f), new Vector2(0, Main.rand.NextFloat(0.5f, 1.5f)));
            }
        }

        /// <summary>
        /// 阶段1：载着玩家飞行
        /// </summary>
        private void RidingPhase() {
            cloudAlpha = 1f;
            cloudScale = 1f + (float)Math.Sin(LifeTimer * 0.08f) * 0.06f; // 轻微呼吸效果

            //计算朝向光标的方向
            Vector2 toMouse = (Main.MouseWorld - Projectile.Center).SafeNormalize(Vector2.Zero);

            //加速飞行
            float acceleration = 1.2f;
            float maxSpeed = 25f;

            Projectile.velocity += toMouse * acceleration;

            //速度限制
            float currentSpeed = Projectile.velocity.Length();
            if (currentSpeed > maxSpeed) {
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * maxSpeed;
            }

            //=== 玩家骑乘效果 ===
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

            //持续生成云朵粒子（更频繁，更动态）
            if (Main.rand.NextBool(1)) {
                Vector2 spawnOffset = new Vector2(
                    Main.rand.NextFloat(-90f, 90f),
                    Main.rand.NextFloat(-25f, 25f)
                );
                
                //粒子速度包含翻滚效果
                Vector2 particleVel = -Projectile.velocity * 0.4f + new Vector2(
                    Main.rand.NextFloat(-1.5f, 1.5f),
                    Main.rand.NextFloat(-0.5f, 1.5f)
                );
                
                SpawnCloudParticle(spawnOffset, particleVel);
            }

            //生成飞行轨迹特效（两侧散开）
            if (Main.rand.NextBool(3)) {
                float sideOffset = Main.rand.NextBool() ? -1f : 1f;
                Vector2 trailPos = Projectile.Center + new Vector2(
                    Main.rand.NextFloat(40f, 70f) * sideOffset,
                    Main.rand.NextFloat(-20f, 20f)
                );

                Dust trail = Dust.NewDustPerfect(
                    trailPos,
                    DustID.Cloud,
                    -Projectile.velocity * 0.3f + new Vector2(sideOffset * 2f, 0),
                    Scale: Main.rand.NextFloat(1.8f, 2.8f)
                );
                trail.noGravity = true;
                trail.alpha = 120;
            }

            //速度线效果（高速时）
            if (currentSpeed > 15f && Main.rand.NextBool(4)) {
                Vector2 speedLinePos = Projectile.Center + Main.rand.NextVector2Circular(60f, 30f);
                Dust speedLine = Dust.NewDustPerfect(
                    speedLinePos,
                    DustID.Smoke,
                    -Projectile.velocity.SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(3f, 6f),
                    Scale: Main.rand.NextFloat(0.8f, 1.2f)
                );
                speedLine.noGravity = true;
                speedLine.alpha = 180;
                speedLine.color = Color.Lerp(Color.White, new Color(200, 230, 255), 0.5f);
            }

            //超时检查
            if (LifeTimer > MaxDuration) {
                Phase = 2;
            }
        }

        /// <summary>
        /// 阶段2：消散
        /// </summary>
        private void DissipatePhase() {
            cloudAlpha -= 0.05f;
            cloudScale += 0.02f;

            //恢复玩家状态
            if (Owner.active) {
                Owner.gravity = Player.defaultGravity;
                Owner.fullRotation = MathHelper.Lerp(Owner.fullRotation, 0f, 0.25f);
            }

            //减速
            Projectile.velocity *= 0.95f;

            //粒子消散效果
            if (Main.rand.NextBool(1)) {
                Vector2 dissipateVel = Main.rand.NextVector2Circular(2f, 2f);
                SpawnCloudParticle(Main.rand.NextVector2Circular(90f, 45f), dissipateVel);
            }

            if (cloudAlpha <= 0f) {
                Projectile.Kill();
            }
        }

        /// <summary>
        /// 生成雨滴
        /// </summary>
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
                    (int)(Owner.GetShootState().WeaponDamage * 0.5f),
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

        /// <summary>
        /// 生成云朵粒子
        /// </summary>
        private void SpawnCloudParticle(Vector2 offset, Vector2 velocity) {
            cloudParticles.Add(new CloudParticle {
                Position = Projectile.Center + offset,
                Velocity = velocity,
                Scale = Main.rand.NextFloat(0.5f, 1.4f),
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi),
                RotationSpeed = Main.rand.NextFloat(-0.04f, 0.04f),
                Alpha = Main.rand.NextFloat(0.3f, 0.7f),
                LifeTime = 0,
                MaxLifeTime = Main.rand.Next(50, 100),
                Color = Color.Lerp(Color.White, new Color(215, 235, 255), Main.rand.NextFloat())
            });

            //限制粒子数量
            if (cloudParticles.Count > 200) {
                cloudParticles.RemoveAt(0);
            }
        }

        /// <summary>
        /// 更新云朵粒子
        /// </summary>
        private void UpdateCloudParticles() {
            for (int i = cloudParticles.Count - 1; i >= 0; i--) {
                CloudParticle particle = cloudParticles[i];
                particle.LifeTime++;

                //更新位置和旋转
                particle.Position += particle.Velocity;
                particle.Rotation += particle.RotationSpeed;

                //轻微重力和阻力
                particle.Velocity.Y += 0.03f;
                particle.Velocity *= 0.99f;

                //生命周期淡出
                float lifeRatio = particle.LifeTime / (float)particle.MaxLifeTime;
                if (lifeRatio > 0.6f) {
                    particle.Alpha *= 0.96f;
                }

                //更新回列表
                cloudParticles[i] = particle;

                //移除死亡粒子
                if (particle.LifeTime >= particle.MaxLifeTime || particle.Alpha <= 0.05f) {
                    cloudParticles.RemoveAt(i);
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (FishCloud.Fog == null) return false;

            SpriteBatch sb = Main.spriteBatch;
            Vector2 center = Projectile.Center - Main.screenPosition;

            //=== 绘制主云体（扁平筋斗云形状） ===
            DrawMainCloudBody(sb, center);

            //=== 绘制云朵粒子（翻滚效果） ===
            DrawCloudParticles(sb);

            //=== 绘制速度拖尾效果 ===
            if (Phase == 1 && Projectile.velocity.Length() > 10f) {
                DrawSpeedTrail(sb, center);
            }

            //=== 绘制筋斗云黄金边缘效果 ===
            if (Phase == 1) {
                DrawGoldenEdges(sb, center);
            }

            //=== 绘制云鱼粒子（伴飞效果） ===
            DrawCloudFishParticles(sb);

            return false;
        }

        /// <summary>
        /// 绘制主云体（扁平椭圆形，多层翻滚）
        /// </summary>
        private void DrawMainCloudBody(SpriteBatch sb, Vector2 center) {
            //云朵主体由多个扁平椭圆云团组成
            int cloudSegments = 12;
            
            for (int i = 0; i < cloudSegments; i++) {
                //计算云团位置（扁平椭圆分布）
                float angle = MathHelper.TwoPi * i / cloudSegments;
                
                //使用翻滚偏移创建动态效果
                float rollOffset = cloudRollOffsets[i];
                float radiusX = 55f + (float)Math.Sin(rollOffset) * 15f; // 横向半径大
                float radiusY = 25f + (float)Math.Cos(rollOffset * 1.3f) * 8f; // 纵向半径小（扁平）

                Vector2 cloudPartPos = center + new Vector2(
                    (float)Math.Cos(angle + LifeTimer * 0.015f) * radiusX,
                    (float)Math.Sin(angle + LifeTimer * 0.015f) * radiusY
                );

                //多层云雾叠加（4层）
                for (int layer = 0; layer < 4; layer++) {
                    float layerScale = (1.3f + layer * 0.25f) * cloudScale;
                    float layerAlpha = cloudAlpha * (0.45f - layer * 0.07f);
                    
                    //每层旋转略有不同，增加厚度感
                    float layerRotation = angle + rollOffset * 0.5f + layer * 0.4f;

                    //云朵颜色随时间和层数变化
                    Color cloudColor = Color.Lerp(
                        new Color(255, 255, 255),
                        new Color(200, 230, 255),
                        (float)Math.Sin(rollOffset + i * 0.5f) * 0.3f + 0.3f
                    );

                    sb.Draw(
                        FishCloud.Fog,
                        cloudPartPos,
                        null,
                        cloudColor * layerAlpha,
                        layerRotation,
                        FishCloud.Fog.Size() / 2f,
                        new Vector2(layerScale, layerScale * 0.75f), // X轴较大，Y轴较小（扁平）
                        SpriteEffects.None,
                        0f
                    );
                }
            }

            //中心核心云团（最厚实的部分）
            for (int i = 0; i < 3; i++) {
                float coreScale = (1.5f + i * 0.3f) * cloudScale;
                float coreAlpha = cloudAlpha * (0.5f - i * 0.1f);
                float coreRotation = LifeTimer * 0.02f + i * 0.8f;

                sb.Draw(
                    FishCloud.Fog,
                    center,
                    null,
                    Color.White * coreAlpha,
                    coreRotation,
                    FishCloud.Fog.Size() / 2f,
                    new Vector2(coreScale, coreScale * 0.6f), // 扁平核心
                    SpriteEffects.None,
                    0f
                );
            }
        }

        /// <summary>
        /// 绘制云朵粒子（翻滚云雾效果）
        /// </summary>
        private void DrawCloudParticles(SpriteBatch sb) {
            foreach (var p in cloudParticles) {
                Vector2 pos = p.Position - Main.screenPosition;
                float baseAlpha = p.Alpha * cloudAlpha;

                //多层绘制，模拟云的厚度和柔和感
                for (int layer = 0; layer < 3; layer++) {
                    float s = p.Scale * (1f + layer * 0.18f);
                    float a = baseAlpha * (0.55f - layer * 0.12f);

                    //扁平化粒子
                    sb.Draw(
                        FishCloud.Fog,
                        pos,
                        null,
                        p.Color * a,
                        p.Rotation,
                        FishCloud.Fog.Size() / 2f,
                        new Vector2(s, s * 0.7f) * cloudScale,
                        SpriteEffects.None,
                        0f
                    );
                }
            }
        }

        /// <summary>
        /// 绘制云鱼粒子（伴飞的云鱼群）
        /// </summary>
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
                float alpha = fish.Alpha * (0.6f + 0.4f * cloudAlpha); // 独立透明度，不完全受云体影响

                // 拖尾
                if (fish.Velocity.Length() > 4f) {
                    for (int t = 1; t <= 3; t++) {
                        Vector2 trail = -fish.Velocity.SafeNormalize(Vector2.Zero) * t * 7f;
                        float trailA = alpha * (1f - t / 3f) * 0.5f;
                        sb.Draw(tex, pos + trail, src, fish.Color * trailA, rot, origin, fish.Scale * (0.9f - t * 0.05f), fx, 0f);
                    }
                }
                // 主体
                sb.Draw(tex, pos, src, fish.Color * alpha, rot, origin, fish.Scale, fx, 0f);
                // 发光层
                float speedGlow = fish.Velocity.Length();
                if (speedGlow > 5f) {
                    float gA = (speedGlow - 5f) / 4f * 0.4f * alpha;
                    sb.Draw(tex, pos, src, Color.Lerp(Color.White, Color.Cyan, 0.5f) * gA, rot, origin, fish.Scale * 1.2f, fx, 0f);
                }
            }
        }

        /// <summary>
        /// 绘制黄金边缘效果（筋斗云经典特征）
        /// </summary>
        private void DrawGoldenEdges(SpriteBatch sb, Vector2 center) {
            for (int i = 0; i < 8; i++) {
                float angle = MathHelper.TwoPi * i / 8f + LifeTimer * 0.025f;
                Vector2 pos = center + new Vector2((float)Math.Cos(angle) * 65f, (float)Math.Sin(angle) * 32f);
                sb.Draw(FishCloud.Fog, pos, null, new Color(255, 245, 180) * (cloudAlpha * 0.35f), angle, FishCloud.Fog.Size() / 2f, new Vector2(0.9f, 0.6f) * cloudScale, SpriteEffects.None, 0f);
            }
            for (int i = 0; i < 3; i++) {
                float s = (0.7f + i * 0.2f) * cloudScale; float a = cloudAlpha * (0.28f - i * 0.06f);
                sb.Draw(FishCloud.Fog, center, null, new Color(255, 250, 210) * a, LifeTimer * 0.04f, FishCloud.Fog.Size() / 2f, new Vector2(s, s * 0.65f), SpriteEffects.None, 0f);
            }
        }

        /// <summary>
        /// 绘制速度拖尾
        /// </summary>
        private void DrawSpeedTrail(SpriteBatch sb, Vector2 center) {
            Vector2 dir = -Projectile.velocity.SafeNormalize(Vector2.Zero);
            for (int i = 1; i <= 6; i++) {
                Vector2 pos = center + dir * i * 18f;
                float a = cloudAlpha * (1f - i / 6f) * 0.35f;
                float s = cloudScale * (1.3f - i * 0.12f);

                sb.Draw(FishCloud.Fog, pos, null, Color.White * a, LifeTimer * 0.015f, FishCloud.Fog.Size() / 2f, new Vector2(s, s * 0.65f), SpriteEffects.None, 0f);
            }
        }

        public override void OnKill(int timeLeft) {
            //最终消散特效（扁平分布）
            for (int i = 0; i < 40; i++) {
                Vector2 pos = Projectile.Center + new Vector2(Main.rand.NextFloat(-80f, 80f), Main.rand.NextFloat(-35f, 35f));
                Dust d = Dust.NewDustPerfect(pos, DustID.Cloud, Main.rand.NextVector2Circular(3f, 2f), Scale: Main.rand.NextFloat(2f, 4f));
                d.noGravity = true;
                d.alpha = 100;
            }

            //云鱼消散特效
            foreach (var fish in cloudFishParticles) {
                for (int k = 0; k < 3; k++) {
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

    /// <summary>
    /// 云朵粒子数据结构
    /// </summary>
    internal struct CloudParticle
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Scale;
        public float Rotation;
        public float RotationSpeed;
        public float Alpha;
        public int LifeTime;
        public int MaxLifeTime;
        public Color Color;
    }

    /// <summary>
    /// 云鱼粒子数据结构（伴飞的云鱼）
    /// </summary>
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

    /// <summary>
    /// 雨滴弹幕
    /// </summary>
    internal class CloudRain : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

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
            //雨滴溅射效果
            for (int i = 0; i < 5; i++) {
                Dust splash = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Water,
                    new Vector2(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-3f, -1f)),
                    Scale: Main.rand.NextFloat(0.6f, 1f)
                );
                splash.noGravity = false;
            }

            SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.3f, Pitch = Main.rand.NextFloat(-0.2f, 0.2f) }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            //绘制雨滴（简单的白色线条）
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            Main.spriteBatch.Draw(
                TextureAssets.MagicPixel.Value,
                drawPos,
                new Rectangle(0, 0, 1, 1),
                new Color(180, 220, 255) * (1f - Projectile.alpha / 255f),
                Projectile.rotation,
                Vector2.Zero,
                new Vector2(2f, 12f),
                SpriteEffects.None,
                0f
            );

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
