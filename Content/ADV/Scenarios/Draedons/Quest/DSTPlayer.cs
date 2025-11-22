using CalamityOverhaul.Content.ADV.Scenarios.Draedons.Quest.DeploySignaltowers;
using CalamityOverhaul.Content.ADV.Scenarios.Draedons.Quest.DeploySignaltowers.SignalTower;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Draedons.Quest
{
    internal class DSTPlayer : ModPlayer
    {
        public static bool HasDeploySignaltowerQuestByWorld;
        public override void OnEnterWorld() {
            //神皇在上啊，保佑我别出bug
            if (Player.TryGetADVSave(out var save)) {
                SignalTowerTargetPoint nearestTarget = SignalTowerTargetManager.GetNearestTarget(Player);
                if (nearestTarget != null) {//如果存在目标点
                    HasDeploySignaltowerQuestByWorld = true;
                }
                else {
                    HasDeploySignaltowerQuestByWorld = false;//如果世界中没有保存的有目标点

                    if (InWorldBossPhase.Downed29.Invoke()//如果星流巨械被打败
                        && !save.DeploySignaltowerQuestCompleted//并且任务未完成
                        ) {
                        DeploySignaltowerScenario.SetTurnOn();//尝试让任务生成
                    }
                }
            }
        }
    }
}
