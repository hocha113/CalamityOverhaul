using CalamityOverhaul.Content.LegendWeapon.MurasamaLegend.MurasamaProj;
using CalamityOverhaul.Content.LegendWeapon.MurasamaLegend.UI;
using InnoVault.GameSystem;
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
    internal class MurasamaOverride : ItemOverride
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
        public static int ID => CWRID.Item_Murasama;
        public override int TargetID => ID;
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
        public static int ActualTrueMeleeDamage(Item item) => (int)(Main.LocalPlayer.GetTotalDamage(CWRRef.GetTrueMeleeDamageClass()).ApplyTo(GetOnDamage(item)));
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
        public static bool UnlockSkill1(Item item) => GetLevel(item) >= 3;
        /// <summary>
        /// 是否解锁下砸
        /// </summary>
        public static bool UnlockSkill2(Item item) => GetLevel(item) >= 8;
        /// <summary>
        /// 是否解锁终结技
        /// </summary>
        public static bool UnlockSkill3(Item item) => GetLevel(item) >= 21;
        /// <summary>
        /// 获得成长等级
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public static int GetLevel(Item item) {
            if (item.type != CWRID.Item_Murasama) {
                return 0;
            }
            CWRItem cwrItem = item.CWR();
            if (cwrItem == null) {
                return 0;
            }
            if (cwrItem.LegendData == null) {
                return 0;
            }

            return cwrItem.LegendData.Level;
        }
        public static bool NameIsSam(Player player) => SamNameList.Contains(player.name);
        public static bool NameIsVergil(Player player) => VergilNameList.Contains(player.name);
        public static void LoadWeaponData() {
            DamageDictionary = new Dictionary<int, int>(){
                {0, 12 },
                {1, 14 },
                {2, 16 },
                {3, 22 },
                {4, 28 },
                {5, 37 },
                {6, 44 },
                {7, 51 },
                {8, 85 },//进入hardmode
                {9, 88 },
                {10, 91 },
                {11, 95 },
                {12, 98 },
                {13, 100 },
                {14, 105 },
                {15, 115 },
                {16, 125 },
                {17, 135 },
                {18, 140 },
                {19, 145 },
                {20, 150 },
                {21, 210 },   //月球领主后
                {22, 460 },
                {23, 540 },
                {24, 1350 },
                {25, 1900 },
                {26, 2250 },
                {27, 3001 },
                {28, 6002 }
            };
            BladeVolumeRatioDictionary = new Dictionary<int, float>(){
                {0, 0.6f },
                {1, 0.62f },
                {2, 0.64f },
                {3, 0.66f },
                {4, 0.68f },
                {5, 0.70f },
                {6, 0.72f },
                {7, 0.75f },
                {8, 0.78f },
                {9, 0.81f },
                {10, 0.85f },
                {11, 0.89f },
                {12, 0.92f },
                {13, 0.95f },
                {14, 0.98f },
                {15, 1.00f },
                {16, 1.03f },
                {17, 1.07f },
                {18, 1.10f },
                {19, 1.15f },
                {20, 1.20f },
                {21, 1.25f },
                {22, 1.30f },
                {23, 1.33f },
                {24, 1.36f },
                {25, 1.40f },
                {26, 1.44f },
                {27, 1.47f },
                {28, 1.50f }
            };
            SetLevelCritDictionary = new Dictionary<int, int>(){
                {0, 10 },
                {1, 11 },
                {2, 12 },
                {3, 13 },
                {4, 15 },
                {5, 17 },
                {6, 19 },
                {7, 21 },
                {8, 23 },
                {9, 25 },
                {10, 27 },
                {11, 29 },
                {12, 31 },
                {13, 33 },
                {14, 35 },
                {15, 37 },
                {16, 39 },
                {17, 41 },
                {18, 43 },
                {19, 46 },
                {20, 49 },
                {21, 52 },
                {22, 54 },
                {23, 56 },
                {24, 58 },
                {25, 59 },
                {26, 60 },
                {27, 61 },
                {28, 61 }
            };
            RDCDDictionary = new Dictionary<int, int>(){
                {0, 400 },
                {1, 394 },
                {2, 388 },
                {3, 382 },
                {4, 376 },
                {5, 370 },
                {6, 364 },
                {7, 356 },
                {8, 340 },
                {9, 324 },
                {10, 308 },
                {11, 292 },
                {12, 276 },
                {13, 260 },
                {14, 244 },
                {15, 228 },
                {16, 212 },
                {17, 200 },
                {18, 188 },
                {19, 180 },
                {20, 176 },
                {21, 172 },
                {22, 168 },
                {23, 164 },
                {24, 160 },
                {25, 160 },
                {26, 160 },
                {27, 160 },
                {28, 160 }
            };
            KnockbackDictionary = new Dictionary<int, float>(){
                {0, 1.6f },
                {1, 1.7f },
                {2, 1.8f },
                {3, 1.95f },
                {4, 2.1f },
                {5, 2.3f },
                {6, 2.5f },
                {7, 2.7f },
                {8, 3.0f },
                {9, 3.3f },
                {10, 3.6f },
                {11, 3.9f },
                {12, 4.1f },
                {13, 4.3f },
                {14, 4.5f },
                {15, 4.7f },
                {16, 4.9f },
                {17, 5.0f },
                {18, 5.1f },
                {19, 5.2f },
                {20, 5.4f },
                {21, 5.6f },
                {22, 5.8f },
                {23, 5.9f },
                {24, 6.0f },
                {25, 6.1f },
                {26, 6.2f },
                {27, 6.4f },
                {28, 6.5f }
            };
        }
        public override void SetStaticDefaults() {
            Main.RegisterItemAnimation(TargetID, new DrawAnimationVertical(5, 13));
            ItemID.Sets.AnimatesAsSoul[TargetID] = true;
        }

        public override void SetDefaults(Item item) => SetDefaultsFunc(item);

        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips) => TooltipHandler.SetTooltip(item, ref tooltips);

        public override bool On_ModifyWeaponDamage(Item item, Player player, ref StatModifier damage) {
            int onDamage = GetOnDamage(item);
            CWRUtils.ModifyLegendWeaponDamageFunc(item, onDamage, GetStartDamage, ref damage);
            float meleeSpeedRoad = player.GetWeaponAttackSpeed(item);
            float SpeedToMelee = 1f + (float)Math.Log(meleeSpeedRoad) * 0.48f;
            damage *= SpeedToMelee;
            return false;
        }

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
            Item.DamageType = CWRRef.GetTrueMeleeDamageClass();
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.channel = true;
            Item.useAnimation = 25;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.useTime = 5;
            Item.knockBack = GetStartKnockback;
            Item.autoReuse = false;
            Item.shoot = ModContent.ProjectileType<MuraSlashDefault>();
            Item.shootSpeed = 24f;
            Item.rare = CWRID.Rarity_BurnishedAuric;
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
                ? false : player.ownedProjectileCounts[Item.shoot] == 0;
        }
    }
}
