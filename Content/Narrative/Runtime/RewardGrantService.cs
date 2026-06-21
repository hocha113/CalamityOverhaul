using InnoVault.Narrative.Core;
using InnoVault.Narrative.Services;
using Terraria;

namespace CalamityOverhaul.Content.Narrative.Runtime
{
    internal sealed class RewardGrantService : IRewardGrantService
    {
        public void Grant(RewardPayload reward, Player player) {
            if (reward == null || player == null || !player.active || reward.ItemType <= 0) {
                return;
            }

            int stack = reward.Stack <= 0 ? 1 : reward.Stack;
            player.QuickSpawnItem(player.GetSource_GiftOrReward(), reward.ItemType, stack);
        }
    }
}
