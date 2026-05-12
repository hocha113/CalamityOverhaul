using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.MoldProcessingTables.UI;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent.ObjectInteractions;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.MoldProcessingTables
{
    /// <summary>
    /// 模具加工台物块：4 宽 3 高。右键打开 <see cref="MoldProcessingUI"/>，无自身持久化状态
    /// </summary>
    internal class MoldProcessingTableTile : ModTile
    {
        public const int Width = 4;
        public const int Height = 3;

        //贴图沿用本目录下已有的 MoldProcessingTableTile.png
        public override string Texture => "CalamityOverhaul/Content/LegendWeapon/SHPCLegend/MoldProcessingTables/MoldProcessingTableTile";

        public override void SetStaticDefaults() {
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;
            AddMapEntry(new Color(40, 120, 140), VaultUtils.GetLocalizedItemName<MoldProcessingTable>());

            TileObjectData.newTile.CopyFrom(TileObjectData.Style3x3);
            TileObjectData.newTile.Width = Width;
            TileObjectData.newTile.Height = Height;
            TileObjectData.newTile.Origin = new Point16(1, 2);
            TileObjectData.newTile.CoordinateHeights = [16, 16, 16];
            TileObjectData.newTile.CoordinateWidth = 16;
            TileObjectData.newTile.CoordinatePadding = 2;
            TileObjectData.newTile.AnchorBottom = new AnchorData(
                AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide,
                TileObjectData.newTile.Width, 0);
            TileObjectData.newTile.LavaDeath = false;
            TileObjectData.addTile(Type);
        }

        public override bool CreateDust(int i, int j, ref int type) {
            Dust.NewDust(new Vector2(i, j) * 16f, 16, 16, DustID.Electric);
            return false;
        }

        public override void NumDust(int i, int j, bool fail, ref int num) => num = fail ? 1 : 3;

        public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) => true;

        public override void MouseOver(int i, int j) {
            Player player = Main.LocalPlayer;
            player.noThrow = 2;
            player.cursorItemIconEnabled = true;
            player.cursorItemIconID = ModContent.ItemType<MoldProcessingTable>();
        }

        public override bool RightClick(int i, int j) {
            if (Main.netMode == NetmodeID.Server) {
                return false;
            }
            if (!VaultUtils.SafeGetTopLeft(i, j, out Point16 topLeft)) {
                return false;
            }
            MoldProcessingUI.Instance?.Open(topLeft);
            return true;
        }
    }
}
