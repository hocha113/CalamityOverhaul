using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RArbalest : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<Arbalest>();
        public override int ProtogenesisID => ModContent.ItemType<ArbalestEcType>();
        public override string TargetToolTipItemName => "ArbalestEcType";
        public override void SetDefaults(Item item) {
            item.SetHeldProj<ArbalestHeldProj>();
            item.CWR().Scope = true;
        }
    }
}
