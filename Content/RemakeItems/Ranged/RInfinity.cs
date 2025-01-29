using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RInfinity : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<Infinity>();
        public override int ProtogenesisID => ModContent.ItemType<InfinityEcType>();
        public override string TargetToolTipItemName => "InfinityEcType";
        public override void SetDefaults(Item item) => InfinityEcType.SetDefaultsFunc(item);
        public override bool? On_CanConsumeAmmo(Item weapon, Item ammo, Player player) => true;
    }
}
