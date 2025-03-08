using System.IO;
using Terraria.ID;
using Terraria;

namespace CalamityOverhaul.Content.Structures.DatIO
{
    public class DatIOLoader
    {
        public static void WriteTile(BinaryWriter writer, Tile tile, Point offsetPoint) {
            writer.Write(offsetPoint.X);
            writer.Write(offsetPoint.Y);
            writer.Write(tile.WallType);
            writer.Write(tile.LiquidAmount);
            writer.Write(tile.TileType);
            writer.Write(tile.TileFrameX);
            writer.Write(tile.TileFrameY);
            writer.Write(tile.HasTile);
            writer.Write((byte)tile.Slope);
        }

        public static void SetTile(BinaryReader reader, Point point) {
            int tilePosX = reader.ReadInt32() + point.X;
            int tilePosY = reader.ReadInt32() + point.Y;

            ushort wallType = reader.ReadUInt16();
            byte liquidAmount = reader.ReadByte();
            ushort tileType = reader.ReadUInt16();
            short frameX = reader.ReadInt16();
            short frameY = reader.ReadInt16();
            bool hasTile = reader.ReadBoolean();
            byte slope = reader.ReadByte();

            Tile tile = Main.tile[tilePosX, tilePosY];
            if (wallType > 1) {
                tile.WallType = wallType;
                tile.LiquidAmount = liquidAmount;
            }

            tile.HasTile = hasTile;
            tile.Slope = (SlopeType)slope;

            if (tileType > 0) {
                tile.TileType = tileType;
            }
            tile.TileFrameX = frameX;
            tile.TileFrameY = frameY;
        }
    }
}
