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

        public override int DefaultCooldown => 60 * 15; // 15秒冷却

        public override bool? AltFunctionUse(Item item, Player player) {
            return true;
        }

        public override bool? CanUseItem(Item item, Player player) {
            if (player.altFunctionUse == 2) {
                //检查冷却
                if (Cooldown > 0) {
                    return false;
                }

                item.UseSound = null;
                Use(item, player);
                return false;
            }

            return base.CanUseItem(item, player);
        }

        public override void Use(Item item, Player player) {
            //设置冷却
            SetCooldown();

            //生成云朵弹幕
            int cloudProj = Projectile.NewProjectile(
                player.GetSource_ItemUse(item),
                player.Center + new Vector2(0, -100), // 在玩家上方生成
                Vector2.Zero,
                ModContent.ProjectileType<CloudRide>(),
                0,
                0f,
                player.whoAmI
            );

            //播放召唤音效
            SoundEngine.PlaySound(SoundID.Item96 with { Volume = 0.8f, Pitch = 0.2f }, player.Center); // 柔和的云雾音效
            SoundEngine.PlaySound(SoundID.DD2_BetsyWindAttack with { Volume = 0.5f, Pitch = 0.5f }, player.Center); // 风声
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

        public override void SetDefaults() {
            Projectile.width = 120;
            Projectile.height = 60;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = MaxDuration + 120; // 额外时间用于消散
        }

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Phase = 2;
            }

            LifeTimer++;

            //更新云朵粒子
            UpdateCloudParticles();

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
        /// 阶段0：飞向玩家脚下
        /// </summary>
        private void FlyToPlayerPhase() {
            //淡入
            cloudAlpha += 0.08f;
            if (cloudAlpha > 1f) cloudAlpha = 1f;

            cloudScale += 0.05f;
            if (cloudScale > 1f) cloudScale = 1f;

            //计算目标位置（玩家脚下）
            targetPosition = Owner.Bottom + new Vector2(0, 20);

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

                //播放到位音效
                SoundEngine.PlaySound(SoundID.Item45 with { Volume = 0.5f, Pitch = 0.4f }, Projectile.Center);
            }

            //生成飞行粒子
            if (Main.rand.NextBool(3)) {
                SpawnCloudParticle(Main.rand.NextVector2Circular(60f, 30f), new Vector2(0, Main.rand.NextFloat(0.5f, 1.5f)));
            }
        }

        /// <summary>
        /// 阶段1：载着玩家飞行
        /// </summary>
        private void RidingPhase() {
            cloudAlpha = 1f;
            cloudScale = 1f + (float)Math.Sin(LifeTimer * 0.1f) * 0.05f; // 轻微呼吸效果

            //计算朝向光标的方向
            Vector2 toMouse = (Main.MouseWorld - Projectile.Center).SafeNormalize(Vector2.Zero);

            //加速飞行
            float acceleration = 1.2f;
            float maxSpeed = 25f;

            Projectile.velocity += toMouse * acceleration;

            //速度限制
            if (Projectile.velocity.Length() > maxSpeed) {
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * maxSpeed;
            }

            //玩家位置跟随云朵
            Owner.position = Projectile.Center + new Vector2(0, -30) - Owner.Size / 2f;
            Owner.velocity = Projectile.velocity;
            Owner.fallStart = (int)(Owner.position.Y / 16f);
            Owner.gravity = 0f;
            Owner.noFallDmg = true;

            //持续生成云朵粒子
            if (Main.rand.NextBool(2)) {
                Vector2 spawnOffset = Main.rand.NextVector2Circular(80f, 40f);
                Vector2 particleVel = -Projectile.velocity * 0.3f + new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(0.5f, 1.5f));
                SpawnCloudParticle(spawnOffset, particleVel);
            }

            //生成飞行轨迹特效
            if (Main.rand.NextBool(4)) {
                Dust trail = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(60f, 30f),
                    DustID.Cloud,
                    -Projectile.velocity * 0.2f,
                    Scale: Main.rand.NextFloat(1.5f, 2.5f)
                );
                trail.noGravity = true;
                trail.alpha = 100;
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

            //恢复玩家重力
            if (Owner.active) {
                Owner.gravity = Player.defaultGravity;
            }

            //减速
            Projectile.velocity *= 0.95f;

            //粒子消散效果
            if (Main.rand.NextBool(2)) {
                Vector2 dissipateVel = Main.rand.NextVector2Circular(2f, 2f);
                SpawnCloudParticle(Main.rand.NextVector2Circular(80f, 40f), dissipateVel);
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
                //在云朵底部随机位置生成雨滴
                Vector2 rainSpawnPos = Projectile.Center + new Vector2(
                    Main.rand.NextFloat(-60f, 60f),
                    30f
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
            if (Main.rand.NextBool(5)) {
                Dust mist = Dust.NewDustPerfect(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-70f, 70f), 35f),
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
                Scale = Main.rand.NextFloat(0.6f, 1.2f),
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi),
                RotationSpeed = Main.rand.NextFloat(-0.03f, 0.03f),
                Alpha = Main.rand.NextFloat(0.4f, 0.8f),
                LifeTime = 0,
                MaxLifeTime = Main.rand.Next(60, 120),
                Color = Color.Lerp(Color.White, new Color(220, 240, 255), Main.rand.NextFloat())
            });

            //限制粒子数量
            if (cloudParticles.Count > 150) {
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

                //重力和阻力
                particle.Velocity.Y += 0.05f;
                particle.Velocity *= 0.98f;

                //生命周期淡出
                float lifeRatio = particle.LifeTime / (float)particle.MaxLifeTime;
                if (lifeRatio > 0.7f) {
                    particle.Alpha *= 0.95f;
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

            SpriteBatch spriteBatch = Main.spriteBatch;

            //=== 绘制云朵粒子 ===
            foreach (CloudParticle particle in cloudParticles) {
                Vector2 drawPos = particle.Position - Main.screenPosition;
                float drawAlpha = particle.Alpha * cloudAlpha;

                //多层绘制，模拟云的厚度和柔和感
                for (int layer = 0; layer < 3; layer++) {
                    float layerScale = particle.Scale * (1f + layer * 0.15f);
                    float layerAlpha = drawAlpha * (0.6f - layer * 0.15f);

                    spriteBatch.Draw(
                        FishCloud.Fog,
                        drawPos,
                        null,
                        particle.Color * layerAlpha,
                        particle.Rotation,
                        FishCloud.Fog.Size() / 2f,
                        layerScale * cloudScale,
                        SpriteEffects.None,
                        0f
                    );
                }
            }

            //=== 绘制主云体（筋斗云核心） ===
            Vector2 cloudCenter = Projectile.Center - Main.screenPosition;

            //云朵核心 - 多层堆叠
            for (int i = 0; i < 8; i++) {
                float angle = MathHelper.TwoPi * i / 8f + LifeTimer * 0.02f;
                float radius = 40f + (float)Math.Sin(LifeTimer * 0.08f + i) * 8f;

                Vector2 cloudPartPos = cloudCenter + new Vector2(
                    (float)Math.Cos(angle) * radius,
                    (float)Math.Sin(angle) * radius * 0.6f // 扁平化
                );

                //多层云雾叠加
                for (int layer = 0; layer < 4; layer++) {
                    float layerScale = (1.2f + layer * 0.3f) * cloudScale;
                    float layerAlpha = cloudAlpha * (0.5f - layer * 0.08f);
                    float layerRotation = angle + layer * 0.3f;

                    Color cloudColor = Color.Lerp(
                        new Color(255, 255, 255),
                        new Color(200, 230, 255),
                        (float)Math.Sin(LifeTimer * 0.1f + i) * 0.5f + 0.5f
                    );

                    spriteBatch.Draw(
                        FishCloud.Fog,
                        cloudPartPos,
                        null,
                        cloudColor * layerAlpha,
                        layerRotation,
                        FishCloud.Fog.Size() / 2f,
                        layerScale,
                        SpriteEffects.None,
                        0f
                    );
                }
            }

            //=== 筋斗云经典黄金边缘效果 ===
            if (Phase == 1) {
                for (int i = 0; i < 6; i++) {
                    float angle = MathHelper.TwoPi * i / 6f + LifeTimer * 0.03f;
                    float radius = 50f;

                    Vector2 edgePos = cloudCenter + new Vector2(
                        (float)Math.Cos(angle) * radius,
                        (float)Math.Sin(angle) * radius * 0.5f
                    );

                    Color goldenEdge = new Color(255, 240, 180) * (cloudAlpha * 0.4f);

                    spriteBatch.Draw(
                        FishCloud.Fog,
                        edgePos,
                        null,
                        goldenEdge,
                        angle,
                        FishCloud.Fog.Size() / 2f,
                        0.8f * cloudScale,
                        SpriteEffects.None,
                        0f
                    );
                }

                //内部发光核心
                for (int i = 0; i < 3; i++) {
                    float glowScale = (0.6f + i * 0.2f) * cloudScale;
                    float glowAlpha = cloudAlpha * (0.3f - i * 0.08f);

                    spriteBatch.Draw(
                        FishCloud.Fog,
                        cloudCenter,
                        null,
                        new Color(255, 250, 200) * glowAlpha,
                        LifeTimer * 0.05f,
                        FishCloud.Fog.Size() / 2f,
                        glowScale,
                        SpriteEffects.None,
                        0f
                    );
                }
            }

            //=== 速度拖尾效果 ===
            if (Phase == 1 && Projectile.velocity.Length() > 10f) {
                for (int i = 1; i <= 5; i++) {
                    Vector2 trailPos = cloudCenter - Projectile.velocity.SafeNormalize(Vector2.Zero) * i * 15f;
                    float trailAlpha = cloudAlpha * (1f - i / 5f) * 0.4f;

                    spriteBatch.Draw(
                        FishCloud.Fog,
                        trailPos,
                        null,
                        Color.White * trailAlpha,
                        LifeTimer * 0.02f,
                        FishCloud.Fog.Size() / 2f,
                        cloudScale * (1.2f - i * 0.1f),
                        SpriteEffects.None,
                        0f
                    );
                }
            }

            return false;
        }

        public override void OnKill(int timeLeft) {
            //最终消散特效
            for (int i = 0; i < 30; i++) {
                Dust cloudDust = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(60f, 30f),
                    DustID.Cloud,
                    Main.rand.NextVector2Circular(3f, 3f),
                    Scale: Main.rand.NextFloat(2f, 3.5f)
                );
                cloudDust.noGravity = true;
                cloudDust.alpha = 100;
            }

            //恢复玩家重力
            if (Owner.active) {
                Owner.gravity = Player.defaultGravity;
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
            Vector2 rainEnd = drawPos + Projectile.velocity.SafeNormalize(Vector2.Zero) * 12f;

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
