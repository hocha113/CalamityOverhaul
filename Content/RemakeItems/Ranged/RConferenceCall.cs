using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RConferenceCall : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<ConferenceCall>();
        public override int ProtogenesisID => ModContent.ItemType<ConferenceCallEcType>();
        public override string TargetToolTipItemName => "ConferenceCallEcType";
        public override void SetDefaults(Item item) => item.SetCartridgeGun<ConferenceCallHeldProj>(85);
    }
}
