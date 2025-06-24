using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RNeedler : CWRItemOverride
    {
        public override int TargetID => ModContent.ItemType<Needler>();
        public override void SetDefaults(Item item) {
            item.damage = 40;
            item.SetCartridgeGun<NeedlerHeldProj>(50);
        }
    }
}
