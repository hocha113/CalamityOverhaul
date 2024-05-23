using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RHandheldTank : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<HandheldTank>();
        public override int ProtogenesisID => ModContent.ItemType<HandheldTankEcType>();
        public override string TargetToolTipItemName => "HandheldTankEcType";
        public override void SetDefaults(Item item) => item.SetCartridgeGun<HandheldTankHeldProj>(12);
    }
}
