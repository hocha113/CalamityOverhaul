using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RRealmRavager : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<RealmRavager>();
        public override int ProtogenesisID => ModContent.ItemType<RealmRavagerEcType>();
        public override string TargetToolTipItemName => "RealmRavagerEcType";
        public override void SetDefaults(Item item) => item.SetCartridgeGun<RealmRavagerHeldProj>(180);
    }
}
