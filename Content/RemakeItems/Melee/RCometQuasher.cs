using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RCometQuasher : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<CometQuasher>();
        public override void SetDefaults(Item item) => CometQuasherEcType.SetDefaultsFunc(item);
    }
}
