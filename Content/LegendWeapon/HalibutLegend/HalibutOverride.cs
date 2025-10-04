using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Rarities;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend.Skills;
using CalamityOverhaul.Content.RemakeItems;
using InnoVault.GameSystem;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Graphics;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend
{
    internal class HalibutPlayer : PlayerOverride//这个类用于存储一些与玩家相关的额外数据
    {
        /// <summary>闪光技能：当前齐射是否激活</summary>
        public bool SparklingVolleyActive { get; set; }
        /// <summary>闪光技能：齐射冷却计时（帧）</summary>
        public int SparklingVolleyCooldown { get; set; }
        /// <summary>闪光技能：当前正在齐射的唯一ID</summary>
        public int SparklingVolleyId { get; set; } = -1;
        /// <summary>闪光技能：齐射内部计时</summary>
        public int SparklingVolleyTimer { get; set; }
        /// <summary>闪光技能：武器普通攻击使用计数</summary>
        public int SparklingUseCounter { get; set; }
        /// <summary>闪光技能：基础冷却（帧）</summary>
        public const int SparklingBaseCooldown = 360; // 6秒
        /// <summary>闪光技能：鱼数量</summary>
        public int SparklingFishCount { get; set; }
        /// <summary>闪光技能：下一条鱼开火索引</summary>
        public int SparklingNextFireIndex { get; set; }
        
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
        
        /// <summary>
        /// 螺旋尖锥突袭状态
        /// </summary>
        public bool FishConeSurgeActive { get; set; }
        
        /// <summary>
        /// 突袭后攻击后摇计时器
        /// </summary>
        public int AttackRecoveryTimer { get; set; }
        
        /// <summary>
        /// 攻击后摇持续时间（60帧 = 1秒）
        /// </summary>
        public const int AttackRecoveryDuration = 60;

        public override void ResetEffects() {//用于每帧恢复数据
            
        }

        public override void PostUpdate() {//在每帧更新后进行一些操作
            // 闪光技能冷却与齐射推进
            if (SparklingVolleyCooldown > 0) SparklingVolleyCooldown--;
            if (SparklingVolleyActive) {
                SparklingVolleyTimer++;
            }

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
            
            // 更新攻击后摇
            if (AttackRecoveryTimer > 0) {
                AttackRecoveryTimer--;
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
        /// <summary>
        /// 技能ID
        /// </summary>
        public static int SkillID;
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
            if (SkillID == FishSwarm.ID) {
                HalibutPlayer halibutPlayer = player.GetOverride<HalibutPlayer>();

                // 检查是否在攻击后摇中
                if (halibutPlayer.AttackRecoveryTimer > 0) {
                    return false; // 禁止攻击
                }

                item.UseSound = SoundID.Item38;
                if (player.altFunctionUse == 2) {
                    item.UseSound = null;
                }
                else {
                    // === 移形换影中左键触发螺旋尖锥突袭 ===
                    if (halibutPlayer.FishSwarmActive) {
                        Vector2 velocity = player.To(Main.MouseWorld).UnitVector();
                        FishSwarm.ActivateFishConeSurge(item, player, velocity * 6);
                        player.velocity += velocity * 6;
                        return false; //不发射普通弹幕
                    }
                }
            }
            
            return true;
        }

        public override bool? AltFunctionUse(Item item, Player player) {
            return true;
        }

        public override bool? UseItem(Item item, Player player) {
            if (player.altFunctionUse == 2) {
                if (SkillID == FishSwarm.ID) {
                    FishSwarm.AltUse(item, player);
                    return false;
                }
                
            }
            return null;
        }

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {

            if (player.altFunctionUse == 2) {
                return false;//右键不触发普通攻击
            }

            // 普攻时尝试触发闪光技能
            if (SkillID == Sparkling.ID) {
                var hp = player.GetOverride<HalibutPlayer>();
                hp.SparklingUseCounter++;
                TryTriggerSparklingVolley(item, player, hp);
            }

            return null;
        }

        private static int _sparklingVolleyIdSeed = 0;
        private static void TryTriggerSparklingVolley(Item item, Player player, HalibutPlayer hp) {
            if (hp.SparklingVolleyActive) return;
            if (hp.SparklingVolleyCooldown > 0) return;
            // 需求：周期性触发，可依据使用计数，也可随机微调
            if (hp.SparklingUseCounter < 6) return; // 至少连续普通攻击6次后触发（可调）
            hp.SparklingUseCounter = 0;

            // 激活齐射
            hp.SparklingVolleyActive = true;
            hp.SparklingVolleyTimer = 0;
            hp.SparklingFishCount = Main.rand.Next(6, 9); // 6~8条鱼
            hp.SparklingNextFireIndex = 0;
            hp.SparklingVolleyId = _sparklingVolleyIdSeed++;
            hp.SparklingVolleyCooldown = HalibutPlayer.SparklingBaseCooldown; // 设置基础冷却

            Vector2 aimDir = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.UnitX);
            Vector2 behind = (-aimDir).SafeNormalize(Vector2.UnitX);
            float arc = MathHelper.ToRadians(70f); // 扇形总角度
            float radius = 90f;
            for (int i = 0; i < hp.SparklingFishCount; i++) {
                float t = (hp.SparklingFishCount == 1) ? 0.5f : i / (float)(hp.SparklingFishCount - 1);
                float angOff = (t - 0.5f) * arc;
                Vector2 offsetDir = behind.RotatedBy(angOff);
                Vector2 spawnPos = player.Center + offsetDir * radius + new Vector2(0, (float)Math.Sin(Main.GameUpdateCount * 0.05f + i) * 6f);
                int proj = Projectile.NewProjectile(player.GetSource_ItemUse(item), spawnPos, Vector2.Zero,
                    ModContent.ProjectileType<SparklingFishHolder>(), 0, 0f, player.whoAmI,
                    ai0: hp.SparklingVolleyId, ai1: i);
                if (Main.projectile[proj].ModProjectile is SparklingFishHolder holder) holder.Owner = player;
            }
            SoundEngine.PlaySound(SoundID.Item92 with { Pitch = -0.4f }, player.Center); // 预热音
        }
    }
}
