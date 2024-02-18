using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    internal class RZenith : BaseRItem
    {
        public override int TargetID => ItemID.Zenith;
        public override bool FormulaSubstitution => false;
        public override void On_PostSetDefaults(Item item) {
            item.damage += 128;
        }
    }
}
