using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RFlakKraken : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<FlakKraken>();
        public override int ProtogenesisID => ModContent.ItemType<FlakKrakenEcType>();
        public override string TargetToolTipItemName => "FlakKrakenEcType";
        public override void SetDefaults(Item item) {
            item.damage = 84;
            item.SetCartridgeGun<FlakKrakenHeldProj>(80);
            item.CWR().Scope = true;
        }
    }
}
