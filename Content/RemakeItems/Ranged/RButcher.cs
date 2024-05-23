using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RButcher : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<Butcher>();
        public override int ProtogenesisID => ModContent.ItemType<ButcherEcType>();
        public override string TargetToolTipItemName => "ButcherEcType";
        public override void SetDefaults(Item item) => item.SetCartridgeGun<ButcherHeldProj>(58);
    }
}
