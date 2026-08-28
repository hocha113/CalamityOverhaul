using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.FluidPumps
{
    internal class FluidPumpTile : ModTile
    {
        public override string Texture => CWRConstant.Asset + "ElectricPowers/FluidPumpTile";
        public override void SetStaticDefaults() {
            Main.tileLighted[Type] = true;
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;
            Main.tileSolidTop[Type] = true;
            AddMapEntry(new Color(64, 96, 128), VaultUtils.GetLocalizedItemName<FluidPump>());

            TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
            TileObjectData.newTile.Width = 3;
            TileObjectData.newTile.Height = 2;
            TileObjectData.newTile.Origin = new Point16(1, 1);
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile
                | AnchorType.SolidWithTop | AnchorType.SolidSide, TileObjectData.newTile.Width, 0);
            TileObjectData.newTile.CoordinateHeights = [16, 16];
            TileObjectData.newTile.LavaDeath = false;

            TileObjectData.addTile(Type);
        }

        public override void MouseOver(int i, int j) {
            Player player = Main.LocalPlayer;
            player.noThrow = 2;
            player.mouseInterface = true;
            player.cursorItemIconEnabled = true;
            player.cursorItemIconID = ModContent.ItemType<FluidPump>();
        }

        public override bool CreateDust(int i, int j, ref int type) {
            Dust.NewDust(new Vector2(i, j) * 16f, 16, 16, DustID.Water);
            return false;
        }

        public override void NumDust(int i, int j, bool fail, ref int num) => num = fail ? 1 : 3;

        public override bool CanDrop(int i, int j) => false;
    }
}
