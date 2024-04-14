using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria.ModLoader;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RLunarianBow : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.LunarianBow>();
        public override int ProtogenesisID => ModContent.ItemType<LunarianBowEcType>();
        public override string TargetToolTipItemName => "LunarianBowEcType";
        public override void SetDefaults(Item item) => item.SetHeldProj<LunarianBowHeldProj>();
    }
}
