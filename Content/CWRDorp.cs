using CalamityOverhaul.Common;
using System;
using Terraria;
using Terraria.GameContent.ItemDropRules;

namespace CalamityOverhaul.Content
{
    public class CWRDorp
    {
        /// <summary>
        /// 根据普通模式和专家模式设置，创建一个基于掉落的对象
        /// </summary>
        /// <param name="itemID">物品的ID</param>
        /// <param name="dropRateInt">掉落率整数</param>
        /// <param name="minNormal">普通模式下的最小数量</param>
        /// <param name="maxNormal">普通模式下的最大数量</param>
        /// <param name="minExpert">专家模式下的最小数量</param>
        /// <param name="maxExpert">专家模式下的最大数量</param>
        public static DropBasedOnExpertMode Quantity(int itemID, int dropRateInt, int minNormal, int maxNormal, int minExpert, int maxExpert) {
            // 创建普通模式的掉落规则
            IItemDropRule normalRule = ItemDropRule.Common(itemID, dropRateInt, minNormal, maxNormal);
            // 创建专家模式的掉落规则
            IItemDropRule expertRule = ItemDropRule.Common(itemID, dropRateInt, minExpert, maxExpert);
            // 返回基于普通和专家模式的掉落设置
            return new DropBasedOnExpertMode(normalRule, expertRule);
        }

        internal class DropRuleCondition : IItemDropRuleCondition
        {
            private readonly Func<DropAttemptInfo, bool> conditionLambda;
            private readonly Func<bool> visibleInUI;
            private readonly Func<string> description;

            internal DropRuleCondition(Func<DropAttemptInfo, bool> lambda, Func<bool> showUI, Func<string> text) {
                conditionLambda = lambda;
                visibleInUI = showUI;
                description = text;
            }

            public bool CanDrop(DropAttemptInfo info) => conditionLambda(info);
            public bool CanShowItemDropInUI() => visibleInUI();
            public string GetConditionDescription() => description();
        }

        public static IItemDropRuleCondition Rule(Func<DropAttemptInfo, bool> lambda, Func<bool> showUi, Func<string> text) {
            return new DropRuleCondition(lambda, showUi, text);
        }

        public static IItemDropRuleCondition InHellDropRule =>
            Rule((info) => Main.LocalPlayer.ZoneUnderworldHeight, () => true, () => CWRLocalizationText.GetTextValue("Drop_Hell_RuleText"));

        public static IItemDropRuleCondition GlodDragonDropRule =>
            Rule((info) => InWorldBossPhase.Downed28.Invoke(), () => true, () => CWRLocalizationText.GetTextValue("Drop_GlodDragonDrop_RuleText"));
    }
}
