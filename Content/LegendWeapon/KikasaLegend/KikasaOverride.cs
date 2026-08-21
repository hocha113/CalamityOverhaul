using CalamityOverhaul.Common;
using CalamityOverhaul.OtherMods.Wikithis;
using InnoVault.GameSystem;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend
{
    /// <summary>
    /// 成长层,经 <see cref="ItemOverride"/> 挂 <see cref="KikasaItem"/>。
    /// 除传奇等级伤害表外,伞下鬼(召唤栏位驱动普攻强度)的数值口径也集中在这里,
    /// 供 <see cref="KikasaRains.KikasaRainUmbrella"/> 与 <see cref="KikasaRains.KikasaInkPour"/> 取用
    /// </summary>
    internal class KikasaOverride : ItemOverride, ILocalizedModType
    {
        public override string LocalizationCategory => "Legend";

        public static int ID => ModContent.ItemType<KikasaItem>();
        public override int TargetID => ID;

        private static Dictionary<int, int> DamageDictionary = [];

        /// <summary>鬼伞成长等级上限(24 段沉宴试炼)</summary>
        public const int MaxLevel = 24;

        //==================== 伞下鬼:召唤栏位缩放口径 ====================

        /// <summary>二档·二鬼帮衬:每波加一颗侧掷鬼滴,墨瀑散射上限 7→9</summary>
        public const int TierGhostAssist = 4;

        /// <summary>三档·众鬼齐掷:每第 4 波全鬼同帧齐掷,墨瀑延时+沉溺内吸</summary>
        public const int TierGhostVolley = 7;

        /// <summary>四档·湖倾:大滴落地留墨洼,满蓄墨瀑收尾喷墨泉</summary>
        public const int TierLakeTilt = 10;

        /// <summary>强度缩放读取的栏位上限</summary>
        public const int SlotCap = 12;

        public static LocalizedText GhostCountLine { get; private set; }
        public static LocalizedText[] TierNames { get; private set; }

        /// <summary>伞下鬼数=召唤栏位,钳 1..12;数值缩放与档位判定共用此口径</summary>
        public static int GetSlotCount(Player player)
            => Math.Clamp(player?.maxMinions ?? 1, 1, SlotCap);

        /// <summary>当前档位 0~3,只用于表现与文本</summary>
        public static int GetTier(int slots) {
            if (slots >= TierLakeTilt) {
                return 3;
            }
            if (slots >= TierGhostVolley) {
                return 2;
            }
            if (slots >= TierGhostAssist) {
                return 1;
            }
            return 0;
        }

        /// <summary>墨雨节拍周期:26 帧起每格快 1.1 帧,下限 14 帧(窗口下限由伞侧再钳)</summary>
        public static int GetVolleyPeriod(int slots)
            => Math.Max(14, 26 - (int)(1.1f * (slots - 1)));

        /// <summary>每波滴数加成:每 3 格栏位多一滴</summary>
        public static int GetDropBonus(int slots) => slots / 3;

        /// <summary>滴间错拍:高档雨密成帘</summary>
        public static int GetDropStagger(int slots) => slots >= TierGhostVolley ? 1 : 2;

        /// <summary>单滴与墨瀑共用的伤害乘区:大头在频率与滴数,这里只给每格 5%</summary>
        public static float GetSlotDamageMul(int slots) => 1f + 0.05f * (slots - 1);

        /// <summary>蓄墨满帧:90 帧起每格快 3 帧,下限 56 帧</summary>
        public static int GetChargeFullFrames(int slots) => Math.Max(56, 90 - 3 * (slots - 1));

        /// <summary>墨瀑宽度加成(px)</summary>
        public static float GetPourWidthBonus(int slots) => 2f * slots;

        //==================== 传奇成长 ====================

        public static int GetStartDamage => DamageDictionary[0];

        public static int GetLevel(Item item) {
            if (item == null || item.type != ID) {
                return 0;
            }
            CWRItem cwrItem = item.CWR();
            if (cwrItem?.LegendData == null) {
                return 0;
            }
            return ClampLevel(cwrItem.LegendData.Level);
        }

        internal static int ClampLevel(int level) => Math.Clamp(level, 0, MaxLevel);

        public static int GetOnDamage(Item item) => DamageDictionary[GetLevel(item)];

        public static void LoadWeaponData() {
            //锚点 0/8(肉山)/11(三机械)/18(月总)/24(湖宴终席),中段近似线性;
            //上限压在鬼切(6002)之下:鬼伞高频多段,还吃栏位乘区
            DamageDictionary = new Dictionary<int, int> {
                {0, 8},
                {1, 12},
                {2, 16},
                {3, 21},
                {4, 27},
                {5, 33},
                {6, 40},
                {7, 46},
                {8, 58},
                {9, 66},
                {10, 75},
                {11, 92},
                {12, 105},
                {13, 118},
                {14, 130},
                {15, 145},
                {16, 160},
                {17, 180},
                {18, 245},
                {19, 320},
                {20, 400},
                {21, 540},
                {22, 720},
                {23, 1150},
                {24, 1600},
            };
        }

        public override void SetStaticDefaults() {
            LoadWeaponData();
            GhostCountLine = this.GetLocalization(nameof(GhostCountLine), () => "伞下栖鬼 {0} 位——{1}");
            TierNames = new LocalizedText[4];
            string[] tierDefaults = ["细雨", "二鬼帮衬", "众鬼齐掷", "湖倾"];
            for (int i = 0; i < TierNames.Length; i++) {
                int idx = i;
                TierNames[i] = this.GetLocalization($"TierName{i}", () => tierDefaults[idx]);
            }
        }

        public override void SetDefaults(Item item) {
            LoadWeaponData();
            item.damage = GetStartDamage;
            item.CWR().LegendData = new KikasaData();
        }

        public override bool On_ModifyWeaponDamage(Item item, Player player, ref StatModifier damage) {
            VaultUtils.ApplyWeaponDamageScaling(item, GetOnDamage(item), GetStartDamage, ref damage);
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
            string keyDisplay = CWRKeySystem.QuestLog_Key?.GetAssignedKeys() is { Count: > 0 } k
                ? k[0] : CWRKeySystem.Notbound.Value;
            tooltips.ReplacePlaceholder("legend_Text",
                LegendUpgradeManagerSystem.QuestManagerHint.Value.Replace("{KEY}", keyDisplay), "");
            int index = item.CWR()?.LegendData?.TargetLevel ?? 0;
            string num = (index + 1).ToString();
            if (index >= MaxLevel) {
                num = LegendUpgradeManagerSystem.TrialPassed.Value;
            }
            string text = LegendData.GetLevelTrialPreText(item.CWR(), LegendUpgradeManagerSystem.Text_Lang_0, num);
            tooltips.ReplacePlaceholder("[Lang4]", text, "");
            AppendGhostLine(tooltips);
        }

        /// <summary>当前伞下鬼数与档位:面板即读,换装立见(SetTooltip 可能被调两次,按行名去重)</summary>
        private static void AppendGhostLine(List<TooltipLine> tooltips) {
            if (GhostCountLine == null || TierNames == null || Main.gameMenu) {
                return;
            }
            const string lineName = "CWR_KikasaGhosts";
            if (tooltips.Exists(line => line.Name == lineName)) {
                return;
            }
            int slots = GetSlotCount(Main.LocalPlayer);
            int tier = GetTier(slots);
            tooltips.Add(new TooltipLine(CWRMod.Instance, lineName,
                GhostCountLine.Format(slots, TierNames[tier].Value)) {
                OverrideColor = tier >= 3
                    ? new Color(214, 78, 84)
                    : new Color(150, 178, 186),
            });
        }
    }
}
