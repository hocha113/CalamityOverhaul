using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RClockGatlignum : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<ClockGatlignum>();
        public override int ProtogenesisID => ModContent.ItemType<ClockGatlignumEcType>();
        public override string TargetToolTipItemName => "ClockGatlignumEcType";
        public override void SetDefaults(Item item) {
            item.SetCartridgeGun<ClockGatlignumHeldProj>(185);
        }
    }
}
