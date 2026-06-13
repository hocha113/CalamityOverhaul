using Microsoft.Xna.Framework.Graphics;
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
    internal class FoodStallChair : ModTile
    {
        public override string Texture => CWRConstant.Asset + "Tiles/" + "FoodStallChair";
        public override void SetStaticDefaults() {
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = true;
            TileID.Sets.HasOutlines[Type] = true;
            TileID.Sets.CanBeSatOnForNPCs[Type] = true; //供 ModifySittingTargetInfo 读取
            TileID.Sets.CanBeSatOnForPlayers[Type] = true; //供 ModifySittingTargetInfo 读取
            TileID.Sets.DisableSmartCursor[Type] = true;
            AddToArray(ref TileID.Sets.RoomNeeds.CountsAsChair);
            AddMapEntry(new Color(200, 200, 200), Language.GetText("MapObject.Chair"));
            AdjTiles = [TileID.Chairs];
            TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
            TileObjectData.newTile.CoordinateHeights = new[] { 16, 16 };
            TileObjectData.newTile.CoordinatePaddingFix = new Point16(0, 2);
            TileObjectData.newAlternate.CopyFrom(TileObjectData.newTile);
            TileObjectData.newAlternate.Direction = TileObjectDirection.PlaceRight;
            TileObjectData.addAlternate(1);
            TileObjectData.addTile(Type);
        }

        public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) {//距离门限内才允许智能交互
            return settings.player.IsWithinSnappngRangeToTile(i, j, 180);//禁止远距离触发
        }

        public override void ModifySittingTargetInfo(int i, int j, ref TileRestingInfo info) {
            Tile tile = Framing.GetTileSafely(i, j);
            info.ExtraInfo.IsAToilet = true;
            info.TargetDirection = Main.LocalPlayer.direction;
            int xPos = tile.TileFrameX / 18;
            if (xPos == 1) {
                i--;
            }
            if (xPos == 2) {
                i++;
            }

            info.AnchorTilePosition.X = i;
            info.AnchorTilePosition.Y = j;

            if (tile.TileFrameY % 40 == 0) {
                info.AnchorTilePosition.Y++;
            }
        }

        public override bool RightClick(int i, int j) {
            Player player = Main.LocalPlayer;
            if (player.IsWithinSnappngRangeToTile(i, j, 180)) {
                player.GamepadEnableGrappleCooldown();
                player.sitting.SitDown(player, i, j);
            }
            return true;
        }

        public override void MouseOver(int i, int j) {
            Player player = Main.LocalPlayer;

            if (!player.IsWithinSnappngRangeToTile(i, j, 180)) { //与 RightClick 同距，超距不显示交互
                return;
            }

            player.noThrow = 2;
            player.mouseInterface = true;
            player.cursorItemIconEnabled = true;
            player.cursorItemIconID = ModContent.ItemType<Items.Placeable.FoodStallChair>();//悬停显示对应物品图标

            if (Main.tile[i, j].TileFrameX / 18 < 1) {
                player.cursorItemIconReversed = true;
            }
        }

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) {
            Tile t = Main.tile[i, j];
            int frameXPos = t.TileFrameX;
            int frameYPos = t.TileFrameY;
            Texture2D tex = TextureAssets.Tile[Type].Value;
            Vector2 offset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
            Vector2 drawOffset = new Vector2(i * 16 - Main.screenPosition.X, j * 16 - Main.screenPosition.Y) + offset;
            Color drawColor = Lighting.GetColor(i, j);

            if (!t.IsHalfBlock && t.Slope == 0)
                spriteBatch.Draw(tex, drawOffset, new Rectangle(frameXPos, frameYPos, 16, 16)
                    , drawColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
            else if (t.IsHalfBlock)
                spriteBatch.Draw(tex, drawOffset + Vector2.UnitY * 8f, new Rectangle(frameXPos, frameYPos, 16, 16)
                    , drawColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
            return false;
        }
    }
}
