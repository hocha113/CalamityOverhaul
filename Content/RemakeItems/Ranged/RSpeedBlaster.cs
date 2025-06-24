using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RSpeedBlaster : CWRItemOverride
    {
        public override int TargetID => ModContent.ItemType<SpeedBlaster>();

        public override void SetDefaults(Item item) {
            item.useAmmo = AmmoID.Bullet;
            item.SetCartridgeGun<SpeedBlasterHeldProj>(80);
        }
    }
}
