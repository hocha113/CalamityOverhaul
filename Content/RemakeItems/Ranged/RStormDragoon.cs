using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RStormDragoon : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<StormDragoon>();

        public override void SetDefaults(Item item) {
            item.damage = 68;
            item.SetCartridgeGun<StormDragoonHeldProj>(225);
        }
        public override bool? On_CanConsumeAmmo(Item weapon, Item ammo, Player player) => Main.rand.NextFloat() > 0.2f;
    }
}
