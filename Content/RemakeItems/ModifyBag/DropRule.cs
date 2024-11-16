using CalamityMod;
using CalamityMod.NPCs.ExoMechs.Thanatos;
using CalamityOverhaul.Common;
using Terraria.GameContent.ItemDropRules;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.ModifyBag
{
    internal class DropRule_Yharon_Down : IItemDropRuleCondition, IProvideItemConditionDescription
    {
        bool IItemDropRuleCondition.CanDrop(DropAttemptInfo info) => InWorldBossPhase.YharonKillCount >= 2;
        bool IItemDropRuleCondition.CanShowItemDropInUI() => true;
        string IProvideItemConditionDescription.GetConditionDescription() => CWRLocText.GetTextValue("Drop_GlodDragonDrop_RuleText");
    }

    internal class Drop_Thanatos_Down : IItemDropRuleCondition, IProvideItemConditionDescription
    {
        bool IItemDropRuleCondition.CanDrop(DropAttemptInfo info) {
            if (info.npc == null) {
                return true;
            }
            return info.npc.type == ModContent.NPCType<ThanatosHead>() || DownedBossSystem.downedThanatos;
        }
        bool IItemDropRuleCondition.CanShowItemDropInUI() => true;
        string IProvideItemConditionDescription.GetConditionDescription() => null;
    }
}
