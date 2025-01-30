using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RPestilentDefiler : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<PestilentDefiler>();
        public override void SetDefaults(Item item) {
            item.damage = 90;
            item.SetCartridgeGun<PestilentDefilerHeldProj>(65);
        }
    }
}
