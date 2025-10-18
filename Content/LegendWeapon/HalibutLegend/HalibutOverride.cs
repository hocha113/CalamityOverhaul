using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Rarities;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend.DomainSkills;
using CalamityOverhaul.Content.RemakeItems;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend
{
    internal class HalibutOverride : CWRItemOverride
    {
        #region Data
        /// <summary>
        /// 目标ID
        /// </summary>
        public static int ID => ModContent.ItemType<HalibutCannon>();
        /// <summary>
        /// 目标ID
        /// </summary>
        public override int TargetID => ID;
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
        /// 获取时期对应的伤害
        /// </summary>
        public static int GetOnDamage(Item item) => DamageDictionary[HalibutData.GetLevel(item)];
        /// <summary>
        /// 计算伤害比例
        /// </summary>
        public static float GetSengsDamage(Item item) => GetOnDamage(item) / (float)GetStartDamage;
        /// <summary>
        /// 根据<see cref="GetOnDamage"/>获取一个与<see cref="RangedDamageClass"/>相关的乘算伤害
        /// </summary>
        public static int ActualRangedDamage(Item item) => (int)(GetOnDamage(item) * Main.LocalPlayer.GetDamage<RangedDamageClass>().Additive);
        /// <summary>
        /// 获取时期对应的额外暴击
        /// </summary>
        public static int GetOnCrit(Item item) => SetLevelCritDictionary[HalibutData.GetLevel(item)];

        public static void LoadWeaponData() {
            DamageDictionary = new Dictionary<int, int>(){
                {0, 3 },
                {1, 5 },
                {2, 7 },
                {3, 9 },
                {4, 12 },
                {5, 15 },
                {6, 18 },
                {7, 23 },
                {8, 35 },
                {9, 45 },
                {10, 65 },
                {11, 80 },
                {12, 110 },
                {13, 170 },
                {14, 280 }
            };
            SetLevelCritDictionary = new Dictionary<int, int>(){
                {0, 0 },
                {1, 1 },
                {2, 1 },
                {3, 2 },
                {4, 2 },
                {5, 5 },
                {6, 6 },
                {7, 7 },
                {8, 8 },
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
            Item.useAnimation = 10;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.rare = ModContent.RarityType<HotPink>();
            Item.noMelee = true;
            Item.knockBack = 1f;
            Item.value = CalamityGlobalItem.RarityHotPinkBuyPrice;
            Item.UseSound = SoundID.Item38 with { Volume = 0.6f };
            Item.autoReuse = true;
            Item.shoot = ProjectileID.Bullet;
            Item.shootSpeed = 12f;
            Item.useAmmo = AmmoID.Bullet;
            Item.Calamity().canFirePointBlankShots = true;
            Item.CWR().LegendData = new HalibutData();
        }

        public override bool? CanUseItem(Item item, Player player) {
            item.UseSound = SoundID.Item38 with { Volume = 0.6f };
            if (FishSkill.IDToInstance.TryGetValue(player.GetOverride<HalibutPlayer>().SkillID, out var fishSkill)) {
                bool? result = fishSkill.CanUseItem(item, player);
                if (result.HasValue) {
                    return result.Value;
                }
            }
            if (player.CountProjectilesOfID<SuperpositionProj>() > 0) {
                return false;
            }
            return true;
        }

        public override bool? AltFunctionUse(Item item, Player player) {
            if (FishSkill.IDToInstance.TryGetValue(player.GetOverride<HalibutPlayer>().SkillID, out var fishSkill)) {
                bool? result = fishSkill.AltFunctionUse(item, player);
                if (result.HasValue) {
                    return result.Value;
                }
            }
            return false;
        }

        public override bool? UseItem(Item item, Player player) {
            if (FishSkill.IDToInstance.TryGetValue(player.GetOverride<HalibutPlayer>().SkillID, out var fishSkill)) {
                bool? result = fishSkill.UseItem(item, player);
                if (result.HasValue) {
                    return result.Value;
                }
            }
            return null;
        }

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            bool isBullet = false;
            if (type == ProjectileID.Bullet) {
                isBullet = true;
                type = ModContent.ProjectileType<OceanCurrent>();
            }

            var hp = player.GetOverride<HalibutPlayer>();
            //记录克隆需要的射击事件
            if (hp.CloneFishActive) {
                hp.RegisterShoot(type, velocity, damage, knockback, item.type);
            }

            if (FishSkill.IDToInstance.TryGetValue(player.GetOverride<HalibutPlayer>().SkillID, out var fishSkill)) {
                bool? result = fishSkill.ShootAlt(item, player, source, position, velocity, type, damage, knockback);
                if (result.HasValue) {
                    return result.Value;
                }
            }

            if (player.altFunctionUse == 2) {
                return false;//右键不触发普通攻击
            }

            if (fishSkill != null) {
                bool? result = fishSkill.Shoot(item, player, source, position, velocity, type, damage, knockback);
                if (result.HasValue) {
                    return result.Value;
                }
            }

            int bulletAmt = Main.rand.Next((1 + HalibutData.GetLevel() / 2), (1 + HalibutData.GetLevel()));
            if (isBullet) {
                damage = (int)(damage * (1.2 + (bulletAmt - 1) * (1 - 0.4/Main.LocalPlayer.GetDamage<RangedDamageClass>().Additive)) * (1 + HalibutData.GetLevel() / 35f));
                bulletAmt = 1;
            }

            for (int index = 0; index < bulletAmt; ++index) {
                float SpeedX = velocity.X + Main.rand.Next(-10, 11) * 0.05f;
                float SpeedY = velocity.Y + Main.rand.Next(-10, 11) * 0.05f;
                Projectile.NewProjectile(source, position.X, position.Y, SpeedX, SpeedY, type, damage, knockback, player.whoAmI);
            }

            return false;
        }
    }
}
