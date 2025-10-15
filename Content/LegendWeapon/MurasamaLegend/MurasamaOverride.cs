using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Rarities;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.MurasamaLegend.MurasamaProj;
using CalamityOverhaul.Content.LegendWeapon.MurasamaLegend.UI;
using CalamityOverhaul.Content.RemakeItems;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.MurasamaLegend
{
    /// <summary>
    /// 妖刀
    /// </summary>
    internal class MurasamaOverride : CWRItemOverride
    {
        #region Data
        /// <summary>
        /// 每个时期阶段对应的伤害，这个成员一般不需要直接访问，而是使用<see cref="GetOnDamage"/>
        /// </summary>
        private static Dictionary<int, int> DamageDictionary = new Dictionary<int, int>();
        /// <summary>
        /// 每个时期阶段对应的挥舞范围大小，这个成员一般不需要直接访问，而是使用<see cref="GetOnScale"/>
        /// </summary>
        private static Dictionary<int, float> BladeVolumeRatioDictionary = new Dictionary<int, float>();
        /// <summary>
        /// 每个时期阶段对应的额外暴击增幅的字典，这个成员一般不需要直接访问，而是使用<see cref="GetOnCrit"/>
        /// </summary>
        private static Dictionary<int, int> SetLevelCritDictionary = new Dictionary<int, int>();
        /// <summary>
        /// 每个时期阶段对应的升龙冷却的字典，这个成员一般不需要直接访问，而是使用<see cref="GetOnRDCD"/>
        /// </summary>
        private static Dictionary<int, int> RDCDDictionary = new Dictionary<int, int>();
        /// <summary>
        /// 每个时期对应的击退力度字典，这个成员一般不需要直接访问，而是使用<see cref="GetOnKnockback"/>
        /// </summary>
        private static Dictionary<int, float> KnockbackDictionary = new Dictionary<int, float>();
        /// <summary>
        /// 获取开局的伤害
        /// </summary>
        public static int GetStartDamage => DamageDictionary[0];
        /// <summary>
        /// 获取开局的击退力度
        /// </summary>
        public static float GetStartKnockback => KnockbackDictionary[0];
        [VaultLoaden(CWRConstant.Item_Melee + "MuraItem")]
        public static Asset<Texture2D> MuraItemAsset { get; private set; }
        private static readonly string[] SamNameList = ["激流山姆", "山姆", "Samuel Rodrigues", "Jetstream Sam", "Sam"];
        private static readonly string[] VergilNameList = ["维吉尔", "Vergil"];
        public override int TargetID => ModContent.ItemType<Murasama>();
        #endregion
        /// <summary>
        /// 获取时期对应的伤害
        /// </summary>
        public static int GetOnDamage(Item item) => DamageDictionary[GetLevel(item)];
        /// <summary>
        /// 计算伤害比例
        /// </summary>
        public static float GetSengsDamage(Item item) => GetOnDamage(item) / (float)GetStartDamage;
        /// <summary>
        /// 根据<see cref="GetOnDamage"/>获取一个与<see cref="TrueMeleeDamageClass"/>相关的乘算伤害
        /// </summary>
        public static int ActualTrueMeleeDamage(Item item) => (int)(GetOnDamage(item) * Main.LocalPlayer.GetDamage<TrueMeleeDamageClass>().Additive);
        /// <summary>
        /// 获取时期对应的范围增幅
        /// </summary>
        public static float GetOnScale(Item item) => BladeVolumeRatioDictionary[GetLevel(item)];
        /// <summary>
        /// 获取时期对应的额外暴击
        /// </summary>
        public static int GetOnCrit(Item item) => SetLevelCritDictionary[GetLevel(item)];
        /// <summary>
        /// 获取时期对应的冷却时间上限
        /// </summary>
        public static int GetOnRDCD(Item item) => RDCDDictionary[GetLevel(item)];
        /// <summary>
        /// 获取时期对应的击退力度
        /// </summary>
        public static float GetOnKnockback(Item item) => KnockbackDictionary[GetLevel(item)];
        /// <summary>
        /// 是否解锁升龙斩
        /// </summary>
        public static bool UnlockSkill1(Item item) => GetLevel(item) >= 2;
        /// <summary>
        /// 是否解锁下砸
        /// </summary>
        public static bool UnlockSkill2(Item item) => GetLevel(item) >= 5;
        /// <summary>
        /// 是否解锁终结技
        /// </summary>
        public static bool UnlockSkill3(Item item) => GetLevel(item) >= 9;
        /// <summary>
        /// 获得成长等级
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public static int GetLevel(Item item) {
            if (item.type != ModContent.ItemType<Murasama>()) {
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
        public static bool NameIsSam(Player player) => SamNameList.Contains(player.name);
        public static bool NameIsVergil(Player player) => VergilNameList.Contains(player.name);
        public static void LoadWeaponData() {
            DamageDictionary = new Dictionary<int, int>(){
                {0, 12 },
                {1, 18 },
                {2, 25 },
                {3, 35 },
                {4, 45 },
                {5, 100 },
                {6, 130 },
                {7, 165 },
                {8, 280 },
                {9, 450 },
                {10, 650 },
                {11, 1350 },
                {12, 1900 },
                {13, 3001 },
                {14, 6002 }
            };
            BladeVolumeRatioDictionary = new Dictionary<int, float>(){
                {0, 0.6f },
                {1, 0.65f },
                {2, 0.7f },
                {3, 0.75f },
                {4, 0.8f },
                {5, 0.85f },
                {6, 0.95f },
                {7, 1f },
                {8, 1.1f },
                {9, 1.2f },
                {10, 1.3f },
                {11, 1.35f },
                {12, 1.4f },
                {13, 1.45f },
                {14, 1.5f }
            };
            SetLevelCritDictionary = new Dictionary<int, int>(){
                {0, 2 },
                {1, 5 },
                {2, 8 },
                {3, 10 },
                {4, 12 },
                {5, 15 },
                {6, 18 },
                {7, 20 },
                {8, 22 },
                {9, 24 },
                {10, 28 },
                {11, 32 },
                {12, 36 },
                {13, 40 },
                {14, 46 }
            };
            RDCDDictionary = new Dictionary<int, int>(){
                {0, 400 },
                {1, 380 },
                {2, 360 },
                {3, 360 },
                {4, 340 },
                {5, 320 },
                {6, 300 },
                {7, 280 },
                {8, 260 },
                {9, 260 },
                {10, 240 },
                {11, 220 },
                {12, 200 },
                {13, 180 },
                {14, 160 }
            };
            KnockbackDictionary = new Dictionary<int, float>(){
                {0, 1.6f },
                {1, 1.85f },
                {2, 2.1f },
                {3, 2.45f },
                {4, 2.8f },
                {5, 3.15f },
                {6, 3.9f },
                {7, 4.2f },
                {8, 4.4f },
                {9, 5.1f },
                {10, 5.3f },
                {11, 5.65f },
                {12, 5.8f },
                {13, 6.2f },
                {14, 6.5f }
            };
        }
        public override void SetStaticDefaults() {
            Main.RegisterItemAnimation(TargetID, new DrawAnimationVertical(5, 13));
            ItemID.Sets.AnimatesAsSoul[TargetID] = true;
        }

        public override void SetDefaults(Item item) => SetDefaultsFunc(item);

        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips) => TooltipHandler.SetTooltip(item, ref tooltips);

        public override void ModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => DataHandler.DamageModify(item, player, ref damage);

        public override void ModifyWeaponKnockback(Item item, Player player, ref StatModifier knockback)
            => CWRUtils.ModifyLegendWeaponKnockbackFunc(item, GetOnKnockback(item), GetStartKnockback, ref knockback);

        public override bool? On_ModifyWeaponCrit(Item item, Player player, ref float crit) {
            crit += GetOnCrit(item);
            return false;
        }

        public override bool? On_PreDrawInInventory(Item item, SpriteBatch spriteBatch, Vector2 position
            , Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale)
            => PreDrawInInventoryFunc(item, spriteBatch, position, frame, origin, scale);

        public override bool? On_CanUseItem(Item item, Player player) => CanUseItemFunc(player, item);

        public static void SetDefaultsFunc(Item Item) {
            LoadWeaponData();
            Item.height = 134;
            Item.width = 90;
            Item.damage = GetStartDamage;
            Item.DamageType = ModContent.GetInstance<TrueMeleeNoSpeedDamageClass>();
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.channel = true;
            Item.useAnimation = 25;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.useTime = 5;
            Item.knockBack = GetStartKnockback;
            Item.autoReuse = false;
            Item.value = CalamityGlobalItem.RarityDarkBlueBuyPrice;
            Item.shoot = ModContent.ProjectileType<MuraSlashDefault>();
            Item.shootSpeed = 24f;
            Item.rare = ModContent.RarityType<Violet>();
            Item.CWR().isHeldItem = true;
            Item.CWR().heldProjType = ModContent.ProjectileType<MurasamaHeld>();
            Item.CWR().LegendData = new MuraData();
            ItemMeleePrefixDic[Item.type] = true;
            ItemRangedPrefixDic[Item.type] = false;
        }

        public static bool PreDrawInInventoryFunc(Item item, SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Vector2 origin, float scale) {
            if (Main.gameMenu || !item.Alives()) {
                return true;
            }

            if (item.Alives() && item.CWR().DyeItemID > 0) {
                item.BeginDyeEffectForUI(item.CWR().DyeItemID);
            }

            if (Main.LocalPlayer.CWR().HeldMurasamaBool && item == Main.LocalPlayer.GetItem()) {
                if (MuraChargeUI.MuraUIStyle == MuraChargeUI.MuraUIStyleEnum.conceal) {
                    item.Initialize();
                    float charge = item.CWR().ai[0];
                    if (charge > 0) {
                        Texture2D barBG = CWRAsset.GenericBarBack.Value;
                        Texture2D barFG = CWRAsset.GenericBarFront.Value;
                        float barScale = 3f;
                        Vector2 barOrigin = barBG.Size() * 0.5f;
                        float yOffset = 50f;
                        Vector2 drawPos = position + Vector2.UnitY * scale * (frame.Height - yOffset);
                        Rectangle frameCrop = new Rectangle(0, 0, (int)(charge / 10f * barFG.Width), barFG.Height);
                        Color color = Main.hslToRgb(Main.GlobalTimeWrappedHourly * 0.6f % 1f, 1f, 0.75f + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3f) * 0.1f);
                        spriteBatch.Draw(barBG, drawPos, null, color, 0f, barOrigin, scale * barScale, 0, 0f);
                        spriteBatch.Draw(barFG, drawPos, frameCrop, color * 0.8f, 0f, barOrigin, scale * barScale, 0, 0f);
                    }
                }
                if (Main.LocalPlayer.PressKey()) {
                    return true;
                }
            }

            spriteBatch.Draw(MuraItemAsset.Value, position, null, Color.White, 0f, origin, scale, SpriteEffects.None, 0);
            return false;
        }

        public static bool CanUseItemFunc(Player player, Item Item) {
            //在升龙斩或者爆发弹幕存在时不能使用武器
            return player.ownedProjectileCounts[ModContent.ProjectileType<MuraBreakerSlash>()] > 0
                || player.ownedProjectileCounts[ModContent.ProjectileType<MuraTriggerDash>()] > 0
                || player.PressKey(false)
                ? false
                : (CWRServerConfig.Instance.WeaponEnhancementSystem || InWorldBossPhase.Level11)
                && player.ownedProjectileCounts[Item.shoot] == 0;
        }
    }
}
