using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RVoidragon : CWRItemOverride
    {
        public override int TargetID => ModContent.ItemType<Voidragon>();
        public override void SetDefaults(Item item) => item.SetCartridgeGun<VoidragonHeldProj>(800);
    }
}
