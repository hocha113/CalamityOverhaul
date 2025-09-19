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

    internal class DyeVatTP : TileProcessor
    {
        public override int TargetTileID => TileID.DyeVat;
        
        public void RightClick(Player player) {
            if (player.whoAmI != Main.myPlayer) {
                return;
            }

            if (DyeVatUI.Instance.DyeVatTP == this) {
                DyeVatUI.Instance.CanOpen = !DyeVatUI.Instance.CanOpen;
            }
            else {
                DyeVatUI.Instance.DyeVatTP = this;
                DyeVatUI.Instance.CanOpen = true;
            }
            SpectrometerUI.Instance.CanOpen = false;//关闭其他同类UI
        }

        public override void Update() {
            if (Main.LocalPlayer.DistanceSQ(CenterInWorld) > 90000) {
                DyeVatUI.Instance.CanOpen = false;
            }
            DyeVatUI.Instance.DyeSlot.UpdateSlot();
        }
    }
}
