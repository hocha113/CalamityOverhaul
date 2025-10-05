using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Rarities;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend.Skills;
using CalamityOverhaul.Content.RemakeItems;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend
{
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
            SkillID = CloneFish.ID; // 原有逻辑
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
            if (SkillID == FishSwarm.ID) {
                return true;
            }
            if (SkillID == CloneFish.ID) {
                return true;
            }
            return false;
        }

        public override bool? UseItem(Item item, Player player) {
            if (player.altFunctionUse == 2) {
                if (SkillID == FishSwarm.ID) {
                    FishSwarm.AltUse(item, player);
                    return true;
                }
                if (SkillID == CloneFish.ID) {
                    CloneFish.AltUse(item, player);
                    return true;
                }
            }
            return null;
        }

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {

            var hp = player.GetOverride<HalibutPlayer>();

            if (player.altFunctionUse == 2) {
                return false;//右键不触发普通攻击
            }

            // 普攻时尝试触发闪光技能
            if (SkillID == Sparkling.ID) {
                hp.SparklingUseCounter++;
                Sparkling.TryTriggerSparklingVolley(item, player, hp);
            }

            int bulletAmt = Main.rand.Next(5, 9);
            for (int index = 0; index < bulletAmt; ++index) {
                float SpeedX = velocity.X + Main.rand.Next(-10, 11) * 0.05f;
                float SpeedY = velocity.Y + Main.rand.Next(-10, 11) * 0.05f;
                int shot = Projectile.NewProjectile(source, position.X, position.Y, SpeedX, SpeedY, type, damage, knockback, player.whoAmI);
                Main.projectile[shot].CWR().SpanTypes = (byte)SpanTypesEnum.HalibutCannon;
            }

            // 记录克隆需要的射击事件
            if (hp.CloneFishActive) {
                hp.RegisterShoot(type, velocity, damage, knockback, item.type);
            }

            return false;
        }
    }
}
