using System.Linq;
using Terraria;

namespace CalamityOverhaul.Content.ADV.Scenarios.Shepel
{
    /// <summary>
    /// SHPC HUD 对话按钮路由，按 <see cref="SHPCDialogueScenarioBase.DialoguePriority"/> 取首个可触发场景
    /// </summary>
    internal static class SHPCDialogueRouter
    {
        /// <summary>
        /// 启动最高优先级可用对话；已有场景运行则 false
        /// </summary>
        public static bool TryStart(Player player) {
            if (ScenarioManager.IsActive()) {
                return false;
            }
            ADVSave save = player.GetModPlayer<ADVSavePlayer>().ADVSave;
            foreach (SHPCDialogueScenarioBase scenario in ADVScenarioBase.Instances
                .OfType<SHPCDialogueScenarioBase>()
                .OrderByDescending(s => s.DialoguePriority)) {
                if (scenario.CanBeRoutedTo(player, save)) {
                    if (scenario.StartScenario()) {
                        return true;
                    }
                    return false;
                }
            }
            return false;
        }
    }
}
