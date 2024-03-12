using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria.ModLoader;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RFirestormCannon : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<FirestormCannon>();
        public override int ProtogenesisID => ModContent.ItemType<FirestormCannonEcType>();
        public override string TargetToolTipItemName => "FirestormCannonEcType";
        public override void SetDefaults(Item item) => item.SetCartridgeGun<FirestormCannonHeldProj>(60);
    }
}
