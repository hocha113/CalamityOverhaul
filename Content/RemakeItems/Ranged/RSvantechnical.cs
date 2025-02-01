using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RSvantechnical : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<Svantechnical>();

        public override void SetDefaults(Item item) {
            item.SetCartridgeGun<SvantechnicalHeldProj>(880);
            item.CWR().Scope = true;
        }

        public override bool? On_CanConsumeAmmo(Item weapon, Item ammo, Player player) => true;
    }
}
