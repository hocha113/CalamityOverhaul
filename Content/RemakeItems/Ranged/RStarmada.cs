using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria.ModLoader;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RStarmada : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<Starmada>();
        public override int ProtogenesisID => ModContent.ItemType<StarmadaEcType>();
        public override string TargetToolTipItemName => "StarmadaEcType";
        public override void SetDefaults(Item item) => item.SetCartridgeGun<StarmadaHeldProj>(180);
    }
}
