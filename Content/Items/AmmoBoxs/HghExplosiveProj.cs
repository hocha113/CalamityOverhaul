using Terraria;

namespace CalamityOverhaul.Content.Items.AmmoBoxs
{
    internal class HghExplosiveProj : BaseAmmoBox
    {
        public override string Texture => CWRConstant.Item + "Placeable/HghExplosiveBox";
        public override bool ClickBehavior(Player player, CWRItem cwr) {
            return false;
        }
    }
}
