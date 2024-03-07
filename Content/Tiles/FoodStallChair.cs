using CalamityOverhaul.Common;
using CalamityOverhaul.Content.TileEntitys;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent.ObjectInteractions;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.Tiles
{
    internal class FoodStallChair : ModTile
    {
        public override string Texture => CWRConstant.Asset + "Tiles/" + "FoodStallChair";
        public bool playerInR;
        public override void SetStaticDefaults() {
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = true;
            TileID.Sets.HasOutlines[Type] = true;

            TileID.Sets.CanBeSatOnForNPCs[Type] = true; // 方便为NPC调用ModifySittingTargetInfo
            TileID.Sets.CanBeSatOnForPlayers[Type] = true; // 方便为玩家调用ModifySittingTargetInfo

            TileID.Sets.DisableSmartCursor[Type] = true;

            AddToArray(ref TileID.Sets.RoomNeeds.CountsAsChair);

            AdjTiles = new int[] { TileID.Chairs };

            AddMapEntry(new Color(200, 200, 200), Language.GetText("MapObject.Chair"));

            TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
            TileObjectData.newTile.CoordinateHeights = new[] { 16, 16 };
            TileObjectData.newTile.CoordinatePaddingFix = new Point16(0, 2);
            //我不明白为什么要设置这个
            //TileObjectData.newTile.Direction = TileObjectDirection.PlaceLeft;
            // 如果决定添加更多样式的垂直堆叠，则需要设置这三行代码以下3行代码
            //TileObjectData.newTile.StyleWrapLimit = 2;
            //TileObjectData.newTile.StyleMultiplier = 2;
            //TileObjectData.newTile.StyleHorizontal = true;

            TileObjectData.newAlternate.CopyFrom(TileObjectData.newTile);
            TileObjectData.newAlternate.Direction = TileObjectDirection.PlaceRight;
            TileObjectData.addTile(Type);
        }

        public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) {//如果返回了true，那么这个物块项目就能够被玩家交互，这里判定距离来防止发生无限距离交互的情况
            return settings.player.IsWithinSnappngRangeToTile(i, j, 180);// 避免能够从远处触发它
        }

        public override void ModifySittingTargetInfo(int i, int j, ref TileRestingInfo info) {
            info.AnchorTilePosition.X = i;
            info.AnchorTilePosition.Y = j;
        }

        public override void RandomUpdate(int i, int j) {
            base.RandomUpdate(i, j);
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
            
            if (!player.IsWithinSnappngRangeToTile(i, j, 180)) { // 匹配RightClick中条件。仅当单击时执行某些操作时才应显示交互
                return;
            }

            player.noThrow = 2;
            player.mouseInterface = true;
            player.cursorItemIconEnabled = true;
            player.cursorItemIconID = ModContent.ItemType<Items.Placeable.FoodStallChair>();//当玩家鼠标悬停在物块之上时，显示该物品的材质

            if (Main.tile[i, j].TileFrameX / 18 < 1) {
                player.cursorItemIconReversed = true;
            }
        }

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) {
            Tile t = Main.tile[i, j];
            int frameXPos = t.TileFrameX;
            int frameYPos = t.TileFrameY;
            Texture2D tex = ModContent.Request<Texture2D>(Texture).Value;
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
