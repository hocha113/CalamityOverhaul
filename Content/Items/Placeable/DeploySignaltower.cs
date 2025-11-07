using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.Items.Placeable
{
    internal class DeploySignaltower : ModItem
    {
        public override string Texture => CWRConstant.Item + "Placeable/DeploySignaltower";

        public override void SetDefaults() {
            Item.width = 32;
            Item.height = 48;
            Item.maxStack = 99;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.useAnimation = 15;
            Item.useTime = 10;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.consumable = true;
            Item.value = Item.buyPrice(gold: 5);
            Item.rare = ItemRarityID.LightPurple;
            Item.createTile = ModContent.TileType<DeploySignaltowerTile>();
        }
    }

    internal class DeploySignaltowerTile : ModTile
    {
        public override string Texture => CWRConstant.Item + "Placeable/DeploySignaltowerTile";

        public override void SetStaticDefaults() {
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;
            Main.tileSolid[Type] = false;

            TileID.Sets.DisableSmartCursor[Type] = true;

            AddMapEntry(new Color(100, 150, 255), VaultUtils.GetLocalizedItemName<DeploySignaltower>());

            TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
            TileObjectData.newTile.Width = 6;
            TileObjectData.newTile.Height = 14;
            TileObjectData.newTile.Origin = new Point16(2, 13);
            TileObjectData.newTile.CoordinateHeights = [
                16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16
            ];
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop, TileObjectData.newTile.Width, 0);
            TileObjectData.newTile.CoordinateWidth = 16;
            TileObjectData.newTile.CoordinatePadding = 2;
            TileObjectData.newTile.LavaDeath = false;
            TileObjectData.addTile(Type);
        }

        public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b) {
            r = 0.2f;
            g = 0.3f;
            b = 0.5f;
        }

        public override bool CreateDust(int i, int j, ref int type) {
            type = DustID.TreasureSparkle;
            return true;
        }
    }
}
