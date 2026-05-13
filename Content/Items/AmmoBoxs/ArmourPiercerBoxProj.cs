using Terraria;

namespace CalamityOverhaul.Content.Items.AmmoBoxs
{
    internal class ArmourPiercerBoxProj : BaseAmmoBox
    {
        public override string Texture => CWRConstant.Item + "Placeable/HEATBox";
        public override bool ClickBehavior(Player player, CWRItem cwr) {
            return false;
        }
    }
}
