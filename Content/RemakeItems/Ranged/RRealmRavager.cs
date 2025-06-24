using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RRealmRavager : CWRItemOverride
    {
        public override int TargetID => ModContent.ItemType<RealmRavager>();
        public override void SetDefaults(Item item) => item.SetCartridgeGun<RealmRavagerHeldProj>(180);
    }
}
