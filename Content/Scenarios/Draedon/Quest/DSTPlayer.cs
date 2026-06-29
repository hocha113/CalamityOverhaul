using CalamityOverhaul.Content.Scenarios.Draedon.Quest.DeploySignaltowers.SignalTower;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Draedon.Quest
{
    internal class DSTPlayer : ModPlayer
    {
        //供 DraedonQuestLine 决定是否在委托面板登记部署任务,委托登门改由 DeploySignaltowerScenario 自驱
        public static bool HasDeploySignaltowerQuestByWorld;

        public override void OnEnterWorld()
            => HasDeploySignaltowerQuestByWorld = SignalTowerTargetManager.GetNearestTarget(Player) != null;
    }
}
