using InnoVault.Narrative.Core;
using InnoVault.Narrative.Services;
using System;
using Terraria;
using Terraria.DataStructures;

namespace CalamityOverhaul.Content.Narrative
{
    internal sealed class RewardGrantService : IRewardGrantService
    {
        public void Grant(RewardPayload reward, Player player) {
            if (reward == null || player == null || !player.active || reward.ItemType <= 0) {
                return;
            }

            //背包是本地权威，替别人发只会造出别人看不见的物品
            if (player.whoAmI != Main.myPlayer) {
                return;
            }

            IEntitySource source = player.GetSource_GiftOrReward();
            int remaining = reward.Stack <= 0 ? 1 : reward.Stack;
            while (remaining > 0) {
                Item gift = new(reward.ItemType);
                gift.stack = Math.Min(remaining, Math.Max(1, gift.maxStack));
                remaining -= gift.stack;
                GiveOrDrop(player, source, gift);
            }
        }

        /// <summary>
        /// 奖励直接进背包，塞不下的那份才落地<br/>
        /// 单人下 Item.NewItem 会立刻把掉落物预定给本人，看起来像直接给；
        /// 多人下这段不执行，实际是请求服务端造一个普通掉落物，
        /// 归属由服务端 FindOwner 按"最近的可拾取者"重算，
        /// 于是拾取范围更大的队友、水火、掉落物槽位回收都可能把礼物吃掉
        /// </summary>
        private static void GiveOrDrop(Player player, IEntitySource source, Item gift) {
            gift.position = player.Center;
            Item overflow = player.GetItem(player.whoAmI, gift, GetItemSettings.NPCEntityToPlayerInventorySettings);
            if (overflow.IsAir || overflow.stack <= 0) {
                return;
            }

            player.QuickSpawnItem(source, overflow, overflow.stack);
        }
    }
}
