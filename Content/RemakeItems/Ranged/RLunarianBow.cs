using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RLunarianBow : CWRItemOverride
    {
        public override int TargetID => ModContent.ItemType<LunarianBow>();
        public override void SetDefaults(Item item) {
            item.damage = 15;
            item.SetHeldProj<LunarianBowHeldProj>();
        }
    }
}
