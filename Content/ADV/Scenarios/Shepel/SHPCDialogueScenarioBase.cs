using Terraria;

namespace CalamityOverhaul.Content.ADV.Scenarios.Shepel
{
    /// <summary>
    /// SHPC 对话按钮可触发的场景基类，由 <see cref="SHPCDialogueRouter"/> 路由
    /// </summary>
    internal abstract class SHPCDialogueScenarioBase : ADVScenarioBase
    {
        /// <summary>
        /// 路由优先级，越大越先检查，默认 0
        /// </summary>
        public virtual int DialoguePriority => 0;

        /// <summary>
        /// 最低 <see cref="ShepelADVData.StoryPhase"/>，未达则跳过路由
        /// </summary>
        public virtual int RequiredPhase => 0;

        /// <summary>
        /// 子类额外触发条件，阶段门控在基类
        /// </summary>
        protected virtual bool CheckConditions(Player player, ADVSave save) => true;

        /// <summary>
        /// 路由入口，非虚以免绕过阶段门控
        /// </summary>
        public bool CanBeRoutedTo(Player player, ADVSave save) {
            ShepelADVData data = save.Get<ShepelADVData>();

            //故事阶段门控
            if (data.StoryPhase < RequiredPhase) {
                return false;
            }

            return CheckConditions(player, save);
        }
    }
}
