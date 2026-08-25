using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Sensors
{
    /// <summary>
    /// 多模式传感器,电网的电子眼;贴图暂复用能量管道物品,感应绿色调区分。<br/>
    /// 面板文本一并注册在本物品下(Items 类目),UI 类只引用不注册
    /// </summary>
    internal class Sensor : ModItem
    {
        public override string Texture => CWRConstant.Asset + "MaterialFlow/PipelineItem";

        /// <summary>系列色调:感应绿</summary>
        internal static readonly Color Tint = new(118, 214, 130);

        public static LocalizedText TitleText { get; private set; }
        public static LocalizedText ConditionLabelText { get; private set; }
        public static LocalizedText ModeOffText { get; private set; }
        public static LocalizedText ModeChargeAboveText { get; private set; }
        public static LocalizedText ModeChargeBelowText { get; private set; }
        public static LocalizedText ModeEnemyText { get; private set; }
        public static LocalizedText ModeBloodMoonText { get; private set; }
        public static LocalizedText ModeEclipseText { get; private set; }
        public static LocalizedText ModeSlimeRainText { get; private set; }
        public static LocalizedText ModeInvasionText { get; private set; }
        public static LocalizedText ThresholdLabelText { get; private set; }
        public static LocalizedText RangeLabelText { get; private set; }
        public static LocalizedText EventHintText { get; private set; }
        public static LocalizedText OutputLabelText { get; private set; }
        public static LocalizedText OutputLevelText { get; private set; }
        public static LocalizedText OutputPulseText { get; private set; }
        public static LocalizedText StatusActiveText { get; private set; }
        public static LocalizedText StatusIdleText { get; private set; }
        public static LocalizedText StatusNoPowerText { get; private set; }
        public static LocalizedText StatusOffText { get; private set; }
        public static LocalizedText EnergyLabelText { get; private set; }

        public override void SetStaticDefaults() {
            TitleText = this.GetLocalization(nameof(TitleText), () => "多模式传感器");
            ConditionLabelText = this.GetLocalization(nameof(ConditionLabelText), () => "触发条件");
            ModeOffText = this.GetLocalization(nameof(ModeOffText), () => "关闭");
            ModeChargeAboveText = this.GetLocalization(nameof(ModeChargeAboveText), () => "电量高于阈值");
            ModeChargeBelowText = this.GetLocalization(nameof(ModeChargeBelowText), () => "电量低于阈值");
            ModeEnemyText = this.GetLocalization(nameof(ModeEnemyText), () => "敌情警戒");
            ModeBloodMoonText = this.GetLocalization(nameof(ModeBloodMoonText), () => "血月");
            ModeEclipseText = this.GetLocalization(nameof(ModeEclipseText), () => "日食");
            ModeSlimeRainText = this.GetLocalization(nameof(ModeSlimeRainText), () => "史莱姆雨");
            ModeInvasionText = this.GetLocalization(nameof(ModeInvasionText), () => "入侵");
            ThresholdLabelText = this.GetLocalization(nameof(ThresholdLabelText), () => "触发阈值");
            RangeLabelText = this.GetLocalization(nameof(RangeLabelText), () => "警戒半径");
            EventHintText = this.GetLocalization(nameof(EventHintText), () => "读取世界事件,无需参数");
            OutputLabelText = this.GetLocalization(nameof(OutputLabelText), () => "输出方式");
            OutputLevelText = this.GetLocalization(nameof(OutputLevelText), () => "电平跟随");
            OutputPulseText = this.GetLocalization(nameof(OutputPulseText), () => "单次脉冲");
            StatusActiveText = this.GetLocalization(nameof(StatusActiveText), () => "条件成立");
            StatusIdleText = this.GetLocalization(nameof(StatusIdleText), () => "待命");
            StatusNoPowerText = this.GetLocalization(nameof(StatusNoPowerText), () => "缺电");
            StatusOffText = this.GetLocalization(nameof(StatusOffText), () => "已关闭");
            EnergyLabelText = this.GetLocalization(nameof(EnergyLabelText), () => "电力");
        }

        public override void SetDefaults() {
            Item.width = 24;
            Item.height = 24;
            Item.maxStack = 9999;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.useAnimation = 15;
            Item.useTime = 10;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.consumable = true;
            Item.value = Item.buyPrice(0, 0, 50, 0);
            Item.rare = ItemRarityID.Green;
            Item.color = Tint;
            Item.createTile = ModContent.TileType<SensorTile>();
            Item.CWR().StorageUE = true;
            Item.CWR().ConsumeUseUE = 200;
        }

        public override void AddRecipes() {
            if (CWRID.DubiousCircuitryAvailable) {
                CreateRecipe().
                AddIngredient(CWRID.Item_DubiousPlating, 10).
                AddIngredient(CWRID.Item_MysteriousCircuitry, 10).
                AddIngredient(ItemID.Lens, 2).
                AddIngredient(ItemID.Wire, 5).
                AddTile(TileID.Anvils).
                Register();
            }
            else {
                CreateRecipe().
                AddRecipeGroup(CWRCrafted.TinBarGroup, 8).
                AddIngredient(ItemID.Lens, 2).
                AddIngredient(ItemID.Wire, 5).
                AddTile(TileID.Anvils).
                Register();
            }
        }
    }
}
