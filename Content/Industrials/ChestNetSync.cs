using InnoVault.Storages;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Industrials
{
    /// <summary>
    /// 箱子槽位网络同步：原版箱子存储(<see cref="ChestStorageProvider"/>)的读写没有任何网络处理，
    /// 服务器上机器/管道改动箱子后必须手动广播 <see cref="MessageID.SyncChestItem"/>，
    /// 否则正开着箱子的玩家会一直看到过期内容，并可能用旧数据覆盖机器的改动<br/>
    /// 用法：改动前 <see cref="Capture"/> 快照，改动后 <see cref="CollectChanged"/> 取差异槽位，
    /// 最后在主线程调用 <see cref="SendChanged"/>（并行阶段经 TP 的 Defer 转发）
    /// </summary>
    internal static class ChestNetSync
    {
        /// <summary>一次箱子改动的快照，仅记录比较所需的最小字段</summary>
        internal readonly struct Snapshot
        {
            internal readonly int ChestIndex;
            internal readonly (int type, int stack, int prefix)[] Slots;
            internal bool IsValid => Slots != null;

            internal Snapshot(int chestIndex, (int, int, int)[] slots) {
                ChestIndex = chestIndex;
                Slots = slots;
            }
        }

        /// <summary>仅在服务器且目标是原版箱子时生成快照；其余情况返回无效快照(零开销跳过)</summary>
        public static Snapshot Capture(IStorageProvider storage) {
            if (!VaultUtils.isServer || storage is not ChestStorageProvider chestProvider) {
                return default;
            }
            int chestIndex = chestProvider.ChestIndex;
            if (chestIndex < 0 || chestIndex >= Main.maxChests) {
                return default;
            }
            Chest chest = Main.chest[chestIndex];
            if (chest?.item == null) {
                return default;
            }

            var slots = new (int, int, int)[chest.item.Length];
            for (int i = 0; i < chest.item.Length; i++) {
                Item item = chest.item[i];
                slots[i] = item == null ? (0, 0, 0) : (item.type, item.stack, item.prefix);
            }
            return new Snapshot(chestIndex, slots);
        }

        /// <summary>对比快照与当前内容，收集发生变化的槽位索引；无变化返回 null</summary>
        public static List<int> CollectChanged(in Snapshot snapshot) {
            if (!snapshot.IsValid) {
                return null;
            }
            Chest chest = Main.chest[snapshot.ChestIndex];
            if (chest?.item == null) {
                return null;
            }

            List<int> changed = null;
            int count = System.Math.Min(snapshot.Slots.Length, chest.item.Length);
            for (int i = 0; i < count; i++) {
                Item item = chest.item[i];
                (int type, int stack, int prefix) now = item == null ? (0, 0, 0) : (item.type, item.stack, item.prefix);
                if (now != snapshot.Slots[i]) {
                    (changed ??= []).Add(i);
                }
            }
            return changed;
        }

        /// <summary>广播变化槽位，须在主线程调用</summary>
        public static void SendChanged(int chestIndex, List<int> changed) {
            if (changed == null || !VaultUtils.isServer) {
                return;
            }
            foreach (int slot in changed) {
                NetMessage.SendData(MessageID.SyncChestItem, -1, -1, null, chestIndex, slot);
            }
        }
    }
}
