using System.Collections.Generic;

namespace CalamityOverhaul.Content.GameModes.Blessings
{
    /// <summary>
    /// 祝福目录：加载期反射收集，按进度序定席。
    /// 席位号即网络编号（tML 强制两端模组哈希一致，同一二进制排序恒一致）
    /// </summary>
    internal sealed class BlessingRegistry : ICWRLoader
    {
        private static readonly List<Blessing> all = [];
        private static readonly Dictionary<string, Blessing> byID = [];
        private static readonly Dictionary<int, Blessing> byAnchor = [];

        /// <summary>全部祝福，按进度序</summary>
        public static IReadOnlyList<Blessing> All => all;

        public static bool TryGet(string id, out Blessing blessing) {
            if (!string.IsNullOrEmpty(id) && byID.TryGetValue(id, out blessing)) {
                return true;
            }
            blessing = null;
            return false;
        }

        /// <summary>按讨伐锚点 NPC 类型取祝福，未收录返回 null</summary>
        public static Blessing FindByAnchor(int npcType) => byAnchor.GetValueOrDefault(npcType);

        void ICWRLoader.LoadData() {
            List<Blessing> found = VaultUtils.GetDerivedInstances<Blessing>();
            found.Sort((a, b) => {
                int order = a.ProgressOrder.CompareTo(b.ProgressOrder);
                return order != 0 ? order : string.CompareOrdinal(a.ID, b.ID);
            });

            foreach (Blessing blessing in found) {
                if (byID.ContainsKey(blessing.ID)) {
                    CWRMod.Instance.Logger.Error($"[BlessingRegistry] 重复档案键 '{blessing.ID}'");
                    continue;
                }
                blessing.Seat = all.Count;
                blessing.LoadLocalization();
                all.Add(blessing);
                byID[blessing.ID] = blessing;
                foreach (int anchor in blessing.AnchorNPCTypes) {
                    if (!byAnchor.TryAdd(anchor, blessing)) {
                        CWRMod.Instance.Logger.Error($"[BlessingRegistry] 锚点 {anchor} 被 '{blessing.ID}' 重复认领");
                    }
                }
            }
        }

        void ICWRLoader.UnLoadData() {
            all.Clear();
            byID.Clear();
            byAnchor.Clear();
        }
    }
}
