using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria.ModLoader;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RPlagueTaintedSMG : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<PlagueTaintedSMG>();
        public override int ProtogenesisID => ModContent.ItemType<PlagueTaintedSMGEcType>();
        public override string TargetToolTipItemName => "PlagueTaintedSMGEcType";
        public override void SetDefaults(Item item) => item.SetCartridgeGun<PlagueTaintedSMGHeldProj>(45);
    }
}
