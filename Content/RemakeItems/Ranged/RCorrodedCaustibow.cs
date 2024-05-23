using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RCorrodedCaustibow : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.CorrodedCaustibow>();
        public override int ProtogenesisID => ModContent.ItemType<CorrodedCaustibowEcType>();
        public override string TargetToolTipItemName => "CorrodedCaustibowEcType";
        public override void SetDefaults(Item item) => item.SetHeldProj<CorrodedCaustibowHeldProj>();
    }
}
