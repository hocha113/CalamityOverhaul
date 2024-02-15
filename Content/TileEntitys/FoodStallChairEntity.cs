using Terraria.ID;
using Terraria;
using Terraria.ModLoader;
using CalamityOverhaul.Content.Tiles;
using Microsoft.Xna.Framework;

namespace CalamityOverhaul.Content.TileEntitys
{
    internal class FoodStallChairEntity : ModTileEntity
    {
        public Vector2 Center => Position.ToWorldCoordinates(16, 16);
        public long Time = 0;
        public float rot;
        public float drawGstPos;

        public override bool IsTileValidForEntity(int x, int y) {
            Tile tile = Main.tile[x, y];
            return false;
        }

        public override void Update() {
            Main.NewText(13);
            Main.LocalPlayer.CWR().inFoodStallChair = Main.LocalPlayer.Center.To(Center).LengthSquared() < 300 * 300;
            Time++;
        }

        public override int Hook_AfterPlacement(int i, int j, int type, int style, int direction, int alternate) {
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                NetMessage.SendTileSquare(Main.myPlayer, i, j, 2, 2);
                NetMessage.SendData(MessageID.TileEntityPlacement, -1, -1, null, i, j, Type);
                return -1;
            }

            int id = Place(i, j);
            return id;
        }

        public override void OnNetPlace() => NetMessage.SendData(MessageID.TileEntitySharing, -1, -1, null, ID, Position.X, Position.Y);
    }
}
