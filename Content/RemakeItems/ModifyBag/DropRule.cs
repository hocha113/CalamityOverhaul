using CalamityOverhaul.Common;
using CalamityOverhaul.OtherMods.InfernumMode;
using Terraria.GameContent.ItemDropRules;

namespace CalamityOverhaul.Content.RemakeItems.ModifyBag
{
    internal class Drop_Thanatos_Down : IItemDropRuleCondition, IProvideItemConditionDescription
    {
        bool IItemDropRuleCondition.CanDrop(DropAttemptInfo info) {
            if (info.npc == null) {
                return true;
            }
            return info.npc.type == CWRID.NPC_ThanatosHead || CWRRef.GetDownedThanatos();
        }
        bool IItemDropRuleCondition.CanShowItemDropInUI() => true;
        string IProvideItemConditionDescription.GetConditionDescription() => null;
    }

    public class DropInDeathMode : IItemDropRuleCondition, IProvideItemConditionDescription
    {
        public bool CanDrop(DropAttemptInfo info) => CWRWorld.Death;
        public bool CanShowItemDropInUI() => CWRWorld.Death || InfernumRef.InfernumModeOpenState;
        public string GetConditionDescription() => CWRLocText.Instance.DeathModeItem.Value;
    }

    public class DropInMachineRebellion : IItemDropRuleCondition, IProvideItemConditionDescription
    {
        public bool CanDrop(DropAttemptInfo info) => CWRWorld.MachineRebellion;
        public bool CanShowItemDropInUI() => CWRWorld.MachineRebellion;
        public string GetConditionDescription() => CWRLocText.Instance.DropInMachineRebellion.Value;
    }
}
