using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria.ModLoader;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RFlarewingBow : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.FlarewingBow>();
        public override int ProtogenesisID => ModContent.ItemType<FlarewingBowEcType>();
        public override string TargetToolTipItemName => "FlarewingBowEcType";
        public override void SetDefaults(Item item) => item.SetHeldProj<FlarewingBowHeldProj>();
    }
}
