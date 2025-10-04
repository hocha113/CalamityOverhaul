using CalamityMod;
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
                    FishSwarmCooldown = FishSwarmMaxCooldown;
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
            return true;
        }

        public override bool? AltFunctionUse(Item item, Player player) {
            return true;
        }

        public override bool? UseItem(Item item, Player player) {
            if (player.altFunctionUse == 2) {
                AltUse(item, player);
                return true;
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

        public void AltUse(Item item, Player player) {//额外的的独立封装，玩家右键使用时调用，用于触发技能
            HalibutPlayer halibutPlayer = player.GetOverride<HalibutPlayer>();
            
            // 检查技能是否在冷却中
            if (halibutPlayer.FishSwarmCooldown > 0) {
                //return;
            }
            
            // 激活技能
            halibutPlayer.FishSwarmActive = true;
            halibutPlayer.FishSwarmTimer = 0;
            
            // 计算冲刺方向（朝向光标）
            Vector2 dashDirection = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.Zero);
            
            // 冲刺加速
            float dashSpeed = 25f;
            player.velocity = dashDirection * dashSpeed;
            
            // 生成鱼群（15-20条鱼）
            int fishCount = Main.rand.Next(15, 21);
            for (int i = 0; i < fishCount; i++) {
                // 在玩家周围随机位置生成鱼
                float angle = MathHelper.TwoPi * i / fishCount + Main.rand.NextFloat(-0.3f, 0.3f);
                float distance = Main.rand.NextFloat(40f, 100f);
                Vector2 spawnOffset = new Vector2(
                    (float)Math.Cos(angle) * distance,
                    (float)Math.Sin(angle) * distance
                );
                
                Vector2 spawnPos = player.Center + spawnOffset;
                Vector2 initialVelocity = dashDirection * Main.rand.NextFloat(8f, 15f);
                
                int proj = Projectile.NewProjectile(
                    player.GetSource_ItemUse(item),
                    spawnPos,
                    initialVelocity,
                    ModContent.ProjectileType<FishingFly>(),
                    0, // 不造成伤害
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

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8;
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
                // 淡入效果
                if (fishAlpha < 1f) {
                    fishAlpha += 0.1f;
                }
            }
            
            // 初始化鱼的缩放（只在第一帧）
            if (Projectile.timeLeft == HalibutPlayer.FishSwarmDuration + 60) {
                fishScale = Main.rand.NextFloat(0.7f, 1.2f);
            }
            
            // 玩家跟随技能时的移动
            if (halibutPlayer.FishSwarmActive) {
                Vector2 targetVelocity = (Main.MouseWorld - OwnerPlayer.Center).SafeNormalize(Vector2.Zero) * 12f;
                OwnerPlayer.velocity = Vector2.Lerp(OwnerPlayer.velocity, targetVelocity, 0.1f);
            }
            
            // === 鱼群算法实现 ===
            CalculateFlockingBehavior();
            
            // 应用鱼群行为力
            Vector2 totalForce = Vector2.Zero;
            
            // 1. 跟随玩家的吸引力（最强）
            Vector2 toPlayer = OwnerPlayer.Center - Projectile.Center;
            float distanceToPlayer = toPlayer.Length();
            
            if (distanceToPlayer > 150f) {
                // 如果离玩家太远，强制拉回
                totalForce += toPlayer.SafeNormalize(Vector2.Zero) * 2.5f;
            }
            else if (distanceToPlayer > 50f) {
                // 保持在合理范围内
                totalForce += toPlayer.SafeNormalize(Vector2.Zero) * 0.8f;
            }
            
            // 2. 鱼群行为力（权重调整）
            totalForce += separationForce * 2.0f;  // 分离最重要，避免重叠
            totalForce += alignmentForce * 0.8f;   // 对齐次之
            totalForce += cohesionForce * 0.6f;    // 聚合最弱
            
            // 3. 随机游动（增加自然感）
            wanderTimer++;
            if (wanderTimer > 30) {
                wanderTimer = 0;
                randomWander = new Vector2(
                    Main.rand.NextFloat(-1f, 1f),
                    Main.rand.NextFloat(-1f, 1f)
                );
            }
            totalForce += randomWander * 0.3f;
            
            // 4. 朝向光标的整体方向
            if (halibutPlayer.FishSwarmActive) {
                Vector2 toMouse = (Main.MouseWorld - Projectile.Center).SafeNormalize(Vector2.Zero);
                totalForce += toMouse * 0.5f;
            }
            
            // 应用力并限制速度
            Projectile.velocity += totalForce * 0.15f;
            
            float maxSpeed = 15f;
            if (Projectile.velocity.Length() > maxSpeed) {
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * maxSpeed;
            }
            
            // 更新朝向和旋转
            if (Projectile.velocity.X != 0) {
                fishDirection = Projectile.velocity.X > 0 ? 1 : -1;
            }
            
            // 根据速度方向计算旋转角度
            if (Projectile.velocity.LengthSquared() > 0.1f) {
                float targetRotation = Projectile.velocity.ToRotation();
                fishRotation = MathHelper.Lerp(fishRotation, targetRotation, 0.2f);
            }
            
            // 模拟游动的波动效果
            Projectile.rotation = fishRotation + (float)Math.Sin(Main.GameUpdateCount * 0.1f + FishID) * 0.15f;
        }
        
        /// <summary>
        /// 计算鱼群算法的三个基本力
        /// </summary>
        private void CalculateFlockingBehavior() {
            separationForce = Vector2.Zero;
            alignmentForce = Vector2.Zero;
            cohesionForce = Vector2.Zero;
            
            Vector2 avgPosition = Vector2.Zero;
            Vector2 avgVelocity = Vector2.Zero;
            int neighborCount = 0;
            
            float perceptionRadius = 120f; // 感知半径
            float separationRadius = 60f;  // 分离半径
            
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
                    
                    // 分离：避免过近
                    if (distance < separationRadius && distance > 0) {
                        Vector2 away = Projectile.Center - other.Center;
                        away = away.SafeNormalize(Vector2.Zero);
                        // 距离越近，分离力越强
                        away /= distance;
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
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadItem(ItemID.Fish);//加载关于鱼的纹理
            Texture2D value = TextureAssets.Item[ItemID.Fish].Value;//获取鱼的纹理
            
            // 计算绘制参数
            Vector2 drawPosition = Projectile.Center - Main.screenPosition;
            Rectangle sourceRect = value.Frame(1, 1, 0, 0);
            Vector2 origin = sourceRect.Size() / 2f;
            float drawRotation = Projectile.rotation + (fishDirection > 0 ? MathHelper.PiOver4 : -MathHelper.PiOver4);
            
            // 根据朝向决定翻转
            SpriteEffects effects = fishDirection > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically;
            
            // 绘制半透明拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                
                float trailAlpha = fishAlpha * (1f - i / (float)Projectile.oldPos.Length) * 0.4f;
                Vector2 trailPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                
                Main.EntitySpriteDraw(
                    value,
                    trailPos,
                    sourceRect,
                    lightColor * trailAlpha,
                    drawRotation,
                    origin,
                    fishScale * 0.8f,
                    effects,
                    0
                );
            }
            
            // 绘制主体
            Main.EntitySpriteDraw(
                value,
                drawPosition,
                sourceRect,
                lightColor * fishAlpha,
                drawRotation,
                origin,
                fishScale,
                effects,
                0
            );
            
            return false;
        }
    }
}
