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
    /// 供墨雨/墨瀑取用;不经物品使用的出口(鬼梦恶犬、鞭笞、伞奴)走 <see cref="GetPanelDamage"/>
    /// </summary>
    internal class KikasaOverride : ItemOverride, ILocalizedModType
    {
        public override string LocalizationCategory => "Legend";

        public static int ID => ModContent.ItemType<KikasaItem>();
        public override int TargetID => ID;

        /// <summary>改动信息由自绘面板承载,关掉鼠标旁的金色小图标</summary>
        public override bool DrawingInfo => false;

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

        //==================== 自绘面板文本(KikasaItemTooltipPanel) ====================
        public static LocalizedText KeyLabelDomain { get; private set; }
        public static LocalizedText KeyLabelSink { get; private set; }
        public static LocalizedText KeyLabelMutate { get; private set; }
        public static LocalizedText KeyLabelWheel { get; private set; }
        public static LocalizedText KeyLabelRestart { get; private set; }
        public static LocalizedText KeyLabelTeleport { get; private set; }
        public static LocalizedText KeyLabelPanorama { get; private set; }
        /// <summary>血湖表里键解绑后的输入层回退</summary>
        public static LocalizedText MutateFallback { get; private set; }

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

        /// <summary>
        /// 鬼伞面板伤（等级表已缩放，含召唤加成与前缀）
        /// 梦中 noItems、鞭笞、伞奴等不经物品使用的出口先 HeldItem 再扫背包，不用 GetItem()（远端会被本机 mouseItem 污染）
        /// </summary>
        public static int GetPanelDamage(Player player) {
            if (player == null) {
                return GetStartDamage;
            }
            Item held = player.HeldItem;
            if (IsKikasa(held)) {
                return player.GetWeaponDamage(held);
            }
            foreach (Item item in player.inventory) {
                if (IsKikasa(item)) {
                    return player.GetWeaponDamage(item);
                }
            }
            return GetStartDamage;
        }

        /// <summary>
        /// 鬼奴基伤表的标定档：18 只鬼奴的常量全部按三机械档（等级 11、表值 92）的手感写死，
        /// <see cref="KikasaServants.KikasaServantBalanceGlobal"/> 在命中端按"当前表值/92"折算成长
        /// </summary>
        public const float ServantTuneAnchor = 92f;

        /// <summary>
        /// 等级表原始值（不含召唤加成与前缀，别与 <see cref="GetPanelDamage"/> 混用——
        /// 那份含加成，叠乘会把召唤加成吃两遍）。鬼奴锚点与械奴钳顶的进度读数
        /// </summary>
        public static int GetRawLevelDamage(Player player) {
            if (player == null) {
                return GetStartDamage;
            }
            Item held = player.HeldItem;
            if (IsKikasa(held)) {
                return DamageDictionary[GetLevel(held)];
            }
            foreach (Item item in player.inventory) {
                if (IsKikasa(item)) {
                    return DamageDictionary[GetLevel(item)];
                }
            }
            return GetStartDamage;
        }

        private static bool IsKikasa(Item item)
            => item != null && item.Alives() && item.type == ID;

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
            GhostCountLine = this.GetLocalization(nameof(GhostCountLine), () => "召唤栏 {0} · {1}");
            TierNames = new LocalizedText[4];
            string[] tierDefaults = ["细雨", "侧掷", "齐掷", "湖倾"];
            for (int i = 0; i < TierNames.Length; i++) {
                int idx = i;
                TierNames[i] = this.GetLocalization($"TierName{i}", () => tierDefaults[idx]);
            }
            //自绘面板:键位功能名(与键位表动作名对齐)与试炼进度行
            KeyLabelDomain = this.GetLocalization(nameof(KeyLabelDomain), () => "领域展开");
            KeyLabelSink = this.GetLocalization(nameof(KeyLabelSink), () => "沉物入湖");
            KeyLabelMutate = this.GetLocalization(nameof(KeyLabelMutate), () => "血湖表里");
            KeyLabelWheel = this.GetLocalization(nameof(KeyLabelWheel), () => "召影转盘");
            KeyLabelRestart = this.GetLocalization(nameof(KeyLabelRestart), () => "重启自身");
            KeyLabelTeleport = this.GetLocalization(nameof(KeyLabelTeleport), () => "领域传送");
            KeyLabelPanorama = this.GetLocalization(nameof(KeyLabelPanorama), () => "湖心景");
            MutateFallback = this.GetLocalization(nameof(MutateFallback), () => "鼠标中键");
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
            //试炼进度与任务书提示已由自绘面板(KikasaItemTooltipPanel)承载,
            //这里只补动态的伞下鬼行,旧 [Lang4]/legend_Text 占位符随正文裁短一并退役
            AppendGhostLine(tooltips);
            LegendTooltipPanel.WrapBodyText(tooltips);
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
