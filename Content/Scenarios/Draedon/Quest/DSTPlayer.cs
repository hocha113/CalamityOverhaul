using CalamityOverhaul.Content.Scenarios.Draedon.Quest.DeploySignaltowers.SignalTower;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Draedon.Quest
{
    internal class DSTPlayer : ModPlayer
    {
        //供DraedonQuestLine登记,登门由DeploySignaltowerScenario自驱
        public static bool HasDeploySignaltowerQuestByWorld;

        public override void OnEnterWorld()
            => HasDeploySignaltowerQuestByWorld = SignalTowerTargetManager.GetNearestTarget(Player) != null;
    }
}
