using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Rarities;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class HalibutCannonEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "HalibutCannon";

        /// <summary>
        /// 每个时期阶段对应的伤害，这个成员一般不需要直接访问，而是使用<see cref="GetOnDamage"/>
        /// </summary>
        private static Dictionary<int, int> DamageDictionary => new Dictionary<int, int>(){
            {0, 3 },
            {1, 4 },
            {2, 5 },
            {3, 7 },
            {4, 10 },
            {5, 15 },
            {6, 20 },
            {7, 25 },
            {8, 30 },
            {9, 35 },
            {10, 42 },
            {11, 46 },
            {12, 50 },
            {13, 55 },
            {14, 70 }
        };

        /// <summary>
        /// 每个时期阶段对应的额外暴击振幅的字典，这个成员一般不需要直接访问，而是使用<see cref="GetOnCrit"/>
        /// </summary>
        private static Dictionary<int, int> SetLevelCritDictionary => new Dictionary<int, int>(){
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
        public static int Level => InWorldBossPhase.Instance.Halibut_Level();
        /// <summary>
        /// 获取开局的伤害
        /// </summary>
        public static int GetStartDamage => DamageDictionary[0];
        /// <summary>
        /// 获取时期对应的伤害
        /// </summary>
        public static int GetOnDamage => DamageDictionary[Level];
        /// <summary>
        /// 计算伤害比例
        /// </summary>
        public static float GetSengsDamage => GetOnDamage / (float)GetStartDamage;
        /// <summary>
        /// 根据<see cref="GetOnDamage"/>获取一个与<see cref="RangedDamageClass"/>相关的乘算伤害
        /// </summary>
        public static int ActualTrueMeleeDamage => (int)(GetOnDamage * Main.LocalPlayer.GetDamage<RangedDamageClass>().Additive);
        /// <summary>
        /// 获取时期对应的额外暴击
        /// </summary>
        public static int GetOnCrit => SetLevelCritDictionary[Level];
        public override void SetDefaults() {
            Item.damage = 3;
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
            Item.CWR().hasHeldNoCanUseBool = true;
            Item.CWR().heldProjType = ModContent.ProjectileType<HalibutCannonHeldProj>();
        }

        public override void ModifyWeaponCrit(Player player, ref float crit) => crit += GetOnCrit;

        public override void ModifyWeaponDamage(Player player, ref StatModifier damage) {
            damage *= GetOnDamage / (float)GetStartDamage;
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips) {

        }
    }
}
