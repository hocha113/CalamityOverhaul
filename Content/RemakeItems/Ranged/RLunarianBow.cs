using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RLunarianBow : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<LunarianBow>();
        public override int ProtogenesisID => ModContent.ItemType<LunarianBowEcType>();
        public override string TargetToolTipItemName => "LunarianBowEcType";
        public override void SetDefaults(Item item) {
            item.damage = 15;
            item.SetHeldProj<LunarianBowHeldProj>();
        }
    }
}
