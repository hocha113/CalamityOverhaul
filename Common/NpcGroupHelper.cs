using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Common
{
    /// <summary>
    /// 多实体 Boss 群组：共享 <see cref="NPC.realLife"/> 或体节类型表归一组
    /// </summary>
    public static class NpcGroupHelper
    {
        /// <summary>
        /// 群组锚点索引（头/主体）    
        /// </summary>
        public static int GetAnchorIndex(NPC npc) {
            if (npc == null || !npc.active) {
                return -1;
            }
            int rl = npc.realLife;
            if (rl >= 0 && rl < Main.maxNPCs && Main.npc[rl].active) {
                return rl;
            }
            return npc.whoAmI;
        }

        /// <summary>
        /// 两 NPC 同组：共享 <see cref="GetAnchorIndex"/> 锚点或同体节类型表
        /// </summary>
        public static bool IsSameGroup(NPC a, NPC b) {
            if (a == null || b == null || !a.active || !b.active) {
                return false;
            }
            if (a.whoAmI == b.whoAmI) {
                return true;
            }
            //realLife 链接判定，覆盖所有蠕虫类 Boss 的体节
            int aa = ResolveAnchor(a);
            int bb = ResolveAnchor(b);
            if (aa == bb) {
                return true;
            }
            //类型表判定，覆盖月总、毁灭者等无 realLife 链接但同属一个 Boss 的多实体
            return ShareSegmentList(a.type, b.type);
        }

        /// <summary>
        /// 单次扫描 <see cref="Main.npc"/> 收集与 <paramref name="root"/> 同组活跃 NPC 写入 <paramref name="output"/>，无递归
        /// </summary>
        /// <param name="root">任意体节或头部</param>
        /// <param name="output">复用容器，避免分配</param>
        /// <param name="clear">写入前清空，默认 true</param>
        public static void CollectGroup(NPC root, List<NPC> output, bool clear = true) {
            if (output == null) {
                return;
            }
            if (clear) {
                output.Clear();
            }
            if (root == null || !root.active) {
                return;
            }
            int anchor = ResolveAnchor(root);
            //预先取出 root 类型对应的体节列表，省掉每次循环重复查找
            List<int> segList = FindSegmentList(root.type);

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC n = Main.npc[i];
                if (!n.active) {
                    continue;
                }
                if (IsMember(n, anchor, segList)) {
                    output.Add(n);
                }
            }
        }

        /// <summary>收集群组成员 whoAmI 索引</summary>
        public static void CollectGroupIndices(NPC root, List<int> output, bool clear = true) {
            if (output == null) {
                return;
            }
            if (clear) {
                output.Clear();
            }
            if (root == null || !root.active) {
                return;
            }
            int anchor = ResolveAnchor(root);
            List<int> segList = FindSegmentList(root.type);

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC n = Main.npc[i];
                if (!n.active) {
                    continue;
                }
                if (IsMember(n, anchor, segList)) {
                    output.Add(n.whoAmI);
                }
            }
        }

        /// <summary>
        /// 新分配列表返回群组成员，热点路径用 <see cref="CollectGroup(NPC, List{NPC}, bool)"/> 复用版
        /// </summary>
        public static List<NPC> GetGroup(NPC root) {
            List<NPC> list = [];
            CollectGroup(root, list, false);
            return list;
        }

        /// <summary>对群组内活跃成员执行 <paramref name="action"/>，不可 null</summary>
        public static void ForEachGroupMember(NPC root, Action<NPC> action) {
            if (action == null || root == null || !root.active) {
                return;
            }
            int anchor = ResolveAnchor(root);
            List<int> segList = FindSegmentList(root.type);

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC n = Main.npc[i];
                if (!n.active) {
                    continue;
                }
                if (IsMember(n, anchor, segList)) {
                    action(n);
                }
            }
        }

        //单成员判定，已假设 n.active
        private static bool IsMember(NPC n, int anchor, List<int> segList) {
            if (anchor >= 0 && ResolveAnchor(n) == anchor) {
                return true;
            }
            if (segList != null && segList.Contains(n.type)) {
                return true;
            }
            return false;
        }

        //不依赖外部活跃判定的锚点解析，调用方需保证 n != null
        private static int ResolveAnchor(NPC n) {
            int rl = n.realLife;
            if (rl >= 0 && rl < Main.maxNPCs && Main.npc[rl].active) {
                return rl;
            }
            return n.whoAmI;
        }

        //在预定义 Boss 体节表中查找包含 type 的列表，找不到返回 null
        private static List<int> FindSegmentList(int type) {
            var all = CWRLoad.AllBossSegmentLists;
            if (all == null) {
                return null;
            }
            for (int i = 0; i < all.Count; i++) {
                var list = all[i];
                if (list != null && list.Contains(type)) {
                    return list;
                }
            }
            return null;
        }

        //两个类型是否同属一个体节列表
        private static bool ShareSegmentList(int typeA, int typeB) {
            var all = CWRLoad.AllBossSegmentLists;
            if (all == null) {
                return false;
            }
            for (int i = 0; i < all.Count; i++) {
                var list = all[i];
                if (list != null && list.Contains(typeA) && list.Contains(typeB)) {
                    return true;
                }
            }
            return false;
        }
    }
}
