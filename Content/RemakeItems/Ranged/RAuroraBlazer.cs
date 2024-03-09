using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria.ModLoader;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RAuroraBlazer : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<AuroraBlazer>();
        public override int ProtogenesisID => ModContent.ItemType<AuroraBlazerEcType>();
        public override string TargetToolTipItemName => "AuroraBlazerEcType";
        public override void SetDefaults(Item item) => item.SetCartridgeGun<AuroraBlazerHeldProj>(660);
    }
}
