using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.Items.Placeable
{
    internal class StarflowPlatedBlock : ModItem
    {
        public override string Texture => CWRConstant.Item + "Placeable/StarflowPlatedBlock";

        public override void SetDefaults() {
            Item.width = 16;
            Item.height = 16;
            Item.maxStack = 9999;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.useAnimation = 15;
            Item.useTime = 10;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.consumable = true;
            Item.value = Item.buyPrice(silver: 1);
            Item.rare = ItemRarityID.Blue;
            Item.createTile = ModContent.TileType<StarflowPlatedBlockTile>();
        }

        public override void AddRecipes() {
            CreateRecipe(10)
                .AddIngredient(ItemID.StoneBlock, 10)
                .AddIngredient(ItemID.Wire, 2)
                .AddIngredient(ItemID.FallenStar, 1)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }

    internal class StarflowPlatedBlockTile : ModTile
    {
        public override string Texture => CWRConstant.Item + "Placeable/StarflowPlatedBlock";

        public override void SetStaticDefaults() {
            Main.tileSolid[Type] = true;
            Main.tileMergeDirt[Type] = true;
            Main.tileBlockLight[Type] = true;
            Main.tileLighted[Type] = true;
            
            DustType = DustID.Stone;
            AddMapEntry(new Color(150, 180, 220));
            
            MineResist = 1.5f;
            MinPick = 55;
        }

        public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b) {
            r = 0.1f;
            g = 0.15f;
            b = 0.2f;
        }

        public override bool CreateDust(int i, int j, ref int type) {
            type = DustID.TreasureSparkle;
            return true;
        }
    }
}
