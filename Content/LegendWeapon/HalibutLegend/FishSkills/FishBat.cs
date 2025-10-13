using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    internal class FishBat : FishSkill
    {
        public override int UnlockFishID => ItemID.Batfish;
        public override int DefaultCooldown => 60 * (20 - HalibutData.GetDomainLayer());
        public bool Active;
        /// <summary>
        /// 蝙蝠群技能最大持续时间
        /// </summary>
        public const int BatSwarmDuration = 1280;

        public override bool? CanUseItem(Item item, Player player) {
            HalibutPlayer halibutPlayer = player.GetOverride<HalibutPlayer>();
            
            if (player.altFunctionUse == 2) {
                // 右键：激活蝙蝠化形
                if (!halibutPlayer.BatSwarmActive && Cooldown <= 0) {
                    item.UseSound = null;
                    Use(item, player);
                    return false;
                }
            }
            else {
                // 左键：消散蝙蝠群
                if (halibutPlayer.BatSwarmActive) {
                    item.UseSound = null;
                    DismissBatSwarm(player, halibutPlayer);
                    return false;
                }
            }
            
            return false;
        }

        public override bool? AltFunctionUse(Item item, Player player) {
            return true;
        }

        public override bool UpdateCooldown(HalibutPlayer halibutPlayer, Player player) {
            // 更新技能状态
            if (halibutPlayer.BatSwarmActive) {
                halibutPlayer.BatSwarmTimer++;
                if (halibutPlayer.BatSwarmTimer >= BatSwarmDuration) {
                    // 技能结束
                    DismissBatSwarm(player, halibutPlayer);
                }
            }
            
            return true;
        }

        public override void Use(Item item, Player player) {
            HalibutPlayer halibutPlayer = player.GetOverride<HalibutPlayer>();

            // 检查技能是否在冷却中
            if (Cooldown > 0 || halibutPlayer.BatSwarmActive) {
                return;
            }

            //SetCooldown();
            
            // 激活技能
            halibutPlayer.BatSwarmActive = true;
            halibutPlayer.BatSwarmTimer = 0;

            // 生成控制器弹幕（管理玩家飞行和技能状态）
            int controller = Projectile.NewProjectile(
                player.GetSource_ItemUse(item),
                player.Center,
                Vector2.Zero,
                ModContent.ProjectileType<BatSwarmController>(),
                0,
                0f,
                player.whoAmI
            );

            // 生成蝙蝠群（30-50只蝙蝠）
            int batCount = Main.rand.Next(30 + 2 * HalibutData.GetDomainLayer(), 50 + 3 * HalibutData.GetDomainLayer());
            
            for (int i = 0; i < batCount; i++) {
                // 在玩家周围随机位置生成蝙蝠
                float angle = MathHelper.TwoPi * i / batCount + Main.rand.NextFloat(-0.3f, 0.3f);
                float distance = Main.rand.NextFloat(40f, 100f);
                Vector2 spawnOffset = new Vector2(
                    (float)Math.Cos(angle) * distance,
                    (float)Math.Sin(angle) * distance
                );

                Vector2 spawnPos = player.Center + spawnOffset;
                Vector2 initialVelocity = Main.rand.NextVector2Circular(5f, 5f);

                int proj = Projectile.NewProjectile(
                    player.GetSource_ItemUse(item),
                    spawnPos,
                    initialVelocity,
                    ModContent.ProjectileType<BatSwarmMinion>(),
                    0,
                    0f,
                    player.whoAmI,
                    ai0: i // 用于区分不同的蝙蝠
                );

                if (Main.projectile[proj].ModProjectile is BatSwarmMinion bat) {
                    bat.OwnerPlayer = player;
                }
            }

            // 播放音效
            SoundEngine.PlaySound(SoundID.NPCHit4 with { 
                Volume = 0.7f, 
                Pitch = -0.3f 
            }, player.Center);
            SoundEngine.PlaySound(SoundID.Zombie20 with { // 蝙蝠声音
                Volume = 0.6f, 
                Pitch = 0.5f 
            }, player.Center);
            
            // 化形特效
            SpawnTransformEffect(player.Center);
        }

        /// <summary>
        /// 消散蝙蝠群
        /// </summary>
        private void DismissBatSwarm(Player player, HalibutPlayer halibutPlayer) {
            halibutPlayer.BatSwarmActive = false;
            halibutPlayer.BatSwarmTimer = 0;
            
            // 杀死所有蝙蝠弹幕
            for (int i = 0; i < Main.maxProjectiles; i++) {
                if (Main.projectile[i].active &&
                    Main.projectile[i].type == ModContent.ProjectileType<BatSwarmMinion>() &&
                    Main.projectile[i].owner == player.whoAmI) {
                    
                    Main.projectile[i].Kill();
                }
            }
            
            // 杀死控制器弹幕
            for (int i = 0; i < Main.maxProjectiles; i++) {
                if (Main.projectile[i].active &&
                    Main.projectile[i].type == ModContent.ProjectileType<BatSwarmController>() &&
                    Main.projectile[i].owner == player.whoAmI) {
                    
                    Main.projectile[i].Kill();
                }
            }
            
            // 消散音效
            SoundEngine.PlaySound(SoundID.NPCDeath4 with { 
                Volume = 0.6f, 
                Pitch = 0.2f 
            }, player.Center);
            
            // 消散特效
            SpawnDismissEffect(player.Center);
        }

        private void SpawnTransformEffect(Vector2 position) {
            // 化形时的黑暗粒子
            for (int i = 0; i < 40; i++) {
                float angle = MathHelper.TwoPi * i / 40f;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(3f, 8f);
                
                Dust dark = Dust.NewDustPerfect(
                    position,
                    DustID.Shadowflame,
                    velocity,
                    100,
                    default,
                    Main.rand.NextFloat(1.5f, 2.5f)
                );
                dark.noGravity = true;
            }
        }

        private void SpawnDismissEffect(Vector2 position) {
            // 消散时的黑暗粒子
            for (int i = 0; i < 30; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(6f, 6f);
                
                Dust dark = Dust.NewDustPerfect(
                    position,
                    DustID.Shadowflame,
                    velocity,
                    100,
                    default,
                    Main.rand.NextFloat(1.2f, 2f)
                );
                dark.noGravity = true;
                dark.fadeIn = 1.3f;
            }
        }
    }

    /// <summary>
    /// 蝙蝠群控制器 - 管理玩家飞行和技能状态
    /// </summary>
    internal class BatSwarmController : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        private Player Owner => Main.player[Projectile.owner];

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = FishBat.BatSwarmDuration;
            Projectile.alpha = 255; // 完全透明
        }

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }

            HalibutPlayer halibutPlayer = Owner.GetOverride<HalibutPlayer>();

            // 检查技能是否结束
            if (!halibutPlayer.BatSwarmActive) {
                Projectile.Kill();
                return;
            }

            // 弹幕位置跟随玩家
            Projectile.Center = Owner.Center;

            // 玩家飞行控制
            Owner.noFallDmg = true;
            Owner.gravity = 0f;
            Owner.maxFallSpeed = 100f;

            Owner.wingTime = 0;

            halibutPlayer.HidePlayerTime = 10;

            // 计算目标速度（朝向光标，允许全方向飞行）
            Vector2 toMouse = (Main.MouseWorld - Owner.Center).SafeNormalize(Vector2.Zero);
            float flySpeed = 20f + HalibutData.GetDomainLayer() * 1.5f; // 基础飞行速度
            
            // 允许玩家通过移动键微调方向
            Vector2 inputDirection = Vector2.Zero;
            if (Owner.controlLeft) inputDirection.X -= 1f;
            if (Owner.controlRight) inputDirection.X += 1f;
            if (Owner.controlUp) inputDirection.Y -= 1f;
            if (Owner.controlDown) inputDirection.Y += 1f;

            Owner.wingTime = 0;
            
            if (inputDirection != Vector2.Zero) {
                inputDirection.Normalize();
                toMouse = Vector2.Lerp(toMouse, inputDirection, 0.4f).SafeNormalize(Vector2.Zero);
            }

            Vector2 targetVelocity = toMouse * flySpeed;

            // 平滑插值
            float lerpSpeed = 0.22f;
            Owner.velocity = Vector2.Lerp(Owner.velocity, targetVelocity, lerpSpeed);
            Owner.direction = Math.Sign(Owner.velocity.X);

            // 飞行粒子效果
            if (Main.rand.NextBool(8)) {
                SpawnFlightParticle();
            }
        }

        private void SpawnFlightParticle() {
            Vector2 dustPos = Owner.Center + Main.rand.NextVector2Circular(20f, 20f);
            Dust flight = Dust.NewDustPerfect(
                dustPos, 
                DustID.Shadowflame,
                -Owner.velocity * 0.3f, 
                Scale: Main.rand.NextFloat(0.8f, 1.3f)
            );
            flight.noGravity = true;
            flight.alpha = 150;
        }

        public override void OnKill(int timeLeft) {
            // 技能结束时恢复玩家重力
            if (Owner != null && Owner.active) {
                Owner.gravity = Player.defaultGravity;
            }
        }
    }

    /// <summary>
    /// 蝙蝠群仆从 - 围绕玩家飞行的蝙蝠
    /// </summary>
    internal class BatSwarmMinion : ModProjectile
    {
        public override string Texture => "Terraria/Images/NPC_" + NPCID.CaveBat;

        /// <summary>
        /// 拥有者玩家
        /// </summary>
        public Player OwnerPlayer { get; set; }

        /// <summary>
        /// 蝙蝠的个体ID
        /// </summary>
        private int BatID => (int)Projectile.ai[0];

        // 群体算法力量
        private Vector2 separationForce = Vector2.Zero;
        private Vector2 alignmentForce = Vector2.Zero;
        private Vector2 cohesionForce = Vector2.Zero;
        private Vector2 randomWander = Vector2.Zero;

        // 个体参数
        private int wanderTimer = 0;
        private float batScale = 1f;
        private float batAlpha = 0f;
        private int batDirection = 1;
        private float batRotation = 0f;
        private float behaviorRandomness = 1f;
        private float wingPhaseOffset = 0f;
        private int lifeTimer = 0;

        // 动画参数
        private int currentFrame = 0;
        private int frameCounter = 0;
        private const int FrameSpeed = 6;

        public override void SetStaticDefaults() {
            Main.projFrames[Projectile.type] = 4; // 4帧动画
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 6;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 22;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = FishBat.BatSwarmDuration + 60;
        }

        public override void AI() {
            // 找到拥有者
            if (OwnerPlayer == null || !OwnerPlayer.active) {
                Projectile.Kill();
                return;
            }

            HalibutPlayer halibutPlayer = OwnerPlayer.GetOverride<HalibutPlayer>();

            // 检查技能是否结束
            if (!halibutPlayer.BatSwarmActive) {
                // 淡出效果
                batAlpha -= 0.08f;
                if (batAlpha <= 0f) {
                    Projectile.Kill();
                    return;
                }
            }
            else {
                // 淡入效果
                if (batAlpha < 1f) {
                    batAlpha += 0.2f;
                    if (batAlpha > 1f) batAlpha = 1f;
                }
            }

            // 初始化参数
            if (lifeTimer == 0) {
                batScale = Main.rand.NextFloat(0.7f, 1.2f);
                behaviorRandomness = Main.rand.NextFloat(0.9f, 1.2f);
                wingPhaseOffset = Main.rand.NextFloat(0f, MathHelper.TwoPi);
            }

            lifeTimer++;

            // 更新动画
            UpdateAnimation();

            // 蝙蝠群行为AI
            BatSwarmAI();

            // 更新朝向和旋转
            if (Math.Abs(Projectile.velocity.X) > 0.5f) {
                batDirection = Projectile.velocity.X > 0 ? 1 : -1;
            }

            // 根据速度方向计算旋转角度
            if (Projectile.velocity.LengthSquared() > 0.1f) {
                batRotation = Projectile.velocity.ToRotation();
            }

            // 模拟飞行的上下波动
            float wingWave = (float)Math.Sin(Main.GameUpdateCount * 0.2f + wingPhaseOffset) * 0.15f;
            Projectile.rotation = batRotation + wingWave;

            // 生成飞行粒子
            if (Main.rand.NextBool(40) && batAlpha > 0.5f) {
                Dust bat = Dust.NewDustPerfect(
                    Projectile.Center, 
                    DustID.Shadowflame,
                    Projectile.velocity * 0.2f, 
                    Scale: Main.rand.NextFloat(0.5f, 0.8f)
                );
                bat.noGravity = true;
                bat.alpha = 180;
            }
        }

        /// <summary>
        /// 蝙蝠群行为AI - 环绕玩家飞行
        /// </summary>
        private void BatSwarmAI() {
            // 计算鱼群算法
            CalculateFlockingBehavior();

            Vector2 totalForce = Vector2.Zero;

            // 1. 围绕玩家的环绕力
            Vector2 toPlayer = OwnerPlayer.Center - Projectile.Center;
            float distanceToPlayer = toPlayer.Length();

            // 目标距离：围绕玩家形成球形分布
            float targetDistance = 80f + (BatID % 10) * 12f; // 分层分布

            if (distanceToPlayer > targetDistance + 50f) {
                // 太远，强力拉回
                totalForce += toPlayer.SafeNormalize(Vector2.Zero) * 4f;
            }
            else if (distanceToPlayer < targetDistance - 30f) {
                // 太近，向外推
                totalForce -= toPlayer.SafeNormalize(Vector2.Zero) * 2f;
            }
            else {
                // 合适距离，环绕飞行
                // 计算切线方向（围绕玩家旋转）
                Vector2 tangent = new Vector2(-toPlayer.Y, toPlayer.X).SafeNormalize(Vector2.Zero);
                // 根据ID决定顺时针还是逆时针
                if (BatID % 2 == 0) tangent = -tangent;
                totalForce += tangent * 2.5f;
                
                // 轻微向中心吸引
                totalForce += toPlayer.SafeNormalize(Vector2.Zero) * 0.3f;
            }

            // 2. 鱼群行为力
            totalForce += separationForce * 2.5f * behaviorRandomness;
            totalForce += alignmentForce * 1.2f;
            totalForce += cohesionForce * 0.8f;

            // 3. 随机游动（增加自然感）
            wanderTimer++;
            if (wanderTimer > Main.rand.Next(20, 40)) {
                wanderTimer = 0;
                randomWander = new Vector2(
                    Main.rand.NextFloat(-1.2f, 1.2f),
                    Main.rand.NextFloat(-1.2f, 1.2f)
                ) * behaviorRandomness;
            }
            totalForce += randomWander * 0.5f;

            // 4. 跟随玩家移动方向
            if (OwnerPlayer.velocity.LengthSquared() > 1f) {
                Vector2 playerMoveDir = OwnerPlayer.velocity.SafeNormalize(Vector2.Zero);
                totalForce += playerMoveDir * 1.5f;
            }

            // 5. 上下波动（模拟翅膀扇动）
            float verticalWave = (float)Math.Sin(Main.GameUpdateCount * 0.15f + wingPhaseOffset) * 0.6f;
            totalForce.Y += verticalWave;

            // 应用力并限制速度
            Projectile.velocity += totalForce * 0.18f;

            // 部分跟随玩家速度
            Projectile.position += OwnerPlayer.velocity * 0.25f;

            float maxSpeed = 14f * behaviorRandomness;
            float minSpeed = 4f;

            float currentSpeed = Projectile.velocity.Length();
            if (currentSpeed > maxSpeed) {
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * maxSpeed;
            }
            else if (currentSpeed < minSpeed && currentSpeed > 0.1f) {
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * minSpeed;
            }
        }

        /// <summary>
        /// 计算鱼群算法
        /// </summary>
        private void CalculateFlockingBehavior() {
            separationForce = Vector2.Zero;
            alignmentForce = Vector2.Zero;
            cohesionForce = Vector2.Zero;

            Vector2 centerOfMass = OwnerPlayer.Center;
            Vector2 averageVelocity = Vector2.Zero;
            int nearbyBatCount = 0;

            for (int i = 0; i < Main.maxProjectiles; i++) {
                if (i == Projectile.whoAmI) continue;

                Projectile other = Main.projectile[i];

                if (other.active && other.type == Projectile.type) {
                    float distance = Vector2.Distance(Projectile.Center, other.Center);

                    // 分离力
                    if (distance < 45f) {
                        Vector2 toOther = (Projectile.Center - other.Center).SafeNormalize(Vector2.Zero);
                        separationForce += toOther / distance;
                    }

                    // 累加质心和速度
                    centerOfMass += other.Center;
                    averageVelocity += other.velocity;
                    nearbyBatCount++;
                }
            }

            if (nearbyBatCount > 0) {
                centerOfMass /= nearbyBatCount;
                averageVelocity /= nearbyBatCount;

                // 对齐力
                Vector2 alignedVelocity = averageVelocity.SafeNormalize(Vector2.Zero) * Projectile.velocity.Length();
                alignmentForce = (alignedVelocity - Projectile.velocity) * 0.12f;

                // 聚合力
                Vector2 toCenter = (centerOfMass - Projectile.Center).SafeNormalize(Vector2.Zero);
                cohesionForce = toCenter * 0.06f;
            }
        }

        /// <summary>
        /// 更新动画帧
        /// </summary>
        private void UpdateAnimation() {
            frameCounter++;
            if (frameCounter >= FrameSpeed) {
                frameCounter = 0;
                currentFrame++;
                if (currentFrame >= 4) {
                    currentFrame = 0;
                }
            }
            Projectile.frame = currentFrame;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D batTex = TextureAssets.Npc[NPCID.CaveBat].Value;

            // 计算帧数据
            int frameHeight = batTex.Height / Main.npcFrameCount[NPCID.CaveBat];
            Rectangle sourceRect = new Rectangle(0, frameHeight * currentFrame, batTex.Width, frameHeight);
            Vector2 origin = sourceRect.Size() / 2f;
            Vector2 drawPosition = Projectile.Center - Main.screenPosition;

            // 根据朝向决定翻转（纹理朝左，所以需要翻转处理）
            SpriteEffects effects = batDirection > 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            // 绘制拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;

                float trailAlpha = batAlpha * (1f - i / (float)Projectile.oldPos.Length) * 0.5f;
                Vector2 trailPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                float trailScale = batScale * (1f - i / (float)Projectile.oldPos.Length * 0.25f);

                Main.EntitySpriteDraw(
                    batTex,
                    trailPos,
                    sourceRect,
                    new Color(100, 100, 120) * trailAlpha,
                    Projectile.rotation,
                    origin,
                    trailScale * 0.9f,
                    effects,
                    0
                );
            }

            // 绘制主体
            Color drawColor = Color.Lerp(lightColor, new Color(180, 180, 200), 0.3f);
            
            Main.EntitySpriteDraw(
                batTex,
                drawPosition,
                sourceRect,
                drawColor * batAlpha,
                Projectile.rotation,
                origin,
                batScale,
                effects,
                0
            );

            // 速度快时的发光效果
            if (Projectile.velocity.Length() > 10f) {
                float glowAlpha = (Projectile.velocity.Length() - 10f) / 8f * 0.4f * batAlpha;
                
                Main.EntitySpriteDraw(
                    batTex,
                    drawPosition,
                    sourceRect,
                    new Color(200, 200, 220) * glowAlpha,
                    Projectile.rotation,
                    origin,
                    batScale * 1.15f,
                    effects,
                    0
                );
            }

            return false;
        }
    }
}