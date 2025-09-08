using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.Tiles
{
    internal class WUTIVSelfPortraitTile : ModTile
    {
        public override string Texture => CWRConstant.Asset + "Painting/WUTIVSelfPortraitTile";
        public override void SetStaticDefaults() {
            Main.tileFrameImportant[Type] = true;
            Main.tileLavaDeath[Type] = true;
            Main.tileSpelunker[Type] = true;
            Main.tileWaterDeath[Type] = false;
            TileObjectData.newTile.CopyFrom(TileObjectData.Style3x3Wall);
            TileObjectData.newTile.Width = 9;
            TileObjectData.newTile.Height = 9;
            TileObjectData.newTile.Origin = new Point16(4, 4);
            TileObjectData.newTile.LavaDeath = true;
            TileObjectData.newTile.CoordinateHeights = [16, 16, 16, 16, 16, 16, 16, 16, 16];
            TileObjectData.addTile(Type);
            TileID.Sets.DisableSmartCursor[Type] = true;
            TileID.Sets.FramesOnKillWall[Type] = true;
            TileID.Sets.DisableSmartCursor[Type] = true;
            DustType = DustID.WoodFurniture;
            AddMapEntry(new Color(99, 50, 30), Language.GetText("MapObject.Painting"));
        }
    }
}
