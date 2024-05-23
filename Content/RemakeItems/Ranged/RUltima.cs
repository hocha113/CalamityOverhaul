using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RUltima : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.Ultima>();
        public override int ProtogenesisID => ModContent.ItemType<UltimaEcType>();
        public override string TargetToolTipItemName => "UltimaEcType";
        public override void SetDefaults(Item item) => item.SetHeldProj<UltimaHeldProj>();
    }
}
