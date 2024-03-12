using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria.ModLoader;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RSpyker : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<Spyker>();
        public override int ProtogenesisID => ModContent.ItemType<SpykerEcType>();
        public override string TargetToolTipItemName => "SpykerEcType";
        public override void SetDefaults(Item item) => item.SetCartridgeGun<SpykerHeldProj>(60);
    }
}
