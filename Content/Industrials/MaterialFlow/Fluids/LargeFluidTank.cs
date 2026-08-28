using CalamityOverhaul.Content.Items.Materials;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.Industrials.MaterialFlow.Fluids
{
    /// <summary>大型液体储罐:储罐的上位大容量件,行为全部继承小罐</summary>
    internal class LargeFluidTank : ModItem
    {
        public override string Texture => CWRConstant.Asset + "MaterialFlow/LargeFluidTank";
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
            Item.value = Item.buyPrice(0, 4, 0, 0);
            Item.rare = ItemRarityID.Blue;
            Item.createTile = ModContent.TileType<LargeFluidTankTile>();
        }

        public override void AddRecipes() {
            CreateRecipe().
            AddIngredient<FluidTank>(2).
            AddIngredient<CircuitBoard>(8).
            AddIngredient(ItemID.Glass, 30).
            AddRecipeGroup(RecipeGroupID.IronBar, 10).
            AddTile(TileID.Anvils).
            Register();
        }
    }

    internal class LargeFluidTankTile : ModTile
    {
        public override string Texture => CWRConstant.Asset + "MaterialFlow/LargeFluidTankTile";
        public override void SetStaticDefaults() {
            Main.tileLighted[Type] = true;
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;
            Main.tileSolidTop[Type] = true;

            AddMapEntry(new Color(58, 96, 118), VaultUtils.GetLocalizedItemName<LargeFluidTank>());

            TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
            TileObjectData.newTile.Width = 4;
            TileObjectData.newTile.Height = 5;
            TileObjectData.newTile.Origin = new Point16(2, 4);
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide, TileObjectData.newTile.Width, 0);
            TileObjectData.newTile.CoordinateHeights = [16, 16, 16, 16, 16];
            TileObjectData.newTile.LavaDeath = false;

            TileObjectData.addTile(Type);
        }

        public override void MouseOver(int i, int j) {
            Player player = Main.LocalPlayer;
            player.noThrow = 2;
            player.mouseInterface = true;
            player.cursorItemIconEnabled = true;
            player.cursorItemIconID = ModContent.ItemType<LargeFluidTank>();
        }

        public override bool CreateDust(int i, int j, ref int type) {
            Dust.NewDust(new Vector2(i, j) * 16f, 16, 16, DustID.Glass);
            return false;
        }

        public override bool CanDrop(int i, int j) => false;
    }

    /// <summary>大型储罐TP:容量四倍,液窗对齐大罐穹顶玻璃,其余行为同小罐</summary>
    internal class LargeFluidTankTP : FluidTankTP
    {
        public override int TargetTileID => ModContent.TileType<LargeFluidTankTile>();
        public override int TargetItem => ModContent.ItemType<LargeFluidTank>();
        public override int FluidCapacity => 128 * FluidHelper.UnitsPerTile;
        internal override Vector2 ChamberMin => new(18f, 20f);
        internal override Vector2 ChamberMax => new(46f, 46f);
    }
}
