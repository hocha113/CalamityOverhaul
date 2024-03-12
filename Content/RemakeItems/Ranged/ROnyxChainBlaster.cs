using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria.ModLoader;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class ROnyxChainBlaster : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<OnyxChainBlaster>();
        public override int ProtogenesisID => ModContent.ItemType<OnyxChainBlasterEcType>();
        public override string TargetToolTipItemName => "OnyxChainBlasterEcType";
        public override void SetDefaults(Item item) => item.SetCartridgeGun<OnyxChainBlasterHeldProj>(120);
    }
}
