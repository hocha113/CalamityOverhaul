﻿using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Rarities;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.RemakeItems;
using InnoVault.GameSystem;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.Graphics;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend
{
    internal class HalibutPlayer : PlayerOverride//这个类用于存储一些与玩家相关的额外数据
    {
        //自定义一些与玩家相关的额外数据都可以放在这里，这个类会自动附加到每个玩家身上
        
        /// <summary>
        /// 移形换影技能激活状态
        /// </summary>
        public bool FishSwarmActive { get; set; }
        
        /// <summary>
        /// 技能持续时间计数器
        /// </summary>
        public int FishSwarmTimer { get; set; }
        
        /// <summary>
        /// 技能最大持续时间（5秒 = 300帧）
        /// </summary>
        public const int FishSwarmDuration = 300;
        
        /// <summary>
        /// 技能冷却时间
        /// </summary>
        public int FishSwarmCooldown { get; set; }
        
        /// <summary>
        /// 技能冷却最大时间（10秒）
        /// </summary>
        public const int FishSwarmMaxCooldown = 600;

        public override void ResetEffects() {//用于每帧恢复数据
            
        }

        public override void PostUpdate() {//在每帧更新后进行一些操作
            // 更新技能状态
            if (FishSwarmActive) {
                FishSwarmTimer++;
                
                if (FishSwarmTimer >= FishSwarmDuration) {
                    // 技能结束
                    FishSwarmActive = false;
                    FishSwarmTimer = 0;
                    FishSwarmCooldown = 60;
                }
            }
            
            // 更新冷却
            if (FishSwarmCooldown > 0) {
                FishSwarmCooldown--;
            }
        }

        public override bool PreDrawPlayers(ref Camera camera, ref IEnumerable<Player> players) {
            //这里可以操纵players移除不需要绘制的玩家达到隐藏玩家的目的
            if (FishSwarmActive) {
                // 移除正在使用技能的玩家，使其隐藏
                List<Player> visiblePlayers = new List<Player>();
                foreach (Player player in players) {
                    if (player.whoAmI != Player.whoAmI) {
                        visiblePlayers.Add(player);
                    }
                }
                players = visiblePlayers;
            }
            return true;
        }
    }

    internal class HalibutOverride : CWRItemOverride
    {
        #region Data
        public override int TargetID => ModContent.ItemType<HalibutCannon>();
        /// <summary>
        /// 每个时期阶段对应的伤害，这个成员一般不需要直接访问，而是使用<see cref="GetOnDamage"/>
        /// </summary>
        private static Dictionary<int, int> DamageDictionary = new Dictionary<int, int>();
        /// <summary>
        /// 每个时期阶段对应的额外暴击振幅的字典，这个成员一般不需要直接访问，而是使用<see cref="GetOnCrit"/>
        /// </summary>
        private static Dictionary<int, int> SetLevelCritDictionary = new Dictionary<int, int>();
        /// <summary>
        /// 获取开局的伤害
        /// </summary>
        public static int GetStartDamage => DamageDictionary[0];
        #endregion
        /// <summary>
        /// 获得成长等级
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public static int GetLevel(Item item) {
            if (item.type != ModContent.ItemType<HalibutCannon>()) {
                return 0;
            }
            CWRItem cwrItem = item.CWR();
            if (cwrItem == null) {
                return 0;
            }
            if (cwrItem.LegendData == null) {
                return 0;
            }
            if (!CWRServerConfig.Instance.WeaponEnhancementSystem) {
                return 12;
            }
            return cwrItem.LegendData.Level;
        }
        /// <summary>
        /// 获取时期对应的伤害
        /// </summary>
        public static int GetOnDamage(Item item) => DamageDictionary[GetLevel(item)];
        /// <summary>
        /// 计算伤害比例
        /// </summary>
        public static float GetSengsDamage(Item item) => GetOnDamage(item) / (float)GetStartDamage;
        /// <summary>
        /// 根据<see cref="GetOnDamage"/>获取一个与<see cref="RangedDamageClass"/>相关的乘算伤害
        /// </summary>
        public static int ActualTrueMeleeDamage(Item item) => (int)(GetOnDamage(item) * Main.LocalPlayer.GetDamage<RangedDamageClass>().Additive);
        /// <summary>
        /// 获取时期对应的额外暴击
        /// </summary>
        public static int GetOnCrit(Item item) => SetLevelCritDictionary[GetLevel(item)];

        public static void LoadWeaponData() {
            DamageDictionary = new Dictionary<int, int>(){
                {0, 3 },
                {1, 4 },
                {2, 5 },
                {3, 7 },
                {4, 8 },
                {5, 10 },
                {6, 13 },
                {7, 15 },
                {8, 18 },
                {9, 22 },
                {10, 26 },
                {11, 30 },
                {12, 34 },
                {13, 38 },
                {14, 46 }
            };
            SetLevelCritDictionary = new Dictionary<int, int>(){
                {0, 0 },
                {1, 1 },
                {2, 1 },
                {3, 2 },
                {4, 2 },
                {5, 5 },
                {6, 5 },
                {7, 8 },
                {8, 9 },
                {9, 9 },
                {10, 10 },
                {11, 11 },
                {12, 13 },
                {13, 15 },
                {14, 20 }
            };
        }
        public override void SetDefaults(Item item) => SetDefaultsFunc(item);
        public override bool? On_ModifyWeaponCrit(Item item, Player player, ref float crit) {
            crit += GetOnCrit(item);
            return false;
        }
        public override bool On_ModifyWeaponDamage(Item item, Player player, ref StatModifier damage) {
            CWRUtils.ModifyLegendWeaponDamageFunc(item, GetOnDamage(item), GetStartDamage, ref damage);
            return false;
        }
        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips) => HalibutText.SetTooltip(item, ref tooltips);

        public override void SetStaticDefaults() => LoadWeaponData();

        public static void SetDefaultsFunc(Item Item) {
            LoadWeaponData();
            Item.damage = GetStartDamage;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 118;
            Item.height = 56;
            Item.useTime = 10;
            Item.useAnimation = 20;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.rare = ModContent.RarityType<HotPink>();
            Item.noMelee = true;
            Item.knockBack = 1f;
            Item.value = CalamityGlobalItem.RarityHotPinkBuyPrice;
            Item.UseSound = SoundID.Item38;
            Item.autoReuse = true;
            Item.shoot = ProjectileID.Bullet;
            Item.shootSpeed = 12f;
            Item.useAmmo = AmmoID.Bullet;
            Item.Calamity().canFirePointBlankShots = true;
            Item.CWR().LegendData = new HalibutData();
        }

        public override bool? CanUseItem(Item item, Player player) {
            item.UseSound = SoundID.Item38;
            if (player.altFunctionUse == 2) {
                item.UseSound = null;
            }
            return true;
        }

        public override bool? AltFunctionUse(Item item, Player player) {
            return true;
        }

        public override bool? UseItem(Item item, Player player) {
            if (player.altFunctionUse == 2) {
                AltUse(item, player);
                return false;
            }
            return null;
        }

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                return false;//右键不触发普通攻击
            }
            return null;
        }

        public static void AltUse(Item item, Player player) {//额外的的独立封装，玩家右键使用时调用，用于触发技能
            HalibutPlayer halibutPlayer = player.GetOverride<HalibutPlayer>();
            
            // 检查技能是否在冷却中
            if (halibutPlayer.FishSwarmCooldown > 0 || halibutPlayer.FishSwarmActive) {
                return;
            }

            // 激活技能
            halibutPlayer.FishSwarmActive = true;
            halibutPlayer.FishSwarmTimer = 0;
            
            // 计算冲刺方向（朝向光标）
            Vector2 dashDirection = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.Zero);
            
            // 生成控制器弹幕（管理玩家移动和技能状态）
            int controller = Projectile.NewProjectile(
                player.GetSource_ItemUse(item),
                player.Center,
                dashDirection, // 存储初始冲刺方向
                ModContent.ProjectileType<FishSwarmController>(),
                0,
                0f,
                player.whoAmI
            );
            
            // 生成鱼群130-140条鱼，增加数量以提高视觉效果）
            int fishCount = Main.rand.Next(130, 141);
            for (int i = 0; i < fishCount; i++) {
                // 在玩家周围随机位置生成鱼
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
                    ai0: i // 用于区分不同的鱼
                );
                
                if (Main.projectile[proj].ModProjectile is FishingFly fish) {
                    fish.OwnerPlayer = player;
                }
            }

            // 播放音效
            SoundEngine.PlaySound(SoundID.Splash, player.Center);
            SoundEngine.PlaySound(SoundID.Item71, player.Center); //冲刺音效
        }
    }

    /// <summary>
    /// 鱼群控制器 - 管理玩家移动和技能状态
    /// </summary>
    internal class FishSwarmController : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        
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

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = HalibutPlayer.FishSwarmDuration;
            Projectile.alpha = 255; // 完全透明
        }

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }
            
            HalibutPlayer halibutPlayer = Owner.GetOverride<HalibutPlayer>();
            
            // 检查技能是否结束
            if (!halibutPlayer.FishSwarmActive) {
                Projectile.Kill();
                return;
            }
            
            // 弹幕位置跟随玩家
            Projectile.Center = Owner.Center;

            Owner.noFallDmg = true;//别冲一下给自己摔死了

            DashTimer++;
            
            // === 冲刺阶段 ===
            if (DashTimer <= DashDuration) {
                // 冲刺阶段：强力加速
                float dashProgress = DashTimer / DashDuration;
                
                // 使用缓动函数实现更有冲击力的加速
                float speedMultiplier = 1f - (float)Math.Pow(dashProgress, 2); // 平方衰减
                float currentDashSpeed = DashSpeed * speedMultiplier;
                
                // 在冲刺阶段允许轻微调整方向
                Vector2 toMouse = (Main.MouseWorld - Owner.Center).SafeNormalize(Vector2.Zero);
                Vector2 adjustedDirection = Vector2.Lerp(DashDirection, toMouse, 0.05f).SafeNormalize(Vector2.Zero);
                
                Owner.velocity = adjustedDirection * (currentDashSpeed + NormalSpeed * dashProgress);
                
                // === 螺旋冲刺特效增强 ===
                // 水花粒子（更密集）
                if (Main.rand.NextBool(2)) {
                    Vector2 dustPos = Owner.Center + Main.rand.NextVector2Circular(120f, 120f);
                    Dust dust = Dust.NewDustPerfect(dustPos, DustID.Water, 
                        -Owner.velocity * 0.3f, Scale: Main.rand.NextFloat(1f, 1.5f));
                    dust.noGravity = true;
                }
                
                // 螺旋涡流粒子（围绕玩家旋转）
                if (Main.rand.NextBool(3)) {
                    float spiralAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                    float spiralRadius = Main.rand.NextFloat(60f, 100f);
                    Vector2 spiralOffset = new Vector2(
                        (float)Math.Cos(spiralAngle) * spiralRadius,
                        (float)Math.Sin(spiralAngle) * spiralRadius
                    );
                    
                    Vector2 tangentialVel = new Vector2(
                        -(float)Math.Sin(spiralAngle),
                        (float)Math.Cos(spiralAngle)
                    ) * 5f;
                    
                    Dust spiralDust = Dust.NewDustPerfect(
                        Owner.Center + spiralOffset, 
                        DustID.Water,
                        tangentialVel + Owner.velocity * 0.2f,
                        Scale: Main.rand.NextFloat(1.2f, 2f)
                    );
                    spiralDust.noGravity = true;
                    spiralDust.alpha = 80;
                    spiralDust.color = Color.Lerp(Color.White, Color.Cyan, 0.5f);
                }
                
                // 冲击波效果（关键时刻）
                if (DashTimer == 1 || DashTimer == 10) {
                    for (int i = 0; i < 20; i++) {
                        float angle = MathHelper.TwoPi * i / 20f;
                        Vector2 shockwaveVel = new Vector2(
                            (float)Math.Cos(angle),
                            (float)Math.Sin(angle)
                        ) * 8f;
                        
                        Dust shockDust = Dust.NewDustPerfect(
                            Owner.Center,
                            DustID.Water,
                            shockwaveVel,
                            Scale: Main.rand.NextFloat(1.5f, 2.5f)
                        );
                        shockDust.noGravity = true;
                        shockDust.alpha = 50;
                    }
                }
            }
            // === 持续移动阶段 ===
            else {
                // 计算目标速度（朝向光标）
                Vector2 targetDirection = (Main.MouseWorld - Owner.Center).SafeNormalize(Vector2.Zero);
                Vector2 targetVelocity = targetDirection * NormalSpeed;
                
                // 使用更快的插值速度，让移动更灵敏
                float lerpSpeed = 0.15f;
                
                // 增加一些周期性的速度波动，模拟鱼群游动的节奏感
                float rhythmBoost = 1f + (float)Math.Sin(DashTimer * 0.15f) * 0.2f;
                targetVelocity *= rhythmBoost;
                
                Owner.velocity = Vector2.Lerp(Owner.velocity, targetVelocity, lerpSpeed);
                Owner.direction = Math.Sign(Owner.velocity.X);
                
                // 持续移动的水花特效（频率较低）
                if (Main.rand.NextBool(5)) {
                    Vector2 dustPos = Owner.Center + Main.rand.NextVector2Circular(15f, 15f);
                    Dust dust = Dust.NewDustPerfect(dustPos, DustID.Water, 
                        -Owner.velocity * 0.2f, Scale: Main.rand.NextFloat(0.8f, 1.2f));
                    dust.noGravity = true;
                    dust.alpha = 100;
                }
            }
            
            // 防止玩家受到其他速度影响
            Owner.maxFallSpeed = 100f;
            Owner.gravity = 0f;
        }

        public override void OnKill(int timeLeft) {
            // 技能结束时恢复玩家重力
            if (Owner != null && Owner.active) {
                Owner.gravity = Player.defaultGravity;
            }
        }
    }

    internal class FishingFly : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;//透明贴图，因为纹理会手动获取
        
        /// <summary>
        /// 拥有者玩家
        /// </summary>
        public Player OwnerPlayer { get; set; }
        
        /// <summary>
        /// 鱼的个体ID（用于行为差异化）
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
        /// 个体的随机行为偏好（用于增加多样性）
        /// </summary>
        private float behaviorRandomness = 1f;
        
        /// <summary>
        /// 跃动周期偏移
        /// </summary>
        private float jumpPhaseOffset = 0f;
        
        /// <summary>
        /// 生命计时器（用于判断冲刺阶段）
        /// </summary>
        private int lifeTimer = 0;
        
        /// <summary>
        /// 冲刺阶段持续时间（与控制器同步）
        /// </summary>
        private const int DashPhase = 20;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8; // 冲刺时需要更长的拖尾
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = HalibutPlayer.FishSwarmDuration + 60; // 额外时间用于淡出
        }

        public override void AI() {
            // 找到拥有者
            if (OwnerPlayer == null || !OwnerPlayer.active) {
                Projectile.Kill();
                return;
            }
            
            HalibutPlayer halibutPlayer = OwnerPlayer.GetOverride<HalibutPlayer>();
            
            // 检查技能是否结束
            if (!halibutPlayer.FishSwarmActive) {
                // 淡出效果
                fishAlpha -= 0.05f;
                if (fishAlpha <= 0f) {
                    Projectile.Kill();
                    return;
                }
            }
            else {
                // 淡入效果（冲刺阶段快速淡入）
                if (fishAlpha < 1f) {
                    float fadeSpeed = lifeTimer < DashPhase ? 0.25f : 0.15f; // 冲刺阶段更快淡入
                    fishAlpha += fadeSpeed;
                    if (fishAlpha > 1f) fishAlpha = 1f;
                }
            }
            
            // 初始化鱼的参数（只在第一帧）
            if (lifeTimer == 0) {
                fishScale = Main.rand.NextFloat(0.6f, 1.3f);
                behaviorRandomness = Main.rand.NextFloat(0.8f, 1.3f);
                jumpPhaseOffset = Main.rand.NextFloat(0f, MathHelper.TwoPi);
            }
            
            lifeTimer++;
            
            // 判断是否在冲刺阶段
            bool isDashing = lifeTimer <= DashPhase;
            
            // === 冲刺阶段特殊行为 ===
            if (isDashing) {
                DashPhaseAI();
            }
            // === 正常阶段行为 ===
            else {
                NormalPhaseAI();
            }
            
            // 更新朝向和旋转
            if (Math.Abs(Projectile.velocity.X) > 0.5f) {
                fishDirection = Projectile.velocity.X > 0 ? 1 : -1;
            }
            
            // 根据速度方向计算旋转角度（更灵敏的旋转）
            if (Projectile.velocity.LengthSquared() > 0.1f) {
                fishRotation = Projectile.velocity.ToRotation();
            }
            
            // 模拟游动的波动效果（冲刺阶段减弱波动）
            float swimWaveIntensity = isDashing ? 0.1f : 0.25f;
            float swimWave = (float)Math.Sin(Main.GameUpdateCount * 0.15f + FishID) * swimWaveIntensity;
            Projectile.rotation = fishRotation + swimWave;
            
            // 生成水花特效（冲刺阶段更频繁）
            int dustChance = isDashing ? 10 : 30;
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
            Vector2 toPlayer = OwnerPlayer.Center - Projectile.Center;
            float distanceToPlayer = toPlayer.Length();
            
            // 冲刺进度（0-1）
            float dashProgress = lifeTimer / (float)DashPhase;
            
            // === 螺旋式跟进设计 ===
            // 基础参数
            float baseAngle = MathHelper.TwoPi * FishID / 140f; // 基础角度分布
            
            // 螺旋层次设计：根据FishID分配到不同的螺旋臂
            int spiralArm = FishID % 5; // 5条螺旋臂
            int layerIndex = FishID / 28; // 每条螺旋臂约28条鱼，共5层
            
            // 动态螺旋角度：随时间旋转，不同螺旋臂旋转速度不同
            float spiralRotationSpeed = 0.15f + spiralArm * 0.05f; // 每条臂旋转速度略有差异
            float spiralAngle = baseAngle + spiralArm * MathHelper.TwoPi / 5f; // 5条臂均匀分布
            spiralAngle += lifeTimer * spiralRotationSpeed; // 持续旋转
            
            // 螺旋半径：由内向外扩展，形成螺旋线
            // 冲刺初期半径小且快速收缩，后期稳定在目标半径
            float targetRadius = 30f + layerIndex * 20f; // 5层：30, 50, 70, 90, 110
            float currentRadius = MathHelper.Lerp(
                150f, // 初始半径（较大，模拟聚拢过程）
                targetRadius, // 目标半径
                MathHelper.Clamp(dashProgress * 2f, 0f, 1f) // 前半段快速收缩
            );
            
            // 螺旋高度波动：沿螺旋臂方向产生上下波动，增强3D感
            float spiralHeightWave = (float)Math.Sin(spiralAngle * 3f + lifeTimer * 0.2f) * 15f;
            
            // 速度感波动：越靠近玩家运动方向的鱼，半径越小（形成尖端效果）
            Vector2 playerDir = OwnerPlayer.velocity.SafeNormalize(Vector2.Zero);
            Vector2 fishDir = new Vector2((float)Math.Cos(spiralAngle), (float)Math.Sin(spiralAngle));
            float alignmentFactor = Vector2.Dot(fishDir, playerDir);
            float radiusModifier = 1f - alignmentFactor * 0.3f; // 前方的鱼半径减小30%
            currentRadius *= radiusModifier;
            
            // 计算螺旋位置
            Vector2 spiralOffset = new Vector2(
                (float)Math.Cos(spiralAngle) * currentRadius,
                (float)Math.Sin(spiralAngle) * currentRadius + spiralHeightWave
            );
            
            // === 预判玩家位置 ===
            // 根据玩家速度预判未来位置，确保鱼群不会落后
            float predictionStrength = MathHelper.Lerp(0.8f, 0.3f, dashProgress); // 初期预判更强
            Vector2 playerVelocityPredict = OwnerPlayer.velocity * predictionStrength;
            
            // 考虑玩家移动方向，让螺旋向前倾斜
            Vector2 forwardBias = OwnerPlayer.velocity.SafeNormalize(Vector2.Zero) * currentRadius * 0.4f;
            
            Vector2 targetPosition = OwnerPlayer.Center + spiralOffset + playerVelocityPredict + forwardBias;
            
            // === 计算运动力 ===
            Vector2 toTarget = targetPosition - Projectile.Center;
            float distanceToTarget = toTarget.Length();
            
            Vector2 totalForce = Vector2.Zero;
            
            // 1. 主要吸引力：向目标螺旋位置移动（根据距离动态调整）
            if (distanceToTarget > 5f) {
                // 距离越远，吸引力越强
                float urgency = MathHelper.Clamp(distanceToTarget / 80f, 0.8f, 3f);
                totalForce += toTarget.SafeNormalize(Vector2.Zero) * 10f * urgency;
            }
            
            // 2. 切向速度：让鱼沿着螺旋切线方向移动（产生螺旋流动感）
            Vector2 tangentialDirection = new Vector2(
                -(float)Math.Sin(spiralAngle),
                (float)Math.Cos(spiralAngle)
            );
            // 切向速度随半径减小而增强（内圈转得更快）
            float tangentialSpeed = (150f - currentRadius) / 150f * 12f + 5f;
            totalForce += tangentialDirection * tangentialSpeed;
            
            // 3. 速度同步：匹配玩家速度
            Vector2 velocityDiff = OwnerPlayer.velocity - Projectile.velocity;
            float syncStrength = MathHelper.Lerp(1.2f, 0.6f, dashProgress); // 初期同步更强
            totalForce += velocityDiff * syncStrength;
            
            // 4. 向心力：让鱼持续指向玩家中心（防止螺旋飞散）
            Vector2 centripetalForce = (OwnerPlayer.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
            float centripetalStrength = MathHelper.Lerp(2f, 4f, dashProgress); // 后期向心力增强
            totalForce += centripetalForce * centripetalStrength;
            
            // 5. 轻微分离力（避免螺旋臂内部重叠）
            CalculateFlockingBehavior();
            totalForce += separationForce * 0.5f; // 权重很低，优先保持螺旋形态
            
            // 6. 沿玩家运动方向的推力（整体向前）
            if (OwnerPlayer.velocity.LengthSquared() > 1f) {
                Vector2 forwardPush = OwnerPlayer.velocity.SafeNormalize(Vector2.Zero);
                totalForce += forwardPush * 4f;
            }
            
            // 7. 螺旋脉冲：周期性的径向收缩/扩张，增强动感
            float pulseFactor = (float)Math.Sin(lifeTimer * 0.3f + spiralArm * MathHelper.PiOver2) * 0.5f;
            Vector2 pulseForce = spiralOffset.SafeNormalize(Vector2.Zero) * pulseFactor;
            totalForce += pulseForce;
            
            // === 应用力和速度限制 ===
            Projectile.velocity += totalForce * 0.4f; // 更高的加速度，响应更快
            
            // 速度限制：根据在螺旋中的位置动态调整
            float baseMaxSpeed = 40f * behaviorRandomness;
            // 内圈鱼速度更快（产生螺旋紧致感）
            float speedMultiplier = MathHelper.Lerp(1.3f, 0.9f, currentRadius / 110f);
            float dashMaxSpeed = baseMaxSpeed * speedMultiplier;
            float dashMinSpeed = 18f; // 保持高速
            
            float currentSpeed = Projectile.velocity.Length();
            if (currentSpeed > dashMaxSpeed) {
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * dashMaxSpeed;
            }
            else if (currentSpeed < dashMinSpeed && currentSpeed > 0.1f) {
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * dashMinSpeed;
            }
            
            // === 强制位置修正（确保螺旋形态） ===
            // 如果鱼偏离螺旋轨迹太远，强制拉回
            if (distanceToTarget > 180f) {
                // 严重偏离，直接插值拉回
                Projectile.Center = Vector2.Lerp(Projectile.Center, targetPosition, 0.25f);
            }
            else if (distanceToTarget > 100f) {
                // 中度偏离，轻微拉回
                Projectile.Center = Vector2.Lerp(Projectile.Center, targetPosition, 0.1f);
            }
            
            // === 视觉效果增强 ===
            // 螺旋轨迹粒子（只在螺旋臂上的关键位置生成）
            if (FishID % 7 == 0 && Main.rand.NextBool(3)) { // 减少粒子密度，但更有目的性
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
        /// 正常阶段AI - 自然的鱼群行为
        /// </summary>
        private void NormalPhaseAI() {
            // === 鱼群算法实现 ===
            CalculateFlockingBehavior();
            
            // 应用鱼群行为力
            Vector2 totalForce = Vector2.Zero;
            
            // 1. 跟随玩家的吸引力
            Vector2 toPlayer = OwnerPlayer.Center - Projectile.Center;
            float distanceToPlayer = toPlayer.Length();
            
            if (distanceToPlayer > 200f) {
                // 如果离玩家很远，强力拉回
                totalForce += toPlayer.SafeNormalize(Vector2.Zero) * 3.5f;
            }
            else if (distanceToPlayer > 100f) {
                // 中等距离，保持跟随
                totalForce += toPlayer.SafeNormalize(Vector2.Zero) * 1.5f;
            }
            else if (distanceToPlayer < 30f) {
                // 太近了，稍微远离
                totalForce -= toPlayer.SafeNormalize(Vector2.Zero) * 0.5f;
            }
            else {
                // 合适距离，轻微吸引
                totalForce += toPlayer.SafeNormalize(Vector2.Zero) * 0.5f;
            }
            
            // 2. 鱼群行为力（提高权重以增加活跃度）
            totalForce += separationForce * 3.0f * behaviorRandomness;  // 分离力增强
            totalForce += alignmentForce * 1.5f;                         // 对齐力增强
            totalForce += cohesionForce * 1.2f;                          // 聚合力增强
            
            // 3. 随机游动（更频繁的方向改变）
            wanderTimer++;
            if (wanderTimer > Main.rand.Next(15, 30)) { // 更频繁地改变方向
                wanderTimer = 0;
                randomWander = new Vector2(
                    Main.rand.NextFloat(-1.5f, 1.5f),
                    Main.rand.NextFloat(-1.5f, 1.5f)
                ) * behaviorRandomness;
            }
            totalForce += randomWander * 0.6f;
            
            // 4. 朝向光标的整体方向（更强的引导力）
            Vector2 toMouse = (Main.MouseWorld - Projectile.Center).SafeNormalize(Vector2.Zero);
            totalForce += toMouse * 1.0f;
            
            // 5. 跃动效果（周期性的上下波动）
            float jumpTime = (Main.GameUpdateCount + jumpPhaseOffset) * 0.08f;
            float jumpStrength = (float)Math.Sin(jumpTime) * 0.8f * behaviorRandomness;
            Vector2 jumpForce = new Vector2(0, jumpStrength);
            
            // 在跃动的高峰期增加额外的横向速度
            if (Math.Abs(Math.Sin(jumpTime)) > 0.7f) {
                float horizontalBoost = (float)Math.Cos(jumpTime * 2f) * 0.5f;
                jumpForce.X = horizontalBoost * behaviorRandomness;
            }
            
            totalForce += jumpForce;
            
            // 应用力并限制速度（提高最大速度以增加动感）
            Projectile.velocity += totalForce * 0.2f; // 提高力的应用系数
            
            // 正常阶段跟随玩家的部分速度（保持相对位置）
            Projectile.position += OwnerPlayer.velocity * 0.3f;
            
            float maxSpeed = 18f * behaviorRandomness; // 提高最大速度
            float minSpeed = 3f; // 设置最小速度，防止鱼停滞
            
            float currentSpeed = Projectile.velocity.Length();
            if (currentSpeed > maxSpeed) {
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * maxSpeed;
            }
            else if (currentSpeed < minSpeed && currentSpeed > 0.1f) {
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * minSpeed;
            }
        }
        
        /// <summary>
        /// 计算鱼群算法的三个基本力（增强版）
        /// </summary>
        private void CalculateFlockingBehavior() {
            separationForce = Vector2.Zero;
            alignmentForce = Vector2.Zero;
            cohesionForce = Vector2.Zero;
            
            Vector2 avgPosition = Vector2.Zero;
            Vector2 avgVelocity = Vector2.Zero;
            int neighborCount = 0;
            
            float perceptionRadius = 150f; // 增加感知半径
            float separationRadius = 70f;  // 增加分离半径
            
            // 遍历所有同类弹幕
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile other = Main.projectile[i];
                
                if (!other.active || other.type != Projectile.type || other.whoAmI == Projectile.whoAmI) {
                    continue;
                }
                
                float distance = Vector2.Distance(Projectile.Center, other.Center);
                
                if (distance < perceptionRadius) {
                    // 聚合：计算平均位置
                    avgPosition += other.Center;
                    
                    // 对齐：计算平均速度
                    avgVelocity += other.velocity;
                    
                    neighborCount++;
                    
                    // 分离：避免过近（增强分离力）
                    if (distance < separationRadius && distance > 0) {
                        Vector2 away = Projectile.Center - other.Center;
                        away = away.SafeNormalize(Vector2.Zero);
                        // 使用平方反比，距离越近，分离力指数增长
                        float separationStrength = 1f / (distance * distance * 0.01f);
                        away *= separationStrength;
                        separationForce += away;
                    }
                }
            }
            
            if (neighborCount > 0) {
                // 计算聚合力：朝向群体中心
                avgPosition /= neighborCount;
                cohesionForce = (avgPosition - Projectile.Center).SafeNormalize(Vector2.Zero);
                
                // 计算对齐力：匹配群体速度
                avgVelocity /= neighborCount;
                alignmentForce = (avgVelocity - Projectile.velocity).SafeNormalize(Vector2.Zero);
                
                // 如果邻居太多，增加分离力
                if (neighborCount > 5) {
                    separationForce *= 1.5f;
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadItem(ItemID.SpecularFish);//加载关于鱼的纹理
            Texture2D value = TextureAssets.Item[ItemID.SpecularFish].Value;//获取鱼的纹理
            
            // 计算绘制参数
            Vector2 drawPosition = Projectile.Center - Main.screenPosition;
            Rectangle sourceRect = value.Frame(1, 1, 0, 0);
            Vector2 origin = sourceRect.Size() / 2f;
            float drawRotation = Projectile.rotation + (fishDirection > 0 ? MathHelper.PiOver4 : -MathHelper.PiOver4);
            
            // 根据朝向决定翻转
            SpriteEffects effects = fishDirection > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically;
            
            // 判断是否在冲刺阶段（用于增强拖尾效果）
            bool isDashing = lifeTimer <= DashPhase;
            int trailLength = isDashing ? Projectile.oldPos.Length : (Projectile.oldPos.Length * 2 / 3);
            
            // 冲刺阶段螺旋颜色效果
            Color spiralColor = lightColor;
            if (isDashing) {
                // 根据螺旋臂添加不同的颜色调制
                int spiralArm = FishID % 5;
                float colorPhase = (lifeTimer * 0.1f + spiralArm * MathHelper.TwoPi / 5f) % MathHelper.TwoPi;
                Color accentColor = Color.Lerp(
                    Color.Cyan,
                    Color.LightBlue,
                    (float)Math.Sin(colorPhase) * 0.5f + 0.5f
                );
                spiralColor = Color.Lerp(lightColor, accentColor, 0.3f);
            }
            
            // 绘制更明显的拖尾（冲刺时形成螺旋轨迹）
            for (int i = 0; i < trailLength; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                
                // 冲刺阶段拖尾更明显且带有颜色渐变
                float baseTrailAlpha = isDashing ? 0.8f : 0.6f;
                float trailAlpha = fishAlpha * (1f - i / (float)Projectile.oldPos.Length) * baseTrailAlpha;
                Vector2 trailPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                float trailScale = fishScale * (1f - i / (float)Projectile.oldPos.Length * 0.3f);
                
                // 螺旋拖尾颜色渐变
                Color trailColor = isDashing ? 
                    Color.Lerp(spiralColor, lightColor, i / (float)trailLength) : 
                    lightColor;
                
                Main.EntitySpriteDraw(
                    value,
                    trailPos,
                    sourceRect,
                    trailColor * trailAlpha,
                    drawRotation,
                    origin,
                    trailScale * 0.85f,
                    effects,
                    0
                );
            }
            
            // 绘制主体
            Main.EntitySpriteDraw(
                value,
                drawPosition,
                sourceRect,
                spiralColor * fishAlpha,
                drawRotation,
                origin,
                fishScale,
                effects,
                0
            );
            
            // 额外的发光层（速度快时更明显，冲刺阶段更强）
            float glowThreshold = isDashing ? 15f : 12f;
            if (Projectile.velocity.Length() > glowThreshold) {
                float glowAlpha = (Projectile.velocity.Length() - glowThreshold) / 10f * 0.5f * fishAlpha;
                if (isDashing) {
                    glowAlpha *= 1.8f; // 冲刺阶段发光更强
                    
                    // 螺旋发光效果：根据在螺旋中的位置产生不同强度的发光
                    int spiralArm = FishID % 5;
                    float glowPulse = (float)Math.Sin(lifeTimer * 0.2f + spiralArm * MathHelper.TwoPi / 5f);
                    glowAlpha *= 1f + glowPulse * 0.3f;
                }
                
                Color glowColor = isDashing ? Color.Lerp(Color.White, Color.Cyan, 0.4f) : Color.White;
                
                Main.EntitySpriteDraw(
                    value,
                    drawPosition,
                    sourceRect,
                    glowColor * glowAlpha,
                    drawRotation,
                    origin,
                    fishScale * 1.2f,
                    effects,
                    0
                );
            }
            
            // 超高速螺旋涡流特效（仅在冲刺阶段且速度极快时）
            if (isDashing && Projectile.velocity.Length() > 30f) {
                int spiralArm = FishID % 5;
                float vortexAlpha = fishAlpha * 0.2f;
                
                Main.EntitySpriteDraw(
                    value,
                    drawPosition,
                    sourceRect,
                    Color.Cyan * vortexAlpha,
                    drawRotation,
                    origin,
                    fishScale * 1.4f,
                    effects,
                    0
                );
            }
            
            return false;
        }
    }
}
