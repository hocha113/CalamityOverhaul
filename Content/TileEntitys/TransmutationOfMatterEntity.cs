using Terraria.ID;
using Terraria;
using Terraria.ModLoader;
using CalamityOverhaul.Content.Tiles;
using Microsoft.Xna.Framework;
using CalamityOverhaul.Content.UIs.SupertableUIs;

namespace CalamityOverhaul.Content.TileEntitys
{
    internal class TransmutationOfMatterEntity : ModTileEntity
    {
        public int frameIndex = 1;
        public Vector2 Center => Position.ToWorldCoordinates(8 * TransmutationOfMatter.Width, 8 * TransmutationOfMatter.Height);
        public long Time = 0;
        public float rot;
        public float drawGstPos;

        public override bool IsTileValidForEntity(int x, int y) {
            Tile tile = Main.tile[x, y];
            return tile.HasTile && tile.TileType == ModContent.TileType<TransmutationOfMatter>() && tile.TileFrameX == 0 && tile.TileFrameY == 0;
        }

        public override void Update() {
            if (SupertableUI.Instance.Active) {
                float leng = Main.LocalPlayer.Center.To(Center).LengthSquared();
                if (leng >= 100 * 100 && leng < 1000 * 1000) {
                    SupertableUI.Instance.Active = false;
                }
            }

            CWRUtils.ClockFrame(ref frameIndex, 6, 3);
            Time++;
        }

        public override int Hook_AfterPlacement(int i, int j, int type, int style, int direction, int alternate) {
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                NetMessage.SendTileSquare(Main.myPlayer, i, j, TransmutationOfMatter.Width, TransmutationOfMatter.Height);
                NetMessage.SendData(MessageID.TileEntityPlacement, -1, -1, null, i, j, Type);
                return -1;
            }

            int id = Place(i, j);
            return id;
        }

        public override void OnNetPlace() {
            NetMessage.SendData(MessageID.TileEntitySharing, number: ID, number2: Position.X, number3: Position.Y);
        }
    }
}
