using CalamityOverhaul.Common;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    internal class FishSwarm : FishSkill
    {
        public override int UnlockFishID => ItemID.Bass;
        /// <summary>
        /// 技能最大持续时间（5秒 = 300帧）
        /// </summary>
        public static int FishSwarmDuration => 300 + HalibutData.GetDomainLayer() * 30;
        public override int DefaultCooldown => 60 * (30 - HalibutData.GetDomainLayer() * 2);
        public override int ResearchDuration => 60 * 14;
        public override bool? CanUseItem(Item item, Player player) {
            HalibutPlayer halibutPlayer = player.GetOverride<HalibutPlayer>();

            if (player.altFunctionUse == 2) {
                item.UseSound = null;
                Use(item, player);
                return false;
            }
            else if (halibutPlayer.FishConeSurgeActive) {
                item.UseSound = null;
                ActivateFishConeSurge(player, halibutPlayer);
                halibutPlayer.FishConeSurgeActive = false;
            }

            return halibutPlayer.HidePlayerTime <= 0;
        }
        public override bool? AltFunctionUse(Item item, Player player) {
            return true;
        }
        public override bool UpdateCooldown(HalibutPlayer halibutPlayer, Player player) {
            //更新技能状态
            if (halibutPlayer.FishSwarmActive) {
                halibutPlayer.FishSwarmTimer++;

                if (halibutPlayer.FishSwarmTimer >= FishSwarmDuration) {
                    //技能结束
                    halibutPlayer.FishSwarmActive = false;
                    halibutPlayer.FishSwarmTimer = 0;
                    SetCooldown();
                }
            }

            return player.CountProjectilesOfID<FishSwarmController>() == 0;
        }
        public override void Use(Item item, Player player) {
            HalibutPlayer halibutPlayer = player.GetOverride<HalibutPlayer>();
            //检查技能是否在冷却中
            if (Cooldown > 0 || halibutPlayer.FishSwarmActive) {
                return;
            }

            SetCooldown();
            //激活技能
            halibutPlayer.FishConeSurgeActive = true;
            halibutPlayer.FishSwarmActive = true;
            halibutPlayer.FishSwarmTimer = 0;

            //计算冲刺方向（朝向光标）
            Vector2 dashDirection = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.Zero);

            //生成控制器（玩家移动与技能时长）
            int controller = Projectile.NewProjectile(
                player.GetSource_ItemUse(item),
                player.Center,
                dashDirection, //存储初始冲刺方向
                ModContent.ProjectileType<FishSwarmController>(),
                0,
                0f,
                player.whoAmI
            );

            //鱼群规模：基础 40-55 条，随领域层数增长
            int fishCount = Main.rand.Next(40 + 5 * HalibutData.GetDomainLayer(), 55 + 5 * HalibutData.GetDomainLayer());
            for (int i = 0; i < fishCount; i++) {
                //在玩家周围随机位置生成鱼
                float angle = MathHelper.TwoPi * i / fishCount + Main.rand.NextFloat(-0.5f, 0.5f);
                float distance = Main.rand.NextFloat(30f, 120f);
                Vector2 spawnOffset = new Vector2(
                    (float)Math.Cos(angle) * distance,
                    (float)Math.Sin(angle) * distance
                );

                Vector2 spawnPos = player.Center + spawnOffset;
                Vector2 initialVelocity = dashDirection * Main.rand.NextFloat(10f, 20f);

                int proj = Projectile.NewProjectile(
                    player.GetSource_ItemUse(item),
                    spawnPos,
                    initialVelocity,
                    ModContent.ProjectileType<FishingFly>(),
                    0,
                    0f,
                    player.whoAmI,
                    ai0: i //个体索引
                );

                if (Main.projectile[proj].ModProjectile is FishingFly fish) {
                    fish.OwnerPlayer = player;
                }
            }

            //播放音效
            SoundEngine.PlaySound(SoundID.Splash, player.Center);
            SoundEngine.PlaySound(CWRSound.Dash, player.Center); //冲刺音效
        }

        /// <summary>
        /// 激活螺旋尖锥突袭
        /// </summary>
        public static void ActivateFishConeSurge(Player player, HalibutPlayer halibutPlayer) {
            //计算突袭方向（朝向光标）
            Vector2 surgeDirection = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.Zero);

            //通知所有鱼弹幕进入突袭模式
            for (int i = 0; i < Main.maxProjectiles; i++) {
                if (Main.projectile[i].active &&
                    Main.projectile[i].type == ModContent.ProjectileType<FishingFly>() &&
                    Main.projectile[i].owner == player.whoAmI) {

                    if (Main.projectile[i].ModProjectile is FishingFly fish && fish.OwnerPlayer == player) {
                        //触发鱼的突袭模式
                        fish.ActivateSurgeMode(surgeDirection);
                    }
                }
            }

            //杀死控制器弹幕（恢复玩家控制）
            for (int i = 0; i < Main.maxProjectiles; i++) {
                if (Main.projectile[i].active &&
                    Main.projectile[i].type == ModContent.ProjectileType<FishSwarmController>() &&
                    Main.projectile[i].owner == player.whoAmI) {
                    Main.projectile[i].timeLeft = 30;
                    if (Main.projectile[i].ModProjectile is FishSwarmController ctrl) {
                        //视觉：登记释放拍与流线束退场
                        ctrl.SurgeCued = true;
                        ctrl.SurgeDir = surgeDirection;
                    }
                }
            }

            //聚拢预告：收缩环 + 向心碎光
            FishSwarmVFX.GatherCue(player);

            //播放突袭音效
            SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown, player.Center); //俯冲音效

            //玩家重新显现
            player.gravity = Player.defaultGravity;
        }
    }

    /// <summary>鱼群控制器，玩家移动与技能时长；兼群体流线束绘制载体</summary>
    internal class FishSwarmController : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private Player Owner => Main.player[Projectile.owner];

        /// <summary>
        /// 初始冲刺方向
        /// </summary>
        private Vector2 DashDirection => Projectile.velocity;

        /// <summary>
        /// 冲刺阶段计时器
        /// </summary>
        private ref float DashTimer => ref Projectile.ai[0];

        /// <summary>
        /// 冲刺持续时间（帧数）
        /// </summary>
        private const int DashDuration = 20;

        /// <summary>
        /// 冲刺速度
        /// </summary>
        private const float DashSpeed = 60f;

        /// <summary>
        /// 正常移动速度
        /// </summary>
        private const float NormalSpeed = 14f;

        /// <summary>突袭已触发（由 <see cref="FishSwarm.ActivateFishConeSurge"/> 置位），驱动释放拍与流线束退场</summary>
        internal bool SurgeCued;

        /// <summary>突袭方向，仅视觉用</summary>
        internal Vector2 SurgeDir;

        private Trail schoolTrail;
        private float ribbonEnv;
        private float wakeHeadWidth;
        private float wakeEnv;

        public override void SetStaticDefaults() {
            //玩家路径即鱼群路径，供流线束取样
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 26;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = FishSwarm.FishSwarmDuration;
            Projectile.alpha = 255; //完全透明
        }

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }

            HalibutPlayer halibutPlayer = Owner.GetOverride<HalibutPlayer>();

            //检查技能是否结束
            if (!halibutPlayer.FishSwarmActive) {
                Projectile.Kill();
                return;
            }

            halibutPlayer.HidePlayerTime = 2;

            //弹幕位置跟随玩家
            Projectile.Center = Owner.Center;

            Owner.noFallDmg = true;//别冲一下给自己摔死了

            DashTimer++;

            //冲刺阶段
            if (DashTimer <= DashDuration) {
                //冲刺阶段：强力加速
                float dashProgress = DashTimer / DashDuration;

                //缓动加速加强冲击
                float speedMultiplier = 1f - (float)Math.Pow(dashProgress, 2); //平方衰减
                float currentDashSpeed = DashSpeed * speedMultiplier;

                //在冲刺阶段允许轻微调整方向
                Vector2 toMouse = (Main.MouseWorld - Owner.Center).SafeNormalize(Vector2.Zero);
                Vector2 adjustedDirection = Vector2.Lerp(DashDirection, toMouse, 0.05f).SafeNormalize(Vector2.Zero);

                Owner.velocity = adjustedDirection * (currentDashSpeed + NormalSpeed * dashProgress);

                //化形入场英雄时刻：剪影分批溶解成鳞片 + 破水环 + 定向震（仅首帧）
                if (DashTimer == 1) {
                    FishSwarmVFX.DissolveBurst(Owner, DashDirection);
                }

                //冲刺水尾底噪（克制填充，主体交给鳞片与流线束）
                if (Main.rand.NextBool(4)) {
                    Vector2 dustPos = Owner.Center + Main.rand.NextVector2Circular(46f, 46f);
                    Dust dust = Dust.NewDustPerfect(dustPos, DustID.Water,
                        -Owner.velocity * 0.25f, Scale: Main.rand.NextFloat(0.9f, 1.4f));
                    dust.noGravity = true;
                    dust.alpha = 120;
                }
            }
            //持续移动阶段
            else {
                //计算目标速度（朝向光标）
                Vector2 targetDirection = (Main.MouseWorld - Owner.Center).SafeNormalize(Vector2.Zero);
                Vector2 targetVelocity = targetDirection * NormalSpeed;

                //使用更快的插值速度，让移动更灵敏
                float lerpSpeed = 0.15f;

                //增加一些周期性的速度波动，模拟鱼群游动的节奏感
                float rhythmBoost = 1f + (float)Math.Sin(DashTimer * 0.15f) * 0.2f;
                targetVelocity *= rhythmBoost;

                Owner.velocity = Vector2.Lerp(Owner.velocity, targetVelocity, lerpSpeed);
                Owner.direction = Math.Sign(Owner.velocity.X);

                //持续移动的水花特效（频率较低）
                if (Main.rand.NextBool(6)) {
                    Vector2 dustPos = Owner.Center + Main.rand.NextVector2Circular(15f, 15f);
                    Dust dust = Dust.NewDustPerfect(dustPos, DustID.Water,
                        -Owner.velocity * 0.2f, Scale: Main.rand.NextFloat(0.8f, 1.2f));
                    dust.noGravity = true;
                    dust.alpha = 100;
                }

                //高速巡游的水纹涟漪（折射暗示，低频）
                if (DashTimer % 24 == 0 && Owner.velocity.Length() > 15f) {
                    FishSwarmVFX.TravelRipple(Owner.Center, Owner.velocity);
                }
            }

            //突袭释放拍：聚拢结束、锥形射出的那一帧（timeLeft 由突袭激活时设为 30，聚拢 15 帧）
            if (SurgeCued && Projectile.timeLeft == 15) {
                FishSwarmVFX.ReleaseCue(Owner, SurgeDir);
            }

            //流线束包络：入场渐起，突袭触发后退场
            ribbonEnv = MathHelper.Lerp(ribbonEnv, SurgeCued ? 0f : 1f, 0.1f);

            //防止玩家受到其他速度影响
            Owner.maxFallSpeed = 100f;
            Owner.gravity = 0f;
        }

        public override void OnKill(int timeLeft) {
            //技能结束时恢复玩家重力
            if (Owner != null && Owner.active) {
                Owner.gravity = Player.defaultGravity;
                //化形退场英雄时刻：鳞片收拢重织人形，掩住玩家显形
                FishSwarmVFX.ReformBurst(Owner);
            }
            Owner.GetOverride<HalibutPlayer>().FishSwarmActive = false;
            Owner.GetOverride<HalibutPlayer>().FishSwarmTimer = 0;
        }

        private float SchoolWakeWidth(float completionRatio) {
            float swell = Math.Min(completionRatio * 3.2f, 1f);     //离头端快速鼓起
            float taper = (float)Math.Pow(1f - completionRatio, 0.85f);
            return wakeHeadWidth * swell * taper;
        }

        private Color SchoolWakeColor(Vector2 coord) => Color.White * (wakeEnv * (1f - coord.X * 0.4f));

        void IPrimitiveDrawable.DrawPrimitives() {
            Effect fx = FishSwarmAssets.FishSwarmWake;
            if (fx == null || !Projectile.active || !Owner.active) {
                return;
            }
            float speed = Owner.velocity.Length();
            wakeEnv = ribbonEnv * MathHelper.Clamp((speed - 7f) / 14f, 0f, 1f);
            if (wakeEnv <= 0.04f) {
                return;
            }
            wakeHeadWidth = MathHelper.Clamp(speed * 2.4f, 22f, 64f);

            Vector2[] pos = new Vector2[Projectile.oldPos.Length];
            for (int i = 0; i < pos.Length; i++) {
                pos[i] = Projectile.oldPos[i] == Vector2.Zero
                    ? Projectile.Center : Projectile.oldPos[i] + Projectile.Size * 0.5f;
            }
            schoolTrail ??= new Trail(pos, SchoolWakeWidth, SchoolWakeColor);
            schoolTrail.TrailPositions = pos;

            FishSwarmVFX.ApplyWake(fx, Projectile.whoAmI * 0.37f);
            Main.graphics.GraphicsDevice.BlendState = BlendState.Additive;
            schoolTrail.DrawTrail(fx);
            Main.graphics.GraphicsDevice.BlendState = BlendState.AlphaBlend;
        }
    }

    internal class FishingFly : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;//透明贴图，因为纹理会手动获取

        /// <summary>
        /// 拥有者玩家
        /// </summary>
        public Player OwnerPlayer { get; set; }

        /// <summary>
        ///个体ID，行为分化
        /// </summary>
        private int FishID => (int)Projectile.ai[0];

        /// <summary>
        /// 鱼群算法 - 分离力量
        /// </summary>
        private Vector2 separationForce = Vector2.Zero;

        /// <summary>
        /// 鱼群算法 - 对齐力量
        /// </summary>
        private Vector2 alignmentForce = Vector2.Zero;

        /// <summary>
        /// 鱼群算法 - 聚合力量
        /// </summary>
        private Vector2 cohesionForce = Vector2.Zero;

        /// <summary>
        /// 个体随机游动偏移
        /// </summary>
        private Vector2 randomWander = Vector2.Zero;

        /// <summary>
        /// 随机游动计时器
        /// </summary>
        private int wanderTimer = 0;

        /// <summary>
        /// 鱼的缩放比例（模拟大小差异）
        /// </summary>
        private float fishScale = 1f;

        /// <summary>
        /// 鱼的透明度
        /// </summary>
        private float fishAlpha = 0f;

        /// <summary>
        /// 鱼的朝向
        /// </summary>
        private int fishDirection = 1;

        /// <summary>
        /// 鱼的旋转角度
        /// </summary>
        private float fishRotation = 0f;

        /// <summary>
        ///个体随机偏好
        /// </summary>
        private float behaviorRandomness = 1f;

        /// <summary>
        /// 跃动周期偏移
        /// </summary>
        private float jumpPhaseOffset = 0f;

        /// <summary>
        /// 转向敏捷度：外层鱼更迟钝，转弯时群体外缘甩尾滞后（惯性差异）
        /// </summary>
        private float turnAgility = 1f;

        /// <summary>
        /// 最近一次群体平均速度，鳞光传染波的流向兜底
        /// </summary>
        private Vector2 lastAvgVel = Vector2.Zero;

        //拖影取样帧：快速时更贴近本体，读作速度拉伸而非贴图串
        private static readonly int[] GhostIdxFast = [1, 3, 5];
        private static readonly int[] GhostIdxSlow = [2, 4, 6];

        /// <summary>
        ///生命计时，判冲刺阶段
        /// </summary>
        private int lifeTimer = 0;

        /// <summary>
        /// 冲刺阶段持续时间（与控制器同步）
        /// </summary>
        private const int DashPhase = 20;

        /// <summary>
        /// 突袭模式激活标志
        /// </summary>
        private bool surgeModeActive = false;

        /// <summary>
        /// 突袭方向
        /// </summary>
        private Vector2 surgeDirection = Vector2.Zero;

        /// <summary>
        /// 突袭阶段计时器
        /// </summary>
        private int surgeTimer = 0;

        /// <summary>
        /// 突袭聚拢阶段持续时间
        /// </summary>
        private const int SurgeGatherPhase = 15;

        /// <summary>
        /// 突袭冲刺阶段持续时间
        /// </summary>
        private const int SurgeDashPhase = 30;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8; //冲刺时需要更长的拖尾
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 3;
            Projectile.timeLeft = FishSwarm.FishSwarmDuration + 60; //额外帧淡出
        }

        /// <summary>
        /// 激活突袭模式（由HalibutOverride调用）
        /// </summary>
        public void ActivateSurgeMode(Vector2 direction) {
            ShootState shootState = OwnerPlayer.GetShootState();
            surgeModeActive = true;
            surgeDirection = direction;
            surgeTimer = 0;
            Projectile.friendly = true;
            Projectile.damage = (int)(shootState.WeaponDamage * (0.35 + HalibutData.GetDomainLayer() * 0.15f));
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            //找到拥有者
            if (OwnerPlayer == null || !OwnerPlayer.active) {
                Projectile.Kill();
                return;
            }

            HalibutPlayer halibutPlayer = OwnerPlayer.GetOverride<HalibutPlayer>();

            //突袭模式
            if (surgeModeActive) {
                SurgeModeAI();
                return;
            }

            //检查技能是否结束
            if (!halibutPlayer.FishSwarmActive) {
                //技能结束：向玩家收拢并入重织的剪影，禁原地淡出 pop
                Vector2 toHome = OwnerPlayer.Center - Projectile.Center;
                float homeDist = toHome.Length();
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, toHome.SafeNormalize(Vector2.Zero) * 24f, 0.16f);
                fishAlpha -= homeDist < 24f ? 0.22f : 0.05f;
                if (fishAlpha <= 0f) {
                    Projectile.Kill();
                    return;
                }
                if (Projectile.velocity.LengthSquared() > 0.1f) {
                    fishRotation = Projectile.velocity.ToRotation();
                    Projectile.rotation = fishRotation;
                }
                if (Math.Abs(Projectile.velocity.X) > 0.5f) {
                    fishDirection = Projectile.velocity.X > 0 ? 1 : -1;
                }
                lifeTimer++;
                return;
            }
            else {
                //淡入效果（冲刺阶段快速淡入）
                if (fishAlpha < 1f) {
                    float fadeSpeed = lifeTimer < DashPhase ? 0.25f : 0.15f; //冲刺阶段更快淡入
                    fishAlpha += fadeSpeed;
                    if (fishAlpha > 1f) fishAlpha = 1f;
                }
            }

            //初始化鱼的参数（只在第一帧）
            if (lifeTimer == 0) {
                fishScale = Main.rand.NextFloat(0.6f, 1.3f);
                behaviorRandomness = Main.rand.NextFloat(0.8f, 1.3f);
                jumpPhaseOffset = Main.rand.NextFloat(0f, MathHelper.TwoPi);
                //按所在螺旋层分配惯性：外层转向滞后
                int layer = Math.Min(FishID / 28, 4);
                turnAgility = MathHelper.Lerp(1.1f, 0.78f, layer / 4f);
            }

            lifeTimer++;

            //判断是否在冲刺阶段
            bool isDashing = lifeTimer <= DashPhase;

            //冲刺阶段特殊行为
            if (isDashing) {
                DashPhaseAI();
            }
            //正常阶段行为
            else {
                NormalPhaseAI();
            }

            //更新朝向和旋转
            if (Math.Abs(Projectile.velocity.X) > 0.5f) {
                fishDirection = Projectile.velocity.X > 0 ? 1 : -1;
            }

            //根据速度方向计算旋转角度（更灵敏的旋转）
            if (Projectile.velocity.LengthSquared() > 0.1f) {
                fishRotation = Projectile.velocity.ToRotation();
            }

            //模拟游动的波动效果（冲刺阶段减弱波动）
            float swimWaveIntensity = isDashing ? 0.1f : 0.25f;
            float swimWave = (float)Math.Sin(Main.GameUpdateCount * 0.15f + FishID) * swimWaveIntensity;
            Projectile.rotation = fishRotation + swimWave;

            //生成水花特效（冲刺阶段更频繁，仅作底噪填充）
            int dustChance = isDashing ? 14 : 36;
            if (Main.rand.NextBool(dustChance) && fishAlpha > 0.5f) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Water,
                    Projectile.velocity * 0.3f, Scale: Main.rand.NextFloat(0.5f, 1f));
                dust.noGravity = true;
                dust.alpha = 150;
            }
        }

        /// <summary>
        /// 冲刺阶段AI - 螺旋式聚拢并紧跟玩家
        /// </summary>
        private void DashPhaseAI() {
            //冲刺进度（0-1）
            float dashProgress = lifeTimer / (float)DashPhase;

            //螺旋式跟进设计
            //基础参数
            float baseAngle = MathHelper.TwoPi * FishID / 140f; //基础角度分布

            //螺旋层次设计：根据FishID分配到不同的螺旋臂
            int spiralArm = FishID % 5; //5条螺旋臂
            int layerIndex = FishID / 28; //每条螺旋臂约28条鱼，共5层

            //动态螺旋角度：随时间旋转，不同螺旋臂旋转速度不同
            float spiralRotationSpeed = 0.15f + spiralArm * 0.05f; //每条臂旋转速度略有差异
            float spiralAngle = baseAngle + spiralArm * MathHelper.TwoPi / 5f; //5条臂均匀分布
            spiralAngle += lifeTimer * spiralRotationSpeed; //持续旋转

            //螺旋半径：由内向外扩展，形成螺旋线
            //冲刺初期半径小且快速收缩，后期稳定在目标半径
            float targetRadius = 30f + layerIndex * 20f; //5层
            float currentRadius = MathHelper.Lerp(
                150f, //初始半径（较大，模拟聚拢过程）
                targetRadius, //目标半径
                MathHelper.Clamp(dashProgress * 2f, 0f, 1f) //前半段快速收缩
            );

            //螺旋高度波动：沿螺旋臂方向产生上下波动，增强3D感
            float spiralHeightWave = (float)Math.Sin(spiralAngle * 3f + lifeTimer * 0.2f) * 15f;

            //速度感波动：越靠近玩家运动方向的鱼，半径越小（形成尖端效果）
            Vector2 playerDir = OwnerPlayer.velocity.SafeNormalize(Vector2.Zero);
            Vector2 fishDir = new Vector2((float)Math.Cos(spiralAngle), (float)Math.Sin(spiralAngle));
            float alignmentFactor = Vector2.Dot(fishDir, playerDir);
            float radiusModifier = 1f - alignmentFactor * 0.3f; //前方的鱼半径减小30%
            currentRadius *= radiusModifier;

            //计算螺旋位置
            Vector2 spiralOffset = new Vector2(
                (float)Math.Cos(spiralAngle) * currentRadius,
                (float)Math.Sin(spiralAngle) * currentRadius + spiralHeightWave
            );

            //预判玩家位移，鱼群不落后
            float predictionStrength = MathHelper.Lerp(0.8f, 0.3f, dashProgress); //初期预判更强
            Vector2 playerVelocityPredict = OwnerPlayer.velocity * predictionStrength;

            //考虑玩家移动方向，让螺旋向前倾斜
            Vector2 forwardBias = OwnerPlayer.velocity.SafeNormalize(Vector2.Zero) * currentRadius * 0.4f;

            Vector2 targetPosition = OwnerPlayer.Center + spiralOffset + playerVelocityPredict + forwardBias;

            //计算运动力
            Vector2 toTarget = targetPosition - Projectile.Center;
            float distanceToTarget = toTarget.Length();

            Vector2 totalForce = Vector2.Zero;

            //主要吸引力：向目标螺旋位置移动（根据距离动态调整）
            if (distanceToTarget > 5f) {
                //距离越远，吸引力越强
                float urgency = MathHelper.Clamp(distanceToTarget / 80f, 0.8f, 3f);
                totalForce += toTarget.SafeNormalize(Vector2.Zero) * 10f * urgency;
            }

            //切向速度：让鱼沿着螺旋切线方向移动（产生螺旋流动感）
            Vector2 tangentialDirection = new Vector2(
                -(float)Math.Sin(spiralAngle),
                (float)Math.Cos(spiralAngle)
            );
            //切向速度随半径减小而增强（内圈转得更快）
            float tangentialSpeed = (150f - currentRadius) / 150f * 12f + 5f;
            totalForce += tangentialDirection * tangentialSpeed;

            //速度同步：匹配玩家速度
            Vector2 velocityDiff = OwnerPlayer.velocity - Projectile.velocity;
            float syncStrength = MathHelper.Lerp(1.2f, 0.6f, dashProgress); //初期同步更强
            totalForce += velocityDiff * syncStrength;

            //向心力：让鱼持续指向玩家中心（防止螺旋飞散）
            Vector2 centripetalForce = (OwnerPlayer.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
            float centripetalStrength = MathHelper.Lerp(2f, 4f, dashProgress); //后期向心力增强
            totalForce += centripetalForce * centripetalStrength;

            //轻微分离力（避免螺旋臂内部重叠）
            CalculateFlockingBehavior();
            totalForce += separationForce * 0.5f; //权重很低，优先保持螺旋形态

            //沿玩家运动方向的推力（整体向前）
            if (OwnerPlayer.velocity.LengthSquared() > 1f) {
                Vector2 forwardPush = OwnerPlayer.velocity.SafeNormalize(Vector2.Zero);
                totalForce += forwardPush * 4f;
            }

            //螺旋脉冲：周期性的径向收缩/扩张，增强动感
            float pulseFactor = (float)Math.Sin(lifeTimer * 0.3f + spiralArm * MathHelper.PiOver2) * 0.5f;
            Vector2 pulseForce = spiralOffset.SafeNormalize(Vector2.Zero) * pulseFactor;
            totalForce += pulseForce;

            //应用力和速度限制（外层鱼响应更钝，转向时甩尾滞后）
            Projectile.velocity += totalForce * (0.4f * turnAgility);

            //速度限制：根据在螺旋中的位置动态调整
            float baseMaxSpeed = 40f * behaviorRandomness;
            //内圈鱼速度更快（产生螺旋紧致感）
            float speedMultiplier = MathHelper.Lerp(1.3f, 0.9f, currentRadius / 110f);
            float dashMaxSpeed = baseMaxSpeed * speedMultiplier;
            float dashMinSpeed = 18f; //保持高速

            float currentSpeed = Projectile.velocity.Length();
            if (currentSpeed > dashMaxSpeed) {
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * dashMaxSpeed;
            }
            else if (currentSpeed < dashMinSpeed && currentSpeed > 0.1f) {
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * dashMinSpeed;
            }

            //如果鱼偏离螺旋轨迹太远，强制拉回
            if (distanceToTarget > 180f) {
                //严重偏离，直接插值拉回
                Projectile.Center = Vector2.Lerp(Projectile.Center, targetPosition, 0.25f);
            }
            else if (distanceToTarget > 100f) {
                //中度偏离，轻微拉回
                Projectile.Center = Vector2.Lerp(Projectile.Center, targetPosition, 0.1f);
            }

            //螺旋轨迹粒子（只在螺旋臂上的关键位置生成）
            if (FishID % 7 == 0 && Main.rand.NextBool(3)) { //减少粒子密度，但更有目的性
                Vector2 particleVel = tangentialDirection * 3f + Main.rand.NextVector2Circular(1f, 1f);
                Dust spiralDust = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Water,
                    particleVel,
                    Scale: Main.rand.NextFloat(1.2f, 1.8f)
                );
                spiralDust.noGravity = true;
                spiralDust.alpha = 100;
                spiralDust.color = Color.Lerp(Color.White, Color.Cyan, Main.rand.NextFloat());
            }
        }

        /// <summary>
        /// 正常阶段AI，自然的鱼群行为
        /// </summary>
        private void NormalPhaseAI() {
            //boids
            CalculateFlockingBehavior();

            //应用鱼群行为力
            Vector2 totalForce = Vector2.Zero;

            //跟随玩家的吸引力
            Vector2 toPlayer = OwnerPlayer.Center - Projectile.Center;
            float distanceToPlayer = toPlayer.Length();

            if (distanceToPlayer > 200f) {
                //如果离玩家很远，强力拉回
                totalForce += toPlayer.SafeNormalize(Vector2.Zero) * 3.5f;
            }
            else if (distanceToPlayer > 100f) {
                //中等距离，保持跟随
                totalForce += toPlayer.SafeNormalize(Vector2.Zero) * 1.5f;
            }
            else if (distanceToPlayer < 30f) {
                //太近了，稍微远离
                totalForce -= toPlayer.SafeNormalize(Vector2.Zero) * 0.5f;
            }
            else {
                //合适距离，轻微吸引
                totalForce += toPlayer.SafeNormalize(Vector2.Zero) * 0.5f;
            }

            //鱼群行为力
            totalForce += separationForce * 3.0f * behaviorRandomness;  //分离力增强
            totalForce += alignmentForce * 1.5f;                         //对齐力增强
            totalForce += cohesionForce * 1.2f;                          //聚合力增强

            //随机游动（更频繁的方向改变）
            wanderTimer++;
            if (wanderTimer > Main.rand.Next(15, 30)) { //更频繁地改变方向
                wanderTimer = 0;
                randomWander = new Vector2(
                    Main.rand.NextFloat(-1.5f, 1.5f),
                    Main.rand.NextFloat(-1.5f, 1.5f)
                ) * behaviorRandomness;
            }
            totalForce += randomWander * 0.6f;

            //朝向光标的整体方向（更强的引导力）
            Vector2 toMouse = (Main.MouseWorld - Projectile.Center).SafeNormalize(Vector2.Zero);
            totalForce += toMouse * 1.0f;

            //跃动效果（周期性的上下波动）
            float jumpTime = (Main.GameUpdateCount + jumpPhaseOffset) * 0.08f;
            float jumpStrength = (float)Math.Sin(jumpTime) * 0.8f * behaviorRandomness;
            Vector2 jumpForce = new Vector2(0, jumpStrength);

            //在跃动的高峰期增加额外的横向速度
            if (Math.Abs(Math.Sin(jumpTime)) > 0.7f) {
                float horizontalBoost = (float)Math.Cos(jumpTime * 2f) * 0.5f;
                jumpForce.X = horizontalBoost * behaviorRandomness;
            }

            totalForce += jumpForce;

            //应用力并限制速度（外层鱼响应更钝，转向时甩尾滞后）
            Projectile.velocity += totalForce * (0.2f * turnAgility);

            //正常阶段跟随玩家的部分速度（保持相对位置）
            Projectile.position += OwnerPlayer.velocity * 0.3f;

            float maxSpeed = 18f * behaviorRandomness; //提高最大速度
            float minSpeed = 3f; //设置最小速度，防止鱼停滞

            float currentSpeed = Projectile.velocity.Length();
            if (currentSpeed > maxSpeed) {
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * maxSpeed;
            }
            else if (currentSpeed < minSpeed && currentSpeed > 0.1f) {
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * minSpeed;
            }
        }

        /// <summary>
        /// 突袭模式AI，聚拢后形成螺旋尖锥突袭
        /// </summary>
        private void SurgeModeAI() {
            surgeTimer++;

            //快速聚拢（0-15帧）
            if (surgeTimer <= SurgeGatherPhase) {
                GatherPhaseAI();
            }
            //螺旋尖锥突袭（16-45帧）
            else if (surgeTimer <= SurgeGatherPhase + SurgeDashPhase) {
                ConeSurgePhaseAI();
            }
            //消散
            else {
                fishAlpha -= 0.1f;
                if (fishAlpha <= 0f) {
                    Projectile.Kill();
                    return;
                }
            }

            //更新旋转
            if (Projectile.velocity.LengthSquared() > 0.1f) {
                fishRotation = Projectile.velocity.ToRotation();
                Projectile.rotation = fishRotation;
            }

            //更新朝向
            if (Math.Abs(Projectile.velocity.X) > 0.5f) {
                fishDirection = Projectile.velocity.X > 0 ? 1 : -1;
            }

            //突袭轨迹底噪（速度感主体交给流线绘制与鳞光波）
            if (Main.rand.NextBool(8)) {
                Dust surgeDust = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Water,
                    -Projectile.velocity * 0.3f,
                    Scale: Main.rand.NextFloat(1f, 1.5f)
                );
                surgeDust.noGravity = true;
                surgeDust.color = Color.Lerp(FishSwarmVFX.Flow, FishSwarmVFX.Silver, 0.5f);
            }
        }

        /// <summary>
        /// 聚拢阶段AI，所有鱼快速向玩家聚拢
        /// </summary>
        private void GatherPhaseAI() {
            float gatherProgress = surgeTimer / (float)SurgeGatherPhase;

            //目标位置，玩家中心附近的紧密区域
            Vector2 targetPosition = OwnerPlayer.Center;

            //轻微的圆形分布，避免完全重叠
            float gatherRadius = MathHelper.Lerp(15f, 5f, gatherProgress); //逐渐收缩
            float gatherAngle = MathHelper.TwoPi * FishID / 140f;
            Vector2 offset = new Vector2(
                (float)Math.Cos(gatherAngle) * gatherRadius,
                (float)Math.Sin(gatherAngle) * gatherRadius
            );

            targetPosition += offset;

            //强力吸引向玩家
            Vector2 toTarget = targetPosition - Projectile.Center;
            float distance = toTarget.Length();

            if (distance > 5f) {
                Vector2 pullForce = toTarget.SafeNormalize(Vector2.Zero) * 15f; //非常强的吸引力
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, pullForce, 0.3f);
            }

            //速度限制
            float maxSpeed = 50f;
            if (Projectile.velocity.Length() > maxSpeed) {
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * maxSpeed;
            }

            //淡入
            if (fishAlpha < 1f) {
                fishAlpha += 0.15f;
            }

            //聚拢粒子效果
            if (Main.rand.NextBool(10)) {
                Dust gatherDust = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Water,
                    (OwnerPlayer.Center - Projectile.Center).SafeNormalize(Vector2.Zero) * 3f,
                    Scale: Main.rand.NextFloat(1.2f, 1.8f)
                );
                gatherDust.noGravity = true;
                gatherDust.color = FishSwarmVFX.Flow;
            }
        }

        /// <summary>
        /// 螺旋尖锥突袭阶段AI，形成尖锥形螺旋突袭
        /// </summary>
        private void ConeSurgePhaseAI() {
            int surgePhaseTimer = surgeTimer - SurgeGatherPhase;
            float surgeProgress = surgePhaseTimer / (float)SurgeDashPhase;

            //螺旋参数
            int spiralArm = FishID % 5; //5条螺旋臂
            int layerIndex = FishID / 28; //5层深度

            //动态螺旋角度
            float spiralRotationSpeed = 0.25f + spiralArm * 0.08f; //更快的旋转
            float spiralAngle = MathHelper.TwoPi * FishID / 140f + spiralArm * MathHelper.TwoPi / 5f;
            spiralAngle += surgePhaseTimer * spiralRotationSpeed;

            //尖锥形状，前方的鱼聚集更紧密，后方更分散
            //距离随时间推进而增加，形成拉长的锥形
            float coneLength = surgeProgress * 400f; //锥形长度逐渐拉长
            float coneRadius = (layerIndex + 1) * 15f + surgeProgress * 30f; //后方半径逐渐增大

            //计算在锥形中的位置
            //前方鱼（小FishID）在锥尖，后方鱼在锥底
            float positionInCone = FishID / 140f; //0-1，0是锥尖
            float distanceFromTip = coneLength * positionInCone;
            float radiusAtPosition = coneRadius * positionInCone;

            //螺旋偏移
            Vector2 spiralOffset = new Vector2(
                (float)Math.Cos(spiralAngle) * radiusAtPosition,
                (float)Math.Sin(spiralAngle) * radiusAtPosition
            );

            //目标位置：沿突袭方向前进
            Vector2 tipPosition = OwnerPlayer.Center + surgeDirection * (200f + surgeProgress * 800f); //锥尖不断前进
            Vector2 targetPosition = tipPosition - surgeDirection * distanceFromTip + spiralOffset;

            //运动力计算
            Vector2 toTarget = targetPosition - Projectile.Center;
            float distanceToTarget = toTarget.Length();

            Vector2 totalForce = Vector2.Zero;

            //追随锥形位置
            if (distanceToTarget > 10f) {
                totalForce += toTarget.SafeNormalize(Vector2.Zero) * 12f;
            }

            //整体向突袭方向高速移动
            totalForce += surgeDirection * 20f;

            //螺旋旋转
            Vector2 tangentialDir = new Vector2(
                -(float)Math.Sin(spiralAngle),
                (float)Math.Cos(spiralAngle)
            );
            totalForce += tangentialDir * 8f;

            //轻微分离力
            CalculateFlockingBehavior();
            totalForce += separationForce * 0.3f;

            //应用力
            Projectile.velocity += totalForce * 0.5f;

            //速度限制：随时间增加
            float maxSpeed = 40f + surgeProgress * 40f; //20-60
            float minSpeed = 30f;

            float currentSpeed = Projectile.velocity.Length();
            if (currentSpeed > maxSpeed) {
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * maxSpeed;
            }
            else if (currentSpeed < minSpeed) {
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * minSpeed;
            }

            //强制锥形位置修正
            if (distanceToTarget > 200f) {
                Projectile.Center = Vector2.Lerp(Projectile.Center, targetPosition, 0.2f);
            }

            //尖锥螺旋粒子特效
            if (FishID % 5 == 0 && Main.rand.NextBool(3)) {
                Dust coneDust = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Water,
                    surgeDirection * Main.rand.NextFloat(3f, 6f),
                    Scale: Main.rand.NextFloat(1.5f, 2.5f)
                );
                coneDust.noGravity = true;
                coneDust.color = Color.Lerp(FishSwarmVFX.Flow, FishSwarmVFX.Silver, Main.rand.NextFloat());
                coneDust.alpha = 50;
            }
        }

        /// <summary>
        /// 计算鱼群算法的分离、对齐和聚合力量
        /// </summary>
        private void CalculateFlockingBehavior() {
            separationForce = Vector2.Zero;
            alignmentForce = Vector2.Zero;
            cohesionForce = Vector2.Zero;

            Vector2 centerOfMass = OwnerPlayer.Center;
            Vector2 averageVelocity = Vector2.Zero;

            int nearbyFishCount = 0;

            //遍历所有鱼，计算鱼群行为
            for (int i = 0; i < Main.maxProjectiles; i++) {
                if (i == Projectile.whoAmI) continue;

                Projectile other = Main.projectile[i];

                //只考虑同一群体的鱼
                if (other.active && other.type == Projectile.type) {
                    float distance = Vector2.Distance(Projectile.Center, other.Center);

                    //距离过近，施加分离力
                    if (distance < 50f) {
                        Vector2 toOther = (Projectile.Center - other.Center).SafeNormalize(Vector2.Zero);
                        separationForce += toOther / distance;
                    }

                    //累加质心和速度
                    centerOfMass += other.Center;
                    averageVelocity += other.velocity;
                    nearbyFishCount++;
                }
            }

            if (nearbyFishCount > 0) {
                //质心和平均速度
                centerOfMass /= nearbyFishCount;
                averageVelocity /= nearbyFishCount;
                lastAvgVel = averageVelocity;

                //对齐力：向平均速度方向旋转
                float alignmentIntensity = 0.1f;
                Vector2 alignedVelocity = averageVelocity.SafeNormalize(Vector2.Zero) * Projectile.velocity.Length();
                alignmentForce = (alignedVelocity - Projectile.velocity) * alignmentIntensity;

                //聚合力：向质心移动
                float cohesionIntensity = 0.05f;
                Vector2 toCenter = (centerOfMass - Projectile.Center).SafeNormalize(Vector2.Zero);
                cohesionForce = toCenter * cohesionIntensity;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //命中演出：水珠扇 + 偶发碎光与轻水声（伤害逻辑不变）
            FishSwarmVFX.HitSplash(Projectile.Center, Projectile.velocity);
        }

        public override void OnKill(int timeLeft) {
            //突袭中耗尽穿透而死的鱼留下坠落水珠
            if (surgeModeActive) {
                FishSwarmVFX.SurgeFishDeath(Projectile.Center, Projectile.velocity);
            }
        }

        /// <summary>
        /// 群体共享流向：鳞光传染波的传播方向。突袭取突袭方向，
        /// 平时取玩家位移方向，兜底用群体平均速度或自身速度
        /// </summary>
        private Vector2 SchoolFlowDir() {
            if (surgeModeActive) {
                return surgeDirection;
            }
            if (OwnerPlayer != null && OwnerPlayer.velocity.LengthSquared() > 16f) {
                return OwnerPlayer.velocity.SafeNormalize(Vector2.UnitX);
            }
            if (lastAvgVel.LengthSquared() > 4f) {
                return lastAvgVel.SafeNormalize(Vector2.UnitX);
            }
            return Projectile.velocity.SafeNormalize(Vector2.UnitX);
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadItem(ItemID.SpecularFish);//加载关于鱼的纹理
            Texture2D value = TextureAssets.Item[ItemID.SpecularFish].Value;//获取鱼的纹理

            //计算绘制参数
            Vector2 drawPosition = Projectile.Center - Main.screenPosition;
            Rectangle sourceRect = value.Frame(1, 1, 0, 0);
            Vector2 origin = sourceRect.Size() / 2f;
            float rotOffset = fishDirection > 0 ? MathHelper.PiOver4 : -MathHelper.PiOver4;
            float drawRotation = Projectile.rotation + rotOffset;

            //根据朝向决定翻转
            SpriteEffects effects = fishDirection > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically;

            float speed = Projectile.velocity.Length();
            bool isDashing = lifeTimer <= DashPhase;
            bool fast = isDashing || surgeModeActive || speed > 20f;

            //鳞光传染波：波前沿群体流向扫过、朝向又与流向一致的鱼才反光，
            //离散小闪点在群里"传染"式波及（纯绘制，无粒子开销）
            Vector2 flow = SchoolFlowDir();
            float align = Vector2.Dot(Projectile.velocity.SafeNormalize(Vector2.Zero), flow);
            Vector2 schoolCenter = OwnerPlayer?.Center ?? Projectile.Center;
            float wave = (Main.GlobalTimeWrappedHourly * 1.35f
                - Vector2.Dot(Projectile.Center - schoolCenter, flow) / 300f + FishID * 0.017f) % 1f;
            if (wave < 0f) {
                wave += 1f;
            }
            float glintPulse = align > 0.84f && wave < 0.085f && fishAlpha > 0.6f ? 1f - wave / 0.085f : 0f;

            //运动拖影：少量残影向尾端变暗收小，读作滑动而非贴图串
            int[] ghostIdx = fast ? GhostIdxFast : GhostIdxSlow;
            float ghostBase = fast ? 0.36f : 0.22f;
            for (int k = 0; k < ghostIdx.Length; k++) {
                int gi = ghostIdx[k];
                if (gi >= Projectile.oldPos.Length || Projectile.oldPos[gi] == Vector2.Zero) {
                    continue;
                }
                float f = 1f - gi / (float)Projectile.oldPos.Length;
                float gRot = (gi < Projectile.oldRot.Length ? Projectile.oldRot[gi] : Projectile.rotation) + rotOffset;
                Vector2 gPos = Projectile.oldPos[gi] + Projectile.Size / 2f - Main.screenPosition;
                Color gCol = Color.Lerp(FishSwarmVFX.Deep, FishSwarmVFX.Flow, f * 0.6f) * (ghostBase * f * fishAlpha);
                Main.EntitySpriteDraw(value, gPos, sourceRect, gCol, gRot, origin
                    , fishScale * (0.72f + 0.2f * f), effects, 0);
            }

            //突袭中三分之一的鱼贴细加色流线（速度各向异性）
            if (surgeModeActive && FishID % 3 == 0 && speed > 22f) {
                Texture2D line = CWRAsset.Line?.Value;
                if (line != null) {
                    Vector2 velNorm = Projectile.velocity.SafeNormalize(Vector2.UnitX);
                    float len = speed * 1.1f;
                    Vector2 lineScale = new(0.3f, len / line.Height);
                    Main.EntitySpriteDraw(line, drawPosition - velNorm * len * 0.5f, null
                        , FishSwarmVFX.Flow with { A = 0 } * (0.30f * fishAlpha)
                        , Projectile.velocity.ToRotation() + MathHelper.PiOver2
                        , line.Size() / 2f, lineScale, SpriteEffects.None, 0);
                }
            }

            //主体：鳞银体色，闪点帧短暂提到近白（禁常驻纯白）
            Color bodyCol = Color.Lerp(lightColor, FishSwarmVFX.Silver, 0.22f);
            if (glintPulse > 0f) {
                bodyCol = Color.Lerp(bodyCol, FishSwarmVFX.Spec, glintPulse * 0.55f);
            }
            Main.EntitySpriteDraw(value, drawPosition, sourceRect, bodyCol * fishAlpha
                , drawRotation, origin, fishScale, effects, 0);

            //离散星形 glint（A=0 加色观感，瞬现即灭）
            if (glintPulse > 0f) {
                Texture2D star = CWRAsset.StarGlow01?.Value;
                if (star != null) {
                    Main.EntitySpriteDraw(star, drawPosition, null
                        , FishSwarmVFX.Spec with { A = 0 } * (glintPulse * fishAlpha)
                        , drawRotation, star.Size() / 2f, 0.085f + glintPulse * 0.05f, SpriteEffects.None, 0);
                }
            }

            return false;
        }
    }
}
