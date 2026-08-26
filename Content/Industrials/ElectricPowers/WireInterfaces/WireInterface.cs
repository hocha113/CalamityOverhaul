using CalamityOverhaul.Content.Items.Materials;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.WireInterfaces
{
    /// <summary>机关接口器,原版机关线与电网机器的双向转接头;贴图暂复用能量管道物品,机关红色调区分</summary>
    internal class WireInterface : ModItem
    {
        public override string Texture => CWRConstant.Asset + "MaterialFlow/PipelineItem";

        /// <summary>系列色调:机关红</summary>
        internal static readonly Color Tint = new(235, 106, 92);

        public static LocalizedText ModeBridgeText { get; private set; }
        public static LocalizedText ModeFullText { get; private set; }
        public static LocalizedText ModeEmptyText { get; private set; }
        public static LocalizedText MachineOffText { get; private set; }
        public static LocalizedText MachineOnText { get; private set; }

        public override void SetStaticDefaults() {
            ModeBridgeText = this.GetLocalization(nameof(ModeBridgeText), () => "仅桥接:收到机关信号时切换邻接机器");
            ModeFullText = this.GetLocalization(nameof(ModeFullText), () => "满电播报:邻接机器充满时发出机关信号");
            ModeEmptyText = this.GetLocalization(nameof(ModeEmptyText), () => "空电播报:邻接机器耗尽时发出机关信号");
            MachineOffText = this.GetLocalization(nameof(MachineOffText), () => "待机");
            MachineOnText = this.GetLocalization(nameof(MachineOnText), () => "启用");
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
            Item.value = Item.buyPrice(0, 0, 20, 0);
            Item.rare = ItemRarityID.Blue;
            Item.color = Tint;
            Item.createTile = ModContent.TileType<WireInterfaceTile>();
        }

        public override void AddRecipes() {
            CreateRecipe(2).
            AddIngredient<CircuitBoard>(8).
            AddIngredient(ItemID.Wire, 10).
            AddTile(TileID.Anvils).
            Register();

        }
    }
}
