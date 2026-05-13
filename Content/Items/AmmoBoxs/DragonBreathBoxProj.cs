using Terraria;

namespace CalamityOverhaul.Content.Items.AmmoBoxs
{
    internal class DragonBreathBoxProj : BaseAmmoBox
    {
        public override string Texture => CWRConstant.Item + "Placeable/DBCBox";
        public override bool ClickBehavior(Player player, CWRItem cwr) {
            return false;
        }
    }
}
