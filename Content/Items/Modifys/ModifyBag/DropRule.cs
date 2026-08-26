using CalamityOverhaul.Content.GameModes;
using CalamityOverhaul.OtherMods.InfernumMode;
using Terraria.GameContent.ItemDropRules;

namespace CalamityOverhaul.Content.Items.Modifys.ModifyBag
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
        public string GetConditionDescription() => CWRItem.DeathModeItemText.Value;
    }

    /// <summary>残酷遗物系列掉落条件：世界残酷旗标关闭时不掉落也不在图鉴显示</summary>
    public class DropInBrutalMode : IItemDropRuleCondition, IProvideItemConditionDescription
    {
        public bool CanDrop(DropAttemptInfo info) => GameModeSystem.BrutalActive;
        public bool CanShowItemDropInUI() => GameModeSystem.BrutalActive;
        public string GetConditionDescription() => CWRItem.BrutalModeItemText.Value;
    }
}
