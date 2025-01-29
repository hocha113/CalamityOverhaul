using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RUltimusCleaver : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<UltimusCleaver>();
        public override void SetDefaults(Item item) => UltimusCleaverEcType.SetDefaultsFunc(item);
    }
}
