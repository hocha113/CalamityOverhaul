using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Rarities;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.HalibutLegend;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class HalibutCannonEcType : EctypeItem
    {
        #region Data
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "HalibutCannon";
        /// <summary>
        /// 每个时期阶段对应的伤害，这个成员一般不需要直接访问，而是使用<see cref="GetOnDamage"/>
        /// </summary>
        private static Dictionary<int, int> DamageDictionary = new Dictionary<int, int>();
        /// <summary>
        /// 每个时期阶段对应的额外暴击振幅的字典，这个成员一般不需要直接访问，而是使用<see cref="GetOnCrit"/>
        /// </summary>
        private static Dictionary<int, int> SetLevelCritDictionary = new Dictionary<int, int>();
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
        #endregion
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
                {10, 25 },
                {11, 28 },
                {12, 32 },
                {13, 36 },
                {14, 40 }
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
        public override void SetStaticDefaults() => LoadWeaponData();
        public override void SetDefaults() => SetDefaultsFunc(Item);
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
            Item.CWR().hasHeldNoCanUseBool = true;
            Item.CWR().heldProjType = ModContent.ProjectileType<HalibutCannonHeldProj>();
        }

        public override void ModifyWeaponCrit(Player player, ref float crit) => crit += GetOnCrit;

        public override void ModifyWeaponDamage(Player player, ref StatModifier damage)
            => CWRUtils.ModifyLegendWeaponDamageFunc(player, Item, GetOnDamage, GetStartDamage, ref damage);

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            TooltipLine legendtops = tooltips.FirstOrDefault((TooltipLine x) => x.Text.Contains("[Text]") && x.Mod == "Terraria");
            if (legendtops != null) {
                int index = InWorldBossPhase.Instance.Halibut_Level();
                legendtops.Text = index >= 0 && index <= 14 ? CWRLocText.GetTextValue($"Halibut_TextDictionary_Content_{index}") : "ERROR";

                if (!CWRServerConfig.Instance.WeaponEnhancementSystem) {
                    legendtops.Text = InWorldBossPhase.Instance.level11 ? CWRLocText.GetTextValue("Halibut_No_legend_Content_2")
                        : CWRLocText.GetTextValue("Halibut_No_legend_Content_1");
                }
                legendtops.OverrideColor = Color.Lerp(Color.BlueViolet, Color.White, 0.5f + (float)Math.Sin(Main.GlobalTimeWrappedHourly) * 0.5f);
            }
            SetTooltip(ref tooltips);
        }

        public static void SetTooltip(ref List<TooltipLine> tooltips, string modName = "Terraria") {
            if (CWRServerConfig.Instance.WeaponEnhancementSystem) {
                int level = InWorldBossPhase.Instance.Halibut_Level();
                string num = (level + 1).ToString();
                if (level == 14) {
                    num = CWRLocText.GetTextValue("Murasama_Text_Lang_End");
                }
                tooltips.ReplaceTooltip("[Lang4]", $"[c/00736d:{CWRLocText.GetTextValue("Murasama_Text_Lang_0") + " "}{num}]", modName);
                tooltips.ReplaceTooltip("legend_Text", CWRLocText.GetTextValue("Halibut_No_legend_Content_3"), modName);
            }
            else {
                tooltips.ReplaceTooltip("[Lang4]", "", modName);
                tooltips.ReplaceTooltip("legend_Text", CWRLocText.GetTextValue("Halibut_No_legend_Content_4"), modName);
            }
        }
    }
}
