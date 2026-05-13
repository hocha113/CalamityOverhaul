using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.AmmoBoxs
{
    internal class NapalmBombHeld : BaseHeldBox
    {
        public override void SetBox() {
            TargetItemID = ModContent.ItemType<AmmoBoxFire>();
            AmmoBoxID = ModContent.ProjectileType<NapalmBombBox>();
            MaxCharge = 40;
        }
    }
}
