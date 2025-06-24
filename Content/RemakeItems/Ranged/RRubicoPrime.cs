using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RRubicoPrime : CWRItemOverride
    {
        public override int TargetID => ModContent.ItemType<RubicoPrime>();
        public override void SetDefaults(Item item) {
            item.damage = 820;
            item.useTime = 20;
            item.SetCartridgeGun<RubicoPrimeHeldProj>(80);
        }

    }
}
