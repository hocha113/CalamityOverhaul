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
        /// <summary>贴图复用热能发电机MK2,靠圣辉紫金色调区分;专属贴图见待美术清单</summary>
        public override string Texture => CWRConstant.Asset + "Generator/ThermalGeneratorMK2";

        /// <summary>系列色调:圣辉紫金</summary>
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
            Item.color = Tint;
            Item.createTile = ModContent.TileType<SolarPanelMK2Tile>();
            Item.CWR().StorageUE = true;
            Item.CWR().ConsumeUseUE = 4000;
        }

        public override void AddRecipes() {
            if (CWRID.DubiousCircuitryAvailable) {
                CreateRecipe().
                AddIngredient<SolarPanel>().
                AddIngredient(ItemID.HallowedBar, 10).
                AddIngredient(ItemID.SoulofLight, 5).
                AddIngredient(CWRID.Item_DubiousPlating, 20).
                AddIngredient(CWRID.Item_MysteriousCircuitry, 20).
                AddTile(TileID.MythrilAnvil).
                Register();
            }
            else {
                CreateRecipe().
                AddIngredient<SolarPanel>().
                AddIngredient(ItemID.HallowedBar, 10).
                AddIngredient(ItemID.SoulofLight, 5).
                AddTile(TileID.MythrilAnvil).
                Register();
            }
        }
    }

    internal class SolarPanelMK2Tile : BaseGeneratorTile
    {
        /// <summary>零贴图程序化绘制,占位魔法像素保证加载安全</summary>
        public override string Texture => CWRConstant.VaultPlaceholder2;
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
            TileObjectData.newTile.Width = 4;
            TileObjectData.newTile.Height = 2;
            TileObjectData.newTile.Origin = new Point16(1, 1);
            TileObjectData.newTile.CoordinateHeights = [16, 16];
            TileObjectData.newTile.StyleWrapLimit = 36;
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
            //整机只在左上格画一次
            if (point.X != i || point.Y != j) {
                return false;
            }
            if (!TileProcessorLoader.ByPositionGetTP(point, out SolarPanelMK2TP tp)) {
                return false;
            }
            tp.DrawPanelBody(spriteBatch);
            return false;
        }
    }

    internal class SolarPanelMK2TP : BaseSolarPanelTP
    {
        public override int TargetTileID => ModContent.TileType<SolarPanelMK2Tile>();
        public override int TargetItem => ModContent.ItemType<SolarPanelMK2>();
        public override float MaxUEValue => 4000 * ModuleRack.StorageMult;
        public override float PeakOutput => 2.4f;
        public override int ModuleSlotCount => 3;

        //圣辉配色:紫底金格,与初代一眼区分
        protected override Color PanelColor => new(58, 40, 108);
        protected override Color CellColor => new(214, 172, 96);
        protected override Color GlintColor => new(255, 226, 168);
    }
}
