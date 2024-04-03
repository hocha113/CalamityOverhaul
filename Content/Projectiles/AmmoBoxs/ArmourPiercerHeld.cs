using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Placeable;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.AmmoBoxs
{
    internal class ArmourPiercerHeld : BaseHeldBox
    {
        public override string Texture => CWRConstant.Item + "Placeable/HEATBoxHeld";
        public override void SetBox() {
            TargetItemID = ModContent.ItemType<ArmourPiercerBox>();
            AmmoBoxID = ModContent.ProjectileType<ArmourPiercerBoxProj>();
            MaxCharge = 350;
        }
    }
}
