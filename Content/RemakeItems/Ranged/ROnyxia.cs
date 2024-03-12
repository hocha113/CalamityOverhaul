using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria.ModLoader;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class ROnyxia : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<Onyxia>();
        public override int ProtogenesisID => ModContent.ItemType<OnyxiaEcType>();
        public override string TargetToolTipItemName => "OnyxiaEcType";
        public override void SetDefaults(Item item) => item.SetCartridgeGun<OnyxiaHeldProj>(180);
    }
}
