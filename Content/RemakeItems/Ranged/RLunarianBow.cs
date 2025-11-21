using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RLunarianBow : CWRItemOverride
    {
        public override void SetDefaults(Item item) {
            item.damage = 15;
            item.SetHeldProj<LunarianBowHeldProj>();
        }
    }
}
