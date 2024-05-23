using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RMalevolence : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.Malevolence>();
        public override int ProtogenesisID => ModContent.ItemType<MalevolenceEcType>();
        public override string TargetToolTipItemName => "MalevolenceEcType";
        public override void SetDefaults(Item item) => item.SetHeldProj<MalevolenceHeldProj>();
    }
}
