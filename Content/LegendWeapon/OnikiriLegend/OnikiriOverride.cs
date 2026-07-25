using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions;
using CalamityOverhaul.OtherMods.Wikithis;
using InnoVault.GameSystem;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend
{
    /// <summary>成长层,经 <see cref="ItemOverride"/> 挂 <see cref="OnikiriItem"/></summary>
    internal class OnikiriOverride : ItemOverride, ILocalizedModType
    {
        public override string LocalizationCategory => "Legend";

        public static int ID => ModContent.ItemType<OnikiriItem>();
        public override int TargetID => ID;

        private static Dictionary<int, int> DamageDictionary = [];
        private static Dictionary<int, int> CritDictionary = [];
        private static Dictionary<int, float> BladeScaleDictionary = [];
        private static Dictionary<int, float> FinaleScaleDictionary = [];

        /// <summary>词缀叠乘后刀刃尺寸上限</summary>
        public const float MaxCompositeBladeScale = 1.45f;

        public static int GetStartDamage => DamageDictionary[0];

        public static int GetLevel(Item item) {
            if (item == null || item.type != ID) {
                return 0;
            }
            CWRItem cwrItem = item.CWR();
            if (cwrItem?.LegendData == null) {
                return 0;
            }
            return cwrItem.LegendData.Level;
        }

        public static int GetOnDamage(Item item) => DamageDictionary[GetLevel(item)];

        public static int GetOnCrit(Item item) => CritDictionary[GetLevel(item)];

        /// <summary>普攻/残心/肢解刀刃尺寸(0.70→1.25)</summary>
        public static float GetBladeScale(Item item) => BladeScaleDictionary[GetLevel(item)];

        /// <summary>终结乱舞弱缩放(0.95→1.08),灭世恒 1.0</summary>
        public static float GetFinaleScale(Item item) => FinaleScaleDictionary[GetLevel(item)];

        public static void LoadWeaponData() {
            //锚点 0/4/9/14/17/20/22,中间线性插值
            DamageDictionary = new Dictionary<int, int> {
                {0, 10},
                {1, 16},
                {2, 22},
                {3, 30},
                {4, 60},
                {5, 80},
                {6, 90},
                {7, 100},
                {8, 109},
                {9, 118},
                {10, 220},
                {11, 300},
                {12, 380},
                {13, 460},
                {14, 580},
                {15, 700},
                {16, 860},
                {17, 1020},
                {18, 1220},
                {19, 1580},
                {20, 1780},
                {21, 2030},
                {22, 2260},
            };
            CritDictionary = new Dictionary<int, int> {
                {0, 0},
                {1, 1},
                {2, 2},
                {3, 3},
                {4, 4},
                {5, 5},
                {6, 5},
                {7, 6},
                {8, 6},
                {9, 7},
                {10, 8},
                {11, 8},
                {12, 9},
                {13, 9},
                {14, 10},
                {15, 11},
                {16, 11},
                {17, 12},
                {18, 13},
                {19, 13},
                {20, 14},
                {21, 14},
                {22, 15},
            };
            BladeScaleDictionary = new Dictionary<int, float> {
                {0, 0.70f},
                {1, 0.73f},
                {2, 0.76f},
                {3, 0.79f},
                {4, 0.82f},
                {5, 0.846f},
                {6, 0.872f},
                {7, 0.898f},
                {8, 0.924f},
                {9, 0.95f},
                {10, 0.976f},
                {11, 1.002f},
                {12, 1.028f},
                {13, 1.054f},
                {14, 1.08f},
                {15, 1.103f},
                {16, 1.127f},
                {17, 1.15f},
                {18, 1.173f},
                {19, 1.197f},
                {20, 1.22f},
                {21, 1.235f},
                {22, 1.25f},
            };
            FinaleScaleDictionary = new Dictionary<int, float> {
                {0, 0.95f},
                {1, 0.955f},
                {2, 0.96f},
                {3, 0.965f},
                {4, 0.97f},
                {5, 0.976f},
                {6, 0.982f},
                {7, 0.988f},
                {8, 0.994f},
                {9, 1.00f},
                {10, 1.006f},
                {11, 1.012f},
                {12, 1.018f},
                {13, 1.024f},
                {14, 1.03f},
                {15, 1.037f},
                {16, 1.043f},
                {17, 1.05f},
                {18, 1.057f},
                {19, 1.063f},
                {20, 1.07f},
                {21, 1.075f},
                {22, 1.08f},
            };
        }

        public override void SetStaticDefaults() => LoadWeaponData();

        public override void SetDefaults(Item item) => SetDefaultsFunc(item);

        public static void SetDefaultsFunc(Item item) {
            LoadWeaponData();
            item.damage = GetStartDamage;
            item.CWR().LegendData = new OnikiriData();
        }

        public override bool On_ModifyWeaponDamage(Item item, Player player, ref StatModifier damage) {
            VaultUtils.ApplyWeaponDamageScaling(item, GetOnDamage(item), GetStartDamage, ref damage);
            //铭刻负担:面板伤害倍率(髭切 0.90);铭数据在物品上,面板各端一致
            float meiMul = OniMeiCombat.Resolve(item).DamageMul;
            if (meiMul != 1f) {
                damage *= meiMul;
            }
            return false;
        }

        public override bool? On_ModifyWeaponCrit(Item item, Player player, ref float crit) {
            crit += GetOnCrit(item);
            return false;
        }

        public override bool? On_ModifyTooltips(Item item, List<TooltipLine> tooltips) {
            CWRItem.OverModifyTooltip(item, tooltips);
            SetTooltip(item, ref tooltips);
            WikithisRef.TryAppendWikiTooltip(item, tooltips);
            return false;
        }

        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips) => SetTooltip(item, ref tooltips);

        public static void SetTooltip(Item item, ref List<TooltipLine> tooltips) {
            string keyDisplay = CWRKeySystem.QuestManager_Key?.GetAssignedKeys() is { Count: > 0 } k
                ? k[0] : CWRKeySystem.Notbound.Value;
            tooltips.ReplacePlaceholder("legend_Text",
                LegendUpgradeManagerSystem.QuestManagerHint.Value.Replace("{KEY}", keyDisplay), "");
            int index = item.CWR()?.LegendData?.TargetLevel ?? 0;
            string num = (index + 1).ToString();
            if (index >= 22) {
                num = LegendUpgradeManagerSystem.TrialPassed.Value;
            }
            string text = LegendData.GetLevelTrialPreText(item.CWR(), LegendUpgradeManagerSystem.Text_Lang_0, num);
            tooltips.ReplacePlaceholder("[Lang4]", text, "");
            AppendMeiSummary(item, tooltips);
        }

        /// <summary>在铭三槽的短摘要:离开改铭台也看得到赋效/代价(SetTooltip 可能被调两次,按行名去重)</summary>
        private static void AppendMeiSummary(Item item, List<TooltipLine> tooltips) {
            if (item.CWR()?.LegendData is not OnikiriData data) {
                return;
            }
            foreach (OniMeiSlotKind slot in OniMeiStore.SlotKinds) {
                OniMeiDefinition def = OniMeiRegistry.GetEngraved(data.Mei, slot);
                if (def == null) {
                    continue;
                }
                string lineName = $"CWR_OniMei_{slot}";
                if (tooltips.Exists(line => line.Name == lineName)) {
                    continue;
                }
                tooltips.Add(new TooltipLine(CWRMod.Instance, lineName
                    , $"「{def.DisplayName.Value}」{def.Summary.Value}") {
                    OverrideColor = def.IsGoldTier
                        ? new Color(218, 172, 82)
                        : new Color(198, 120, 112),
                });
            }
        }
    }
}
