using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RThePack : CWRItemOverride
    {
        public override int TargetID => ModContent.ItemType<ThePack>();
        public override void SetDefaults(Item item) => item.SetCartridgeGun<ThePackHeldProj>(12);
    }
}
