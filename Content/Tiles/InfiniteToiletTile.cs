using CalamityOverhaul.Content.Items.Placeable;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent;
using Terraria.GameContent.ObjectInteractions;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.Tiles
{
    internal class InfiniteToiletTile : ModTile
    {
        public override string Texture => CWRConstant.Asset + "Tiles/" + "InfiniteToilet";
        public override void SetStaticDefaults() {
            RegisterItemDrop(ModContent.ItemType<InfiniteToiletItem>(), 1);
            RegisterItemDrop(ModContent.ItemType<InfiniteToiletItem>());
            Main.tileFrameImportant[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;
            AdjTiles = [TileID.Chairs];
            AddToArray(ref TileID.Sets.RoomNeeds.CountsAsChair);
            AddMapEntry(new Color(191, 142, 111), Language.GetText("MapObject.Toilet"));
            TileID.Sets.CanBeSatOnForNPCs[Type] = true;
            TileID.Sets.CanBeSatOnForPlayers[Type] = true;
            TileID.Sets.HasOutlines[Type] = true;
            TileObjectData.newTile.CopyFrom(TileObjectData.Style3x2);
            TileObjectData.newTile.Width = 2;
            TileObjectData.newTile.Height = 3;
            TileObjectData.newTile.CoordinateHeights = [16, 16, 16];
            TileObjectData.newTile.CoordinateWidth = 16;
            TileObjectData.newTile.CoordinatePadding = 2;
            TileObjectData.newTile.Direction = TileObjectDirection.PlaceLeft;
            TileObjectData.newTile.StyleHorizontal = true;
            TileObjectData.newTile.Origin = new Point16(0, 1);
            TileObjectData.newTile.UsesCustomCanPlace = true;
            TileObjectData.newTile.LavaDeath = false;
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop, 2, 0);
            TileObjectData.newAlternate.CopyFrom(TileObjectData.newTile);
            TileObjectData.newAlternate.Direction = TileObjectDirection.PlaceRight;
            TileObjectData.addAlternate(1);
            TileObjectData.addTile(Type);
        }

        public override bool CanExplode(int i, int j) => false;

        public override bool CreateDust(int i, int j, ref int type) => false;

        public override void NumDust(int i, int j, bool fail, ref int num) => num = fail ? 1 : 3;

        public override void ModifySittingTargetInfo(int i, int j, ref TileRestingInfo info) {
            Tile tile = Framing.GetTileSafely(i, j);
            bool frameCheck = tile.TileFrameX >= 35;
            info.ExtraInfo.IsAToilet = true;
            info.TargetDirection = -1;
            if (frameCheck) {
                info.TargetDirection = 1;
            }

            int xPos = tile.TileFrameX / 18;
            if (xPos == 1) {
                i--;
            }
            if (xPos == 2) {
                i++;
            }

            info.AnchorTilePosition.X = i + (frameCheck ? -1 : 1);
            info.AnchorTilePosition.Y = j;

            if (tile.TileFrameY % 40 == 0) {
                info.AnchorTilePosition.Y++;
            }
        }

        public override bool RightClick(int i, int j) {
            Player player = Main.LocalPlayer;

            if (player.IsWithinSnappngRangeToTile(i, j, PlayerSittingHelper.ChairSittingMaxDistance)) {
                player.GamepadEnableGrappleCooldown();
                player.sitting.SitDown(player, i, j);
            }

            return base.RightClick(i, j);
        }

        public override void MouseOver(int i, int j) {
            Player player = Main.LocalPlayer;
            player.noThrow = 2;
            player.mouseInterface = true;
            player.cursorItemIconEnabled = true;
            player.cursorItemIconID = ModContent.ItemType<InfiniteToiletItem>();//当玩家鼠标悬停在物块之上时，显示该物品的材质
        }

        public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) => settings.player.IsWithinSnappngRangeToTile(i, j, PlayerSittingHelper.ChairSittingMaxDistance);

        public override void HitWire(int i, int j) {
            Tile tile = Main.tile[i, j];

            int spawnX = i;
            int spawnY = j - tile.TileFrameY % 40 / 18;

            Wiring.SkipWire(spawnX, spawnY);
            Wiring.SkipWire(spawnX, spawnY + 1);

            if (Wiring.CheckMech(spawnX, spawnY, 60)) { }
        }
    }
}
