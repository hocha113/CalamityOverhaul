using CalamityMod.Items;
using Microsoft.Xna.Framework.Graphics;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul
{
    internal class TextItem : ModItem
    {
        public override string Texture => "CalamityOverhaul/icon";

        //private bool old;
        public override bool IsLoadingEnabled(Mod mod) {
            return true;
        }

        public override void SetDefaults() {
            Item.width = 80;
            Item.height = 80;
            Item.damage = 9999;
            Item.DamageType = DamageClass.Default;
            Item.useAnimation = Item.useTime = 13;
            Item.useTurn = true;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 2.25f;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.shootSpeed = 8f;
            Item.shoot = ProjectileID.PurificationPowder;
            Item.value = CalamityGlobalItem.RarityYellowBuyPrice;
            Item.rare = ItemRarityID.Yellow;
        }

        public override void UpdateInventory(Player player) {
            //player.velocity.Domp();
            //bool news = player.PressKey(false);
            //if (news && !old) {
            //    player.QuickSpawnItem(player.parent(), Main.HoverItem, Main.HoverItem.stack);
            //}
            //old = news;

        }

        public override void HoldItem(Player player) {
        }

        private void WriteTile(BinaryWriter writer, Tile tile, Point offsetPoint) {
            writer.Write(offsetPoint.X);
            writer.Write(offsetPoint.Y);
            writer.Write(tile.WallType);
            writer.Write(tile.TileType);
            writer.Write(tile.TileFrameX);
            writer.Write(tile.TileFrameY);
            writer.Write(tile.HasTile);
            writer.Write((byte)tile.Slope);
        }

        private void SetTile(BinaryReader reader) {
            int tilePosX = reader.ReadInt32() + 3720;
            int tilePosY = reader.ReadInt32() + 400;
            ushort wallType = reader.ReadUInt16();
            ushort tileType = reader.ReadUInt16();
            short frameX = reader.ReadInt16();
            short frameY = reader.ReadInt16();
            bool hasTile = reader.ReadBoolean();
            byte slope = reader.ReadByte();
            Tile tile = Main.tile[tilePosX, tilePosY];
            if (wallType > 1) {
                tile.WallType = wallType;
                tile.LiquidAmount = 255;
            }

            tile.HasTile = hasTile;
            tile.Slope = (SlopeType)slope;

            if (tileType > 0) {
                tile.TileType = tileType;
            }
            tile.TileFrameX = frameX;
            tile.TileFrameY = frameY;
            CWRUtils.SafeSquareTileFrame(tilePosX, tilePosY);
        }

        public override bool? UseItem(Player player) {
            Point startPoint = new Point(1720, 400);
            Point endPoint = new Point(1720, 400);
            int heiget = 2000;
            int wid = 1400;
            //using (BinaryWriter writer = new BinaryWriter(File.Open("D:\\TileWorldData\\structure.dat", FileMode.Create))) {
            //    for (int x = 0; x < wid; x++) {
            //        for (int y = 0; y < heiget; y++) {
            //            Point offsetPoint = new Point(x, y);
            //            WriteTile(writer, Main.tile[startPoint.X + x, startPoint.Y + y], offsetPoint);
            //        }
            //    }
            //}
            //Point point = new Point((int)Main.MouseWorld.X / 16, (int)Main.MouseWorld.Y / 16);
            //point.Domp();

            //using (BinaryReader reader = new BinaryReader(File.Open("D:\\TileWorldData\\structure.dat", FileMode.Open))) {
            //    for (int x = 0; x < wid; x++) {
            //        for (int y = 0; y < heiget; y++) {
            //            SetTile(reader);
            //        }
            //    }
            //}

            return true;
        }
    }
}
