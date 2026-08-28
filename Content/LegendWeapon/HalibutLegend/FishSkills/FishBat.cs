using InnoVault.PRT;
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
        public override int ResearchDuration => 60 * 12;
        /// <summary>蝙蝠群技能最大持续时间</summary>
        public const int BatSwarmDuration = 1280;

        public override bool? CanUseItem(Item item, Player player) {
            HalibutPlayer halibutPlayer = player.GetOverride<HalibutPlayer>();

            if (player.altFunctionUse == 2) {
                //右键，激活蝙蝠化形
                if (!halibutPlayer.BatSwarmActive && Cooldown <= 0) {
                    item.UseSound = null;
                    Use(item, player);
                }
                return false;
            }
            else {
                //左键，消散蝙蝠群
                if (halibutPlayer.BatSwarmActive) {
                    item.UseSound = null;
                    DismissBatSwarm(player, halibutPlayer);
                    return false;
                }
            }

            return true;
        }

        public override bool? AltFunctionUse(Item item, Player player) {
            return true;
        }

        public override bool UpdateCooldown(HalibutPlayer halibutPlayer, Player player) {
            //更新技能状态
            if (halibutPlayer.BatSwarmActive) {
                halibutPlayer.BatSwarmTimer++;
                if (halibutPlayer.BatSwarmTimer >= BatSwarmDuration) {
                    //技能结束
                    DismissBatSwarm(player, halibutPlayer);
                }
            }

            //玩家更新阶段 ownedProjectileCounts 是上一拍完整快照，O(1) 代替全表扫描
            return player.ownedProjectileCounts[ModContent.ProjectileType<BatSwarmController>()] == 0;
        }

        public override void Use(Item item, Player player) {
            HalibutPlayer halibutPlayer = player.GetOverride<HalibutPlayer>();

            //检查技能是否在冷却中
            if (Cooldown > 0 || halibutPlayer.BatSwarmActive) {
                return;
            }

            SetCooldown();

            //激活技能
            halibutPlayer.BatSwarmActive = true;
            halibutPlayer.BatSwarmTimer = 0;

            //生成控制器弹幕（玩家飞行与技能时长）
            int controller = Projectile.NewProjectile(
                player.GetSource_ItemUse(item),
                player.Center,
                Vector2.Zero,
                ModContent.ProjectileType<BatSwarmController>(),
                0,
                0f,
                player.whoAmI
            );

            //生成蝙蝠群（30-50只蝙蝠）
            int batCount = Main.rand.Next(30 + 2 * HalibutData.GetDomainLayer(), 50 + 3 * HalibutData.GetDomainLayer());

            for (int i = 0; i < batCount; i++) {
                //自躯体内爆散而非环上凭空出现
                Vector2 spawnPos = player.Center + new Vector2(
                    Main.rand.NextFloat(-14f, 14f),
                    Main.rand.NextFloat(-26f, 24f)
                );
                Vector2 burstDir = (spawnPos - player.Center).SafeNormalize(Main.rand.NextVector2Unit());
                Vector2 initialVelocity = burstDir * Main.rand.NextFloat(7f, 13f) + Main.rand.NextVector2Circular(2f, 2f);

                int proj = Projectile.NewProjectile(
                    player.GetSource_ItemUse(item),
                    spawnPos,
                    initialVelocity,
                    ModContent.ProjectileType<BatSwarmMinion>(),
                    0,
                    0f,
                    player.whoAmI,
                    ai0: i //个体索引
                );

                if (Main.projectile[proj].ModProjectile is BatSwarmMinion bat) {
                    bat.OwnerPlayer = player;
                }
            }

            //化形剪影撕散
            if (Main.myPlayer == player.whoAmI) {
                Projectile.NewProjectile(
                    player.GetSource_ItemUse(item),
                    player.Center,
                    Vector2.Zero,
                    ModContent.ProjectileType<FishBatMorphProj>(),
                    0,
                    0f,
                    player.whoAmI,
                    ai0: 0f
                );
            }

            //播放音效
            SoundEngine.PlaySound(SoundID.NPCHit4 with {
                Volume = 0.7f,
                Pitch = -0.3f
            }, player.Center);
            SoundEngine.PlaySound(SoundID.Zombie20 with { //蝙蝠声音
                Volume = 0.6f,
                Pitch = 0.5f
            }, player.Center);

            //化形特效
            SpawnTransformEffect(player.Center);
        }

        /// <summary>消散蝙蝠群</summary>
        private static void DismissBatSwarm(Player player, HalibutPlayer halibutPlayer) {
            halibutPlayer.BatSwarmActive = false;
            halibutPlayer.BatSwarmTimer = 0;

            //蝙蝠转入收拢俯冲
            for (int i = 0; i < Main.maxProjectiles; i++) {
                if (Main.projectile[i].active &&
                    Main.projectile[i].type == ModContent.ProjectileType<BatSwarmMinion>() &&
                    Main.projectile[i].owner == player.whoAmI &&
                    Main.projectile[i].ModProjectile is BatSwarmMinion bat) {

                    bat.StartRegather();
                }
            }

            //杀死控制器弹幕，操控权即刻交还
            for (int i = 0; i < Main.maxProjectiles; i++) {
                if (Main.projectile[i].active &&
                    Main.projectile[i].type == ModContent.ProjectileType<BatSwarmController>() &&
                    Main.projectile[i].owner == player.whoAmI) {

                    Main.projectile[i].Kill();
                }
            }

            //收拢重组剪影
            if (Main.myPlayer == player.whoAmI) {
                Projectile.NewProjectile(
                    player.GetSource_Misc("FishBatMorph"),
                    player.Center,
                    Vector2.Zero,
                    ModContent.ProjectileType<FishBatMorphProj>(),
                    0,
                    0f,
                    player.whoAmI,
                    ai0: 1f
                );
            }

            //消散音效，低响+收拢振翅
            SoundEngine.PlaySound(SoundID.NPCDeath4 with {
                Volume = 0.6f,
                Pitch = 0.2f
            }, player.Center);
            SoundEngine.PlaySound(SoundID.Zombie20 with {
                Volume = 0.35f,
                Pitch = 0.2f
            }, player.Center);

            //消散特效
            SpawnDismissEffect(player.Center);
        }

        private static void SpawnTransformEffect(Vector2 position) {
            if (VaultUtils.isServer) {
                return;
            }
            //声呐主环与回声环由化形弹幕统一发射，各客户端节拍一致且不重复叠环

            //炭黑暗烟自躯体涌出压底
            for (int i = 0; i < 7; i++) {
                Vector2 puffPos = position + new Vector2(Main.rand.NextFloat(-12f, 12f), Main.rand.NextFloat(-24f, 22f));
                Vector2 puffVel = (puffPos - position).SafeNormalize(Vector2.UnitY) * Main.rand.NextFloat(1.2f, 2.8f) + new Vector2(0f, -0.4f);
                PRTLoader.NewParticle<PRT_FishBatSmoke>(puffPos, puffVel, FishBatMorphProj.SmokeDark, Main.rand.NextFloat(0.16f, 0.26f))
                    .Configure(Main.rand.Next(24, 34), 0.5f);
            }

            //皮翼暗影拍散
            for (int i = 0; i < 5; i++) {
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 vel = ang.ToRotationVector2() * Main.rand.NextFloat(3f, 6f);
                var wing = PRTLoader.NewParticle<PRT_FishBatCrescent>(position + ang.ToRotationVector2() * 14f, vel
                    , FishBatMorphProj.WingViolet, Main.rand.NextFloat(0.34f, 0.5f));
                wing.Rotation = ang + MathHelper.PiOver2;
                wing.Configure(Main.rand.Next(12, 18), Main.rand.NextFloat(-0.08f, 0.08f));
            }

            //暗影法尘作底噪填充
            for (int i = 0; i < 14; i++) {
                float angle = MathHelper.TwoPi * i / 14f + Main.rand.NextFloat(-0.2f, 0.2f);
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(3f, 7f);

                Dust dark = Dust.NewDustPerfect(
                    position,
                    DustID.Shadowflame,
                    velocity,
                    100,
                    default,
                    Main.rand.NextFloat(1.2f, 2f)
                );
                dark.noGravity = true;
            }
        }

        private static void SpawnDismissEffect(Vector2 position) {
            if (VaultUtils.isServer) {
                return;
            }
            //塌缩声呐环由重组弹幕统一发射，此处只铺底噪

            //少量暗影法尘底噪，向心暗烟由重组弹幕逐帧补足
            for (int i = 0; i < 10; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(4f, 4f);

                Dust dark = Dust.NewDustPerfect(
                    position,
                    DustID.Shadowflame,
                    velocity,
                    100,
                    default,
                    Main.rand.NextFloat(1f, 1.6f)
                );
                dark.noGravity = true;
                dark.fadeIn = 1.2f;
            }
        }
    }

    /// <summary>蝙蝠群控制器，玩家飞行与技能时长</summary>
    internal class BatSwarmController : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private Player Owner => Main.player[Projectile.owner];

        //声呐脉冲节拍计时
        private int sonarTimer;

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = FishBat.BatSwarmDuration;
            Projectile.alpha = 255; //完全透明
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            return false;
        }

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }

            HalibutPlayer halibutPlayer = Owner.GetOverride<HalibutPlayer>();

            //检查技能是否结束
            if (!halibutPlayer.BatSwarmActive) {
                Projectile.Kill();
                return;
            }

            //玩家飞行控制
            Owner.noFallDmg = true;
            Owner.gravity = 0f;
            Owner.maxFallSpeed = 100f;

            Owner.wingTime = 0;

            halibutPlayer.HidePlayerTime = 2;

            //计算目标速度（朝向光标，允许全方向飞行）
            Vector2 toMouse = (Main.MouseWorld - Owner.Center).SafeNormalize(Vector2.Zero);
            float flySpeed = 20f + HalibutData.GetDomainLayer() * 1.5f; //基础飞行速度

            //允许玩家通过移动键微调方向
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

            //平滑插值
            Projectile.localAI[0]++;
            float lerpSpeed = 0.22f;
            if (Projectile.localAI[0] < 8f) {
                lerpSpeed = 0.34f;
                targetVelocity *= 1.2f;
            }
            Projectile.velocity = Owner.velocity = Vector2.Lerp(Owner.velocity, targetVelocity, lerpSpeed);
            Owner.direction = Math.Sign(Owner.velocity.X);
            Owner.Center = Projectile.Center;

            SpawnFlightDress();
        }

        /// <summary>飞行装饰</summary>
        private void SpawnFlightDress() {
            if (VaultUtils.isServer) {
                return;
            }
            if (Main.rand.NextBool(5)) {
                Vector2 puffPos = Owner.Center + Main.rand.NextVector2Circular(22f, 22f);
                PRTLoader.NewParticle<PRT_FishBatSmoke>(puffPos, -Owner.velocity * 0.16f
                    , FishBatMorphProj.SmokeDark, Main.rand.NextFloat(0.13f, 0.2f))
                    .Configure(Main.rand.Next(18, 26), 0.4f);
            }

            if (Main.rand.NextBool(9)) {
                Vector2 dustPos = Owner.Center + Main.rand.NextVector2Circular(20f, 20f);
                Dust flight = Dust.NewDustPerfect(
                    dustPos,
                    DustID.Shadowflame,
                    -Owner.velocity * 0.3f,
                    Scale: Main.rand.NextFloat(0.8f, 1.2f)
                );
                flight.noGravity = true;
                flight.alpha = 150;
            }

            //回声定位节拍
            if (++sonarTimer >= 44) {
                sonarTimer = 0;
                PRTLoader.NewParticle<PRT_FishBatSonar>(Owner.Center + Owner.velocity * 1.2f
                    , Owner.velocity * 0.16f, new Color(170, 148, 226), 1f)
                    .Configure(0.18f, 1.35f, 20);
            }
        }

        public override void OnKill(int timeLeft) {
            //技能结束时恢复玩家重力
            if (Owner != null && Owner.active) {
                Owner.gravity = Player.defaultGravity;
            }
        }
    }

    /// <summary>蝙蝠群个体，环绕玩家的 boids</summary>
    internal class BatSwarmMinion : ModProjectile
    {
        public override string Texture => "Terraria/Images/NPC_" + NPCID.CaveBat;

        /// <summary>拥有者玩家</summary>
        public Player OwnerPlayer { get; set; }

        /// <summary>蝙蝠的个体ID</summary>
        private int BatID => (int)Projectile.ai[0];

        //群体算法力量
        private Vector2 separationForce = Vector2.Zero;
        private Vector2 alignmentForce = Vector2.Zero;
        private Vector2 cohesionForce = Vector2.Zero;
        private Vector2 randomWander = Vector2.Zero;

        //个体参数
        private int wanderTimer = 0;
        private float batScale = 1f;
        private float batAlpha = 0f;
        private int batDirection = 1;
        private float batRotation = 0f;
        private float behaviorRandomness = 1f;
        private float wingPhaseOffset = 0f;
        private int lifeTimer = 0;

        //收拢俯冲状态
        private bool Regather => Projectile.ai[1] == 1f;
        private int regatherTimer = 0;
        //扑翼拍向，逐拍交替
        private int flapDir = 1;

        //动画参数
        private int currentFrame = 0;
        private int frameCounter = 0;
        private const int FrameSpeed = 6;

        public override void SetStaticDefaults() {
            Main.projFrames[Projectile.type] = 4; //4帧动画
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

        /// <summary>转入收拢俯冲，扑回玩家折翼没入，状态经 ai[1] 同步远端</summary>
        public void StartRegather() {
            if (Regather) {
                return;
            }
            Projectile.ai[1] = 1f;
            regatherTimer = 0;
            Projectile.timeLeft = Math.Max(Projectile.timeLeft, 60);
            Projectile.netUpdate = true;
        }

        public override void AI() {
            //找到拥有者
            OwnerPlayer ??= Main.player[Projectile.owner];
            if (OwnerPlayer == null || !OwnerPlayer.active) {
                Projectile.Kill();
                return;
            }

            HalibutPlayer halibutPlayer = OwnerPlayer.GetOverride<HalibutPlayer>();

            //初始化参数
            if (lifeTimer == 0) {
                batScale = Main.rand.NextFloat(0.7f, 1.2f);
                behaviorRandomness = Main.rand.NextFloat(0.9f, 1.2f);
                wingPhaseOffset = Main.rand.NextFloat(0f, MathHelper.TwoPi);
                //扑翼动画错帧起步
                frameCounter = Main.rand.Next(FrameSpeed);
                currentFrame = Main.rand.Next(4);
            }

            lifeTimer++;

            //一切结束路径统一走收拢，禁瞬灭；BatSwarmActive 只在拥有者端可信
            if (!Regather && Projectile.owner == Main.myPlayer && !halibutPlayer.BatSwarmActive) {
                StartRegather();
            }

            if (Regather) {
                RegatherAI();
                if (!Projectile.active) {
                    return;
                }
            }
            else {
                if (batAlpha < 1f) {
                    batAlpha += 0.2f;
                    if (batAlpha > 1f) batAlpha = 1f;
                }

                Projectile.position += OwnerPlayer.velocity * 0.75f;

                //蝙蝠群行为AI
                BatSwarmAI();
            }

            //更新动画
            UpdateAnimation();

            //更新朝向和旋转
            if (Math.Abs(Projectile.velocity.X) > 0.5f) {
                batDirection = Projectile.velocity.X > 0 ? 1 : -1;
            }

            //根据速度方向计算旋转角度
            if (Projectile.velocity.LengthSquared() > 0.1f) {
                batRotation = Projectile.velocity.ToRotation();
            }

            //上下波动
            float wingWave = (float)Math.Sin(Main.GameUpdateCount * 0.2f + wingPhaseOffset) * 0.15f;
            Projectile.rotation = batRotation + wingWave;

            //生成飞行粒子
            if (Main.rand.NextBool(50) && batAlpha > 0.5f) {
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

        /// <summary>收拢俯冲AI，越到后段咬合越急，贴近后渐隐折翼没入躯体</summary>
        private void RegatherAI() {
            regatherTimer++;

            Vector2 toOwner = OwnerPlayer.Center - Projectile.Center;
            float dist = toOwner.Length();

            float chase = MathHelper.Clamp(9f + regatherTimer * 1.7f, 9f, 30f);
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, toOwner.SafeNormalize(Vector2.Zero) * chase, 0.24f);

            //临身折翼
            if (dist < 26f || regatherTimer > 30) {
                if (!VaultUtils.isServer && batAlpha > 0.3f && Main.rand.NextBool(3)) {
                    PRTLoader.NewParticle<PRT_FishBatSmoke>(Projectile.Center, toOwner.SafeNormalize(Vector2.Zero) * 1.5f
                        , FishBatMorphProj.SmokeDark, Main.rand.NextFloat(0.12f, 0.18f))
                        .Configure(Main.rand.Next(14, 20), 0.45f);
                }
                Projectile.Kill();
                return;
            }

            //贴近渐隐只降不升
            if (dist < 70f) {
                batAlpha = Math.Min(batAlpha, MathHelper.Clamp(dist / 70f, 0.2f, 1f));
            }
        }

        /// <summary>环绕玩家的 boids AI</summary>
        private void BatSwarmAI() {
            //出场爆散段
            if (lifeTimer < 12) {
                Projectile.velocity *= 0.93f;
            }

            //计算鱼群算法
            CalculateFlockingBehavior();

            Vector2 totalForce = Vector2.Zero;

            //1. 围绕玩家的环绕力
            Vector2 toPlayer = OwnerPlayer.Center - Projectile.Center;
            float distanceToPlayer = toPlayer.Length();

            //目标距离，围绕玩家形成球形分布
            float targetDistance = 80f + (BatID % 10) * 12f; //分层分布

            //murmuration 呼吸
            float breath = (float)Math.Sin(Main.GameUpdateCount * 0.026f);
            targetDistance *= 1f + breath * (0.16f + (BatID % 10) * 0.014f);

            if (distanceToPlayer > targetDistance + 50f) {
                //太远，强力拉回
                totalForce += toPlayer.SafeNormalize(Vector2.Zero) * 4f;
            }
            else if (distanceToPlayer < targetDistance - 30f) {
                //太近，向外推
                totalForce -= toPlayer.SafeNormalize(Vector2.Zero) * 2f;
            }
            else {
                //合适距离，环绕飞行
                //计算切线方向（围绕玩家旋转）
                Vector2 tangent = new Vector2(-toPlayer.Y, toPlayer.X).SafeNormalize(Vector2.Zero);
                //根据ID决定顺时针还是逆时针
                if (BatID % 2 == 0) tangent = -tangent;
                totalForce += tangent * 2.5f;

                //轻微向中心吸引
                totalForce += toPlayer.SafeNormalize(Vector2.Zero) * 0.3f;
            }

            //2. 鱼群行为力
            totalForce += separationForce * 2.5f * behaviorRandomness;
            totalForce += alignmentForce * 1.2f;
            totalForce += cohesionForce * 0.8f;

            //3. 随机扰动
            wanderTimer++;
            if (wanderTimer > Main.rand.Next(20, 40)) {
                wanderTimer = 0;
                randomWander = new Vector2(
                    Main.rand.NextFloat(-1.2f, 1.2f),
                    Main.rand.NextFloat(-1.2f, 1.2f)
                ) * behaviorRandomness;
            }
            totalForce += randomWander * 0.5f;

            //4. 跟随玩家移动方向
            if (OwnerPlayer.velocity.LengthSquared() > 1f) {
                Vector2 playerMoveDir = OwnerPlayer.velocity.SafeNormalize(Vector2.Zero);
                totalForce += playerMoveDir * 1.5f;
            }

            //5. 垂直正弦波动
            float verticalWave = (float)Math.Sin(Main.GameUpdateCount * 0.15f + wingPhaseOffset) * 0.6f;
            totalForce.Y += verticalWave;

            //应用力并限制速度
            Projectile.velocity += totalForce * 0.18f;

            //部分跟随玩家速度
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

        /// <summary>邻近蝙蝠分离/对齐/聚合</summary>
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

                    //分离力
                    if (distance < 45f) {
                        Vector2 toOther = (Projectile.Center - other.Center).SafeNormalize(Vector2.Zero);
                        separationForce += toOther / distance;
                    }

                    //累加质心和速度
                    centerOfMass += other.Center;
                    averageVelocity += other.velocity;
                    nearbyBatCount++;
                }
            }

            if (nearbyBatCount > 0) {
                centerOfMass /= nearbyBatCount;
                averageVelocity /= nearbyBatCount;

                //对齐力
                Vector2 alignedVelocity = averageVelocity.SafeNormalize(Vector2.Zero) * Projectile.velocity.Length();
                alignmentForce = (alignedVelocity - Projectile.velocity) * 0.12f;

                //聚合力
                Vector2 toCenter = (centerOfMass - Projectile.Center).SafeNormalize(Vector2.Zero);
                cohesionForce = toCenter * 0.06f;
            }
        }

        /// <summary>更新动画帧，下拍瞬间甩出新月翼影</summary>
        private void UpdateAnimation() {
            frameCounter++;
            int speed = Regather ? 3 : FrameSpeed;//收拢时扑翼变急
            if (frameCounter >= speed) {
                frameCounter = 0;
                currentFrame++;
                if (currentFrame >= 4) {
                    currentFrame = 0;
                }
                flapDir = -flapDir;
                if (currentFrame == 2 && batAlpha > 0.6f && Main.rand.NextBool(3)) {
                    ShedWingCrescent();
                }
            }
            Projectile.frame = currentFrame;
        }

        /// <summary>新月扑翼残影</summary>
        private void ShedWingCrescent() {
            if (VaultUtils.isServer) {
                return;
            }
            Vector2 perp = Projectile.velocity.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2 * flapDir);
            var wing = PRTLoader.NewParticle<PRT_FishBatCrescent>(
                Projectile.Center - Projectile.velocity * 0.5f,
                Projectile.velocity * 0.18f + perp * Main.rand.NextFloat(0.8f, 1.6f),
                FishBatMorphProj.WingViolet * batAlpha,
                batScale * Main.rand.NextFloat(0.3f, 0.42f));
            wing.Rotation = Projectile.rotation + MathHelper.PiOver2 * flapDir;
            wing.Configure(Main.rand.Next(10, 15), flapDir * Main.rand.NextFloat(0.02f, 0.05f));
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D batTex = TextureAssets.Npc[NPCID.CaveBat].Value;

            //计算帧数据
            int frameHeight = batTex.Height / Main.npcFrameCount[NPCID.CaveBat];
            Rectangle sourceRect = new Rectangle(0, frameHeight * currentFrame, batTex.Width, frameHeight);
            Vector2 origin = sourceRect.Size() / 2f;
            Vector2 drawPosition = Projectile.Center - Main.screenPosition;

            //纹理朝左，按朝向翻转
            SpriteEffects effects = batDirection > 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            //速度拉伸
            float speed = Projectile.velocity.Length();
            float stretch = 1f + MathHelper.Clamp((speed - 6f) * 0.028f, 0f, 0.4f);
            Vector2 bodyScale = new Vector2(batScale * stretch, batScale * (1f - (stretch - 1f) * 0.45f));

            //暗紫压色
            float lightT = (lightColor.R + lightColor.G + lightColor.B) / 765f;
            Color bodyColor = Color.Lerp(new Color(48, 40, 78), new Color(126, 110, 172), lightT);

            //紧凑残影链
            for (int i = 1; i <= 5; i += 2) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;

                float trailAlpha = batAlpha * (0.4f - i * 0.055f);
                Vector2 trailPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;

                Main.EntitySpriteDraw(
                    batTex,
                    trailPos,
                    sourceRect,
                    new Color(30, 22, 46) * trailAlpha,
                    Projectile.rotation,
                    origin,
                    bodyScale * (1f - i * 0.04f),
                    effects,
                    0
                );
            }

            //叠底衬影
            Main.EntitySpriteDraw(
                batTex,
                drawPosition + new Vector2(2f, 3f),
                sourceRect,
                new Color(8, 6, 14) * (0.5f * batAlpha),
                Projectile.rotation,
                origin,
                bodyScale,
                effects,
                0
            );

            //绘制主体
            Main.EntitySpriteDraw(
                batTex,
                drawPosition,
                sourceRect,
                bodyColor * batAlpha,
                Projectile.rotation,
                origin,
                bodyScale,
                effects,
                0
            );

            return false;
        }
    }
}
