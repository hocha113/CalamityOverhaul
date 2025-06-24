using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RFlakToxicannon : CWRItemOverride
    {
        public override int TargetID => ModContent.ItemType<FlakToxicannon>();
        public override void SetDefaults(Item item) {
            item.damage = 62;
            item.SetCartridgeGun<FlakToxicannonHeldProj>(80);
            item.CWR().Scope = true;
        }
    }
}
