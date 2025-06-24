using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RFlakKraken : CWRItemOverride
    {
        public override int TargetID => ModContent.ItemType<FlakKraken>();

        public override void SetDefaults(Item item) {
            item.damage = 84;
            item.SetCartridgeGun<FlakKrakenHeldProj>(80);
            item.CWR().Scope = true;
        }
    }
}
