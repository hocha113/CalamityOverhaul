using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RSomaPrime : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<SomaPrime>();
        public override void SetDefaults(Item item) {
            item.damage = 300;
            item.SetCartridgeGun<SomaPrimeHeldProj>(600);
        }
        public override bool? On_CanConsumeAmmo(Item weapon, Item ammo, Player player) => Main.rand.NextFloat() > 0.1f;
    }
}
