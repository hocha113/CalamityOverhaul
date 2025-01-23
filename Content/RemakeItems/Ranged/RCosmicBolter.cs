using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RCosmicBolter : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<VernalBolter>();
        public override int ProtogenesisID => ModContent.ItemType<CosmicBolterEcType>();
        public override string TargetToolTipItemName => "CosmicBolterEcType";
        public override void SetDefaults(Item item) => item.SetHeldProj<CosmicBolterHeldProj>();
    }
}
