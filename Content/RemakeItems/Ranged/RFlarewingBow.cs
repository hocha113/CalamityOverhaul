using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RFlarewingBow : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<FlarewingBow>();
        public override int ProtogenesisID => ModContent.ItemType<FlarewingBowEcType>();
        public override string TargetToolTipItemName => "FlarewingBowEcType";
        public override void SetDefaults(Item item) => item.SetHeldProj<FlarewingBowHeldProj>();
    }
}
