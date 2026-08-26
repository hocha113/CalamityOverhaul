using InnoVault.TileProcessors;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent.ObjectInteractions;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.BottlingMachines
{
    internal class BottlingMachineTile : ModTile
    {
        public override string Texture => CWRConstant.Asset + "ElectricPowers/BottlingMachineTile";
        public override void SetStaticDefaults() {
            Main.tileLighted[Type] = true;
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;
            Main.tileSolidTop[Type] = true;
            AddMapEntry(new Color(96, 128, 96), VaultUtils.GetLocalizedItemName<BottlingMachine>());

            TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
            TileObjectData.newTile.Width = 2;
            TileObjectData.newTile.Height = 2;
            TileObjectData.newTile.Origin = new Point16(1, 1);
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile
                | AnchorType.SolidWithTop | AnchorType.SolidSide, TileObjectData.newTile.Width, 0);
            TileObjectData.newTile.CoordinateHeights = [16, 18];
            TileObjectData.newTile.LavaDeath = false;

            TileObjectData.addTile(Type);
        }

        public override void MouseOver(int i, int j) {
            Item item = Main.LocalPlayer.GetItem();
            int type = ModContent.ItemType<BottlingMachine>();
            if (BottlingRecipes.CanProcess(item)) {
                type = item.type;
            }
            Main.LocalPlayer.SetMouseOverByTile(type);
        }

        public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) => true;

        public override bool RightClick(int i, int j) {
            if (!VaultUtils.SafeGetTopLeft(i, j, out var topLeft)) {
                return false;
            }
            if (!TileProcessorLoader.ByPositionGetTP(topLeft, out BottlingMachineTP tp)) {
                return false;
            }
            tp.RightClickByTile();
            return true;
        }

        public override bool CreateDust(int i, int j, ref int type) {
            Dust.NewDust(new Vector2(i, j) * 16f, 16, 16, DustID.Glass);
            return false;
        }

        public override void NumDust(int i, int j, bool fail, ref int num) => num = fail ? 1 : 3;

        public override bool CanDrop(int i, int j) => false;
    }
}
