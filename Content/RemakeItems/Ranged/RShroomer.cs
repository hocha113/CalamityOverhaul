using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RShroomer : CWRItemOverride
    {
        public override int TargetID => ModContent.ItemType<Shroomer>();
        public override void SetDefaults(Item item) {
            item.damage = 135;
            item.SetCartridgeGun<ShroomerHeldProj>(12);
            item.CWR().Scope = true;
        }
    }
}
