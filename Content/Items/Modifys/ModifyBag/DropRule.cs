using CalamityOverhaul.Content.GameModes;
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

    /// <summary>残酷世界专属掉落条件（机械三王武器与残酷遗物共用）：世界残酷旗标关闭时不掉落也不在图鉴显示</summary>
    public class DropInBrutalWorld : IItemDropRuleCondition, IProvideItemConditionDescription
    {
        public bool CanDrop(DropAttemptInfo info) => GameModeSystem.BrutalActive;
        public bool CanShowItemDropInUI() => GameModeSystem.BrutalActive;
        public string GetConditionDescription() => CWRItem.BrutalWorldItemText.Value;
    }
}
