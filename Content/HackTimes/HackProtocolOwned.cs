using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.HackTimes
{
    /// <summary>
    /// 协议持有门闩：玩家能在骇入面板点得动的协议集合。<br/>
    /// 出厂协议由 <see cref="QuickHackDef.UnlockedByDefault"/> 自动入种子，
    /// 其余靠协议芯片 <see cref="Chips.BaseHackProtocolChip{T}"/> 解锁
    /// </summary>
    internal static class HackProtocolOwned
    {
        /// <summary>是否持有；协议为空或玩家无效均视作未持有</summary>
        public static bool Owns(Player player, QuickHackDef hack) {
            if (hack == null) {
                return false;
            }
            if (hack.UnlockedByDefault) {
                return true;
            }
            if (player == null || !player.TryGetModPlayer(out HackTimePlayer htp)) {
                return false;
            }
            EnsureSeed(htp);
            return htp.OwnedProtocols.Contains(hack.FullName);
        }

        /// <summary>写入持有；已持有返回 false，不发重复快照</summary>
        public static bool Unlock(Player player, QuickHackDef hack) {
            if (hack == null || player == null
                || !player.TryGetModPlayer(out HackTimePlayer htp)) {
                return false;
            }
            EnsureSeed(htp);
            if (!htp.OwnedProtocols.Add(hack.FullName)) {
                return false;
            }
            HackTimeNetSync.SendOwnedSnapshot(player);
            return true;
        }

        /// <summary>权威端按快照重建持有集</summary>
        internal static void ApplyNetworkSnapshot(Player player, IEnumerable<QuickHackDef> hacks) {
            if (player == null || !player.TryGetModPlayer(out HackTimePlayer htp)) {
                return;
            }
            htp.OwnedProtocols = [];
            if (hacks != null) {
                foreach (QuickHackDef hack in hacks) {
                    if (hack != null) {
                        htp.OwnedProtocols.Add(hack.FullName);
                    }
                }
            }
            EnsureSeed(htp);
            htp.OwnedSnapshotReceived = true;
        }

        /// <summary>补齐出厂协议，读档与建号两条路都要过这里</summary>
        public static void EnsureSeed(HackTimePlayer htp) {
            if (htp == null) {
                return;
            }
            htp.OwnedProtocols ??= [];
            List<QuickHackDef> all = QuickHackDef.Instances;
            for (int i = 0; i < all.Count; i++) {
                QuickHackDef hack = all[i];
                if (hack != null && hack.UnlockedByDefault) {
                    htp.OwnedProtocols.Add(hack.FullName);
                }
            }
        }

        /// <summary>面板页脚计数用：已持有的协议数</summary>
        public static int CountOwned(Player player) {
            List<QuickHackDef> all = QuickHackDef.Instances;
            int count = 0;
            for (int i = 0; i < all.Count; i++) {
                if (Owns(player, all[i])) {
                    count++;
                }
            }
            return count;
        }
    }
}
