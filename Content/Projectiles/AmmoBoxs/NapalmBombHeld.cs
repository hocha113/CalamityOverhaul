using CalamityOverhaul.Content.Items.Placeable;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.AmmoBoxs
{
    internal class NapalmBombHeld : BaseHeldBox
    {
        public override void SetBox() {
            TargetItemID = ModContent.ItemType<AmmoBoxFire>();
            AmmoBoxID = ModContent.ProjectileType<NapalmBombBox>();
            MaxCharge = 300;
        }
    }
}
