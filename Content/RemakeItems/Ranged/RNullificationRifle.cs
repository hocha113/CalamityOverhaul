using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria.ModLoader;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RNullificationRifle : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<NullificationRifle>();
        public override int ProtogenesisID => ModContent.ItemType<NullificationRifleEcType>();
        public override string TargetToolTipItemName => "NullificationRifleEcType";
        public override void SetDefaults(Item item) => item.SetCartridgeGun<NullificationRifleHeldProj>(80);
    }
}
