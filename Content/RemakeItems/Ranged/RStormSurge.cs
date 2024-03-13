using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria.ModLoader;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RStormSurge : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<StormSurge>();
        public override int ProtogenesisID => ModContent.ItemType<StormSurgeEcType>();
        public override string TargetToolTipItemName => "StormSurgeEcType";
        public override void SetDefaults(Item item) => item.SetHeldProj<StormSurgeHeldProj>();
    }
}
