using InnoVault.Narrative.Core;
using InnoVault.Narrative.Services;
using Terraria;

namespace CalamityOverhaul.Content.Narrative
{
    internal sealed class RewardGrantService : IRewardGrantService
    {
        public void Grant(RewardPayload reward, Player player) {
            if (reward == null || player == null || !player.active || reward.ItemType <= 0) {
                return;
            }

            player.GiveItem(player.GetSource_GiftOrReward(), reward.ItemType, reward.Stack);
        }
    }
}
