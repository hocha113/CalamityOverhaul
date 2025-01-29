using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RBurntSienna : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<BurntSienna>();
        public override void SetDefaults(Item item) => BurntSiennaEcType.SetDefaultsFunc(item);
    }
}
