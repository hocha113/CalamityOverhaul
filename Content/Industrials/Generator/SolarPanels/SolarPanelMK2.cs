using CalamityOverhaul.Content.Items.Materials;
using InnoVault.TileProcessors;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.Industrials.Generator.SolarPanels
{
    internal class SolarPanelMK2 : ModItem
    {
        public override string Texture => CWRConstant.Asset + "Generator/SolarPanelMK2";

        /// <summary>系列色调:圣辉紫金,用于 UI 点缀</summary>
        internal static readonly Color Tint = new(215, 175, 255);

        public override void SetDefaults() {
            Item.width = 32;
            Item.height = 32;
            Item.maxStack = 9999;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.useAnimation = 15;
            Item.useTime = 10;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.consumable = true;
            Item.value = Item.buyPrice(0, 3, 0, 0);
            Item.rare = ItemRarityID.Pink;
            Item.createTile = ModContent.TileType<SolarPanelMK2Tile>();
            Item.CWR().StorageUE = true;
            Item.CWR().ConsumeUseUE = 4000;
        }

        public override void AddRecipes() {
            CreateRecipe().
            AddIngredient<SolarPanel>().
            AddIngredient(ItemID.HallowedBar, 10).
            AddIngredient(ItemID.SoulofLight, 5).
            AddIngredient<CircuitBoard>(20).
            AddTile(TileID.MythrilAnvil).
            Register();

        }
    }

    internal class SolarPanelMK2Tile : BaseGeneratorTile
    {
        public override string Texture => CWRConstant.Asset + "Generator/SolarPanelMK2Tile";
        public override int GeneratorTP => TileProcessorLoader.GetModuleID<SolarPanelMK2TP>();
        public override int GeneratorUI => UIHandleLoader.GetUIHandleID<GeneratorReadoutUI>();
        public override int TargetItem => ModContent.ItemType<SolarPanelMK2>();

        public override void SetStaticDefaults() {
            Main.tileLighted[Type] = true;
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;
            Main.tileSolidTop[Type] = true;
            AddMapEntry(new Color(150, 110, 220), VaultUtils.GetLocalizedItemName<SolarPanelMK2>());

            TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
            TileObjectData.newTile.Width = 7;
            TileObjectData.newTile.Height = 3;
            TileObjectData.newTile.Origin = new Point16(3, 2);
            TileObjectData.newTile.CoordinateHeights = [16, 16, 16];
            TileObjectData.newTile.AnchorBottom = new AnchorData(
                AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide,
                TileObjectData.newTile.Width, 0);
            TileObjectData.newTile.CoordinateWidth = 16;
            TileObjectData.newTile.CoordinatePadding = 2;
            TileObjectData.addTile(Type);
        }

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) {
            if (!VaultUtils.SafeGetTopLeft(i, j, out var point)) {
                return false;
            }
            if (!TileProcessorLoader.ByPositionGetTP(point, out SolarPanelMK2TP tp)) {
                return false;
            }
            return BaseSolarPanelTP.DrawDimmablePanelTile(i, j, spriteBatch, tp, Type);
        }
    }

    internal class SolarPanelMK2TP : BaseSolarPanelTP
    {
        public override int TargetTileID => ModContent.TileType<SolarPanelMK2Tile>();
        public override int TargetItem => ModContent.ItemType<SolarPanelMK2>();
        public override float MaxUEValue => 4000 * ModuleRack.StorageMult;
        public override float PeakOutput => 2.4f;
        public override int ModuleSlotCount => 3;

        //圣辉掠光:金白,与初代冷白一眼区分
        protected override Color GlintColor => new(255, 226, 168);
    }
}
