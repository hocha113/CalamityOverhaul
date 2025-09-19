using CalamityOverhaul.Content.Industrials.ElectricPowers;
using CalamityOverhaul.Content.UIs;
using InnoVault.GameSystem;
using InnoVault.TileProcessors;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Industrials.Modifys
{
    internal class ModifyDyeVat : TileOverride
    {
        public override int TargetID => TileID.DyeVat;
        public override bool? RightClick(int i, int j, Tile tile) {
            if (TPUtils.TryGetTPAt(i, j, out DyeVatTP tp)) {
                tp.RightClick(Main.LocalPlayer);
            }
            return base.RightClick(i, j, tile);
        }

        public override void MouseOver(int i, int j) => Main.LocalPlayer.SetMouseOverByTile(ItemID.DyeVat);
    }

    internal class DyeVatTP : BaseDyeTP
    {
        public override int TargetTileID => TileID.DyeVat;
        public override bool CanDrop => false;
        public override bool ReceivedEnergy => true;
        public override float MaxUEValue => 0;
        public override BaseDyeMachineUI DyeMachineUI => DyeVatUI.Instance;
    }
}
