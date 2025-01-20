using CalamityMod.Buffs.StatDebuffs;
using CalamityMod.Items.Accessories;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems
{
    internal class RRustyMedallion : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<RustyMedallion>();
        public override bool DrawingInfo => false;
        public override bool FormulaSubstitution => false;
        public override bool On_UpdateAccessory(Item item, Player player, bool hideVisual) {
            player.CWR().RustyMedallion_Value = true;
            player.buffImmune[ModContent.BuffType<Irradiated>()] = true;
            return false;
        }
    }
}
