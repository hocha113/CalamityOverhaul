using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria.ModLoader;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RSpeedBlaster : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<SpeedBlaster>();
        public override int ProtogenesisID => ModContent.ItemType<SpeedBlasterEcType>();
        public override string TargetToolTipItemName => "SpeedBlasterEcType";
        public override void SetDefaults(Item item) => item.SetCartridgeGun<SpeedBlasterHeldProj>(80);
    }
}
