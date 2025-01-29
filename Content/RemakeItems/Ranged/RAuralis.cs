using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RAuralis : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<Auralis>();
        public override int ProtogenesisID => ModContent.ItemType<AuralisEcType>();
        public override string TargetToolTipItemName => "AuralisEcType";
        public override void SetDefaults(Item item) {
            item.SetCartridgeGun<AuralisHeldProj>(18);
            item.CWR().Scope = true;
        }
    }
}
