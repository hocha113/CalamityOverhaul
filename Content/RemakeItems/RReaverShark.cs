using InnoVault.GameSystem;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems
{
    internal class RReaverShark : ItemOverride
    {
        public override int TargetID => ItemID.ReaverShark;
        public override bool CanLoadLocalization => false;
        public override void SetDefaults(Item item) {
            item.pick = 60;
            item.axe = 10;
            item.useTime = 4;           
        }
    }
}
