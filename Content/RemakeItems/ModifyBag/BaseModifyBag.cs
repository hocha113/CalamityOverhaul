using CalamityOverhaul.Content.RemakeItems.Core;

namespace CalamityOverhaul.Content.RemakeItems.ModifyBag
{
    internal abstract class BaseModifyBag : ItemOverride
    {
        public override bool DrawingInfo => false;
        public override bool FormulaSubstitution => false;
    }
}
