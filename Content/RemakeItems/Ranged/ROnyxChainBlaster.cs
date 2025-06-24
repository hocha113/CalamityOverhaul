using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class ROnyxChainBlaster : CWRItemOverride
    {
        public override int TargetID => ModContent.ItemType<OnyxChainBlaster>();
        public override void SetDefaults(Item item) {
            item.damage = 58;
            item.SetCartridgeGun<OnyxChainBlasterHeldProj>(100);
        }
        public override bool? On_CanConsumeAmmo(Item weapon, Item ammo, Player player) => Main.rand.NextFloat() > 0.1f;
    }
}
