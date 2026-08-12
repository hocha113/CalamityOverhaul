using InnoVault.Storages;
using System;
using Terraria;
using Terraria.DataStructures;

namespace CalamityOverhaul.Content.Industrials.MachineModules
{
    /// <summary>
    /// 物流行为件的共用实现:自动进料(从近旁存储抽料)与自动出料(产物直入近旁存储)。<br/>
    /// 只在权威端主线程调用(并行阶段经 TP 的 Defer 转发);
    /// 原版箱子的改动全部走 <see cref="ChestNetSync"/> 快照广播
    /// </summary>
    internal static class MachineLogistics
    {
        /// <summary>存储搜索半径(像素),从机器左上角起算</summary>
        internal const int SearchRange = 320;

        /// <summary>
        /// 从近旁存储抽出第一个满足谓词的物品,最多 <paramref name="maxTake"/> 件。
        /// 返回抽到的物品,没抽到返回空物品。
        /// 宿主自身也可能注册为存储提供者(如焚化炉),按位置排除,防止自吞自吐
        /// </summary>
        internal static Item TryWithdraw(Point16 position, Func<Item, bool> predicate, int maxTake) {
            foreach (IStorageProvider provider in StorageLoader.FindAllStorageTargets(position, SearchRange)) {
                if (provider == null || !provider.IsValid || provider.Position == position) {
                    continue;
                }

                //先扫后取:枚举期间不动存储,避免边遍历边改
                int foundType = -1;
                int foundStack = 0;
                foreach (Item stored in provider.GetStoredItems()) {
                    if (stored == null || stored.IsAir || !predicate(stored)) {
                        continue;
                    }
                    foundType = stored.type;
                    foundStack = stored.stack;
                    break;
                }
                if (foundType <= 0) {
                    continue;
                }

                //原版箱子改动后需广播变化槽位,否则开着箱子的玩家看到过期内容
                ChestNetSync.Snapshot snap = ChestNetSync.Capture(provider);
                Item got = provider.WithdrawItem(foundType, Math.Min(maxTake, foundStack));
                if (got != null && !got.IsAir) {
                    ChestNetSync.SendChanged(snap.ChestIndex, ChestNetSync.CollectChanged(snap));
                    return got;
                }
            }
            return new Item();
        }

        /// <summary>产物存入近旁存储(宿主自身按位置排除);没有可用存储返回 false,由调用方决定落地与否</summary>
        internal static bool TryDeposit(Point16 position, Item item) {
            if (item == null || item.IsAir) {
                return false;
            }
            foreach (IStorageProvider provider in StorageLoader.FindAllStorageTargets(position, SearchRange)) {
                if (provider == null || !provider.IsValid || provider.Position == position
                    || !provider.CanAcceptItem(item)) {
                    continue;
                }
                ChestNetSync.Snapshot snap = ChestNetSync.Capture(provider);
                if (provider.DepositItem(item)) {
                    provider.PlayDepositAnimation();
                    ChestNetSync.SendChanged(snap.ChestIndex, ChestNetSync.CollectChanged(snap));
                    return true;
                }
            }
            return false;
        }
    }
}
