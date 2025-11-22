using CalamityOverhaul.Content.ADV.Scenarios.Draedons.Quest.DeploySignaltowers.SignalTower;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Draedons.Quest
{
    internal class DSTPlayer : ModPlayer
    {
        public override void OnEnterWorld() {
            //神皇在上啊，保佑我别出bug
            if (Player.TryGetADVSave(out var save)) {
                SignalTowerTargetPoint nearestTarget = SignalTowerTargetManager.GetNearestTarget(Player);
                if (nearestTarget != null) {//如果存在目标点，则确保任务被接受且未完成
                    save.DeploySignaltowerQuestAccepted = true;//确保任务被接受
                    save.DeploySignaltowerQuestCompleted = false;//确保任务未完成
                }
                else {//如果不存在目标点
                    save.DeploySignaltowerQuestAccepted = false;
                    save.DeploySignaltowerQuestCompleted = true;
                }
            }
        }
    }
}
