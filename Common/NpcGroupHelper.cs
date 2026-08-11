using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Common
{
    /// <summary>
    /// 多实体 Boss 群组，共享 <see cref="NPC.realLife"/> 或体节类型表
    /// </summary>
    public static class NpcGroupHelper
    {
        /// <summary>群组锚点索引（头/主体）</summary>
        public static int GetAnchorIndex(NPC npc) {
            if (npc == null) {
                return -1;
            }
            int rl = npc.realLife;
            if (rl >= 0 && rl < Main.maxNPCs && Main.npc[rl].active) {
                return rl;
            }
            if (npc.type == CWRID.NPC_AquaticScourgeBodyAlt
                || npc.type == CWRID.NPC_AquaticScourgeBody
                || npc.type == CWRID.NPC_AquaticScourgeTail) {
                int nearestHead = -1;
                float nearestSq = float.MaxValue;
                foreach (NPC candidate in Main.ActiveNPCs) {
                    if (candidate.type != CWRID.NPC_AquaticScourgeHead) {
                        continue;
                    }
                    float distanceSq = candidate.DistanceSQ(npc.Center);
                    if (distanceSq < nearestSq) {
                        nearestSq = distanceSq;
                        nearestHead = candidate.whoAmI;
                    }
                }
                if (nearestHead >= 0) {
                    return nearestHead;
                }
            }
            return npc.whoAmI;
        }

        /// <summary>Boss 级目标，包含 ShouldBeCountedAsBoss、realLife 锚点与已登记 Boss 体节表。</summary>
        public static bool IsBossTier(NPC npc) {
            if (npc == null || !npc.active) {
                return false;
            }
            if (npc.boss || NPCID.Sets.ShouldBeCountedAsBoss[npc.type]) {
                return true;
            }

            int anchorIndex = GetAnchorIndex(npc);
            if (anchorIndex >= 0 && anchorIndex != npc.whoAmI) {
                NPC anchor = Main.npc[anchorIndex];
                if (anchor.boss || NPCID.Sets.ShouldBeCountedAsBoss[anchor.type]) {
                    return true;
                }
            }

            return FindSegmentList(npc.type) != null;
        }

        /// <summary>同组判定，共享锚点或同体节表</summary>
        public static bool IsSameGroup(NPC a, NPC b) {
            if (a == null || b == null || !a.active || !b.active) {
                return false;
            }
            if (a.whoAmI == b.whoAmI) {
                return true;
            }
            //realLife 链接（蠕虫体节）
            int aa = ResolveAnchor(a);
            int bb = ResolveAnchor(b);
            if (aa == bb) {
                return true;
            }
            //体节类型表（月总、毁灭者等无 realLife）
            return ShareSegmentList(a.type, b.type);
        }

        /// <summary>
        /// 单次扫描 <see cref="Main.npc"/> 收集同组活跃成员写入 <paramref name="output"/>，无递归；
        /// 再沿 ai 双向体节链补集一次，不共享 realLife 又不在体节表里的蠕虫（EoW 分裂半截、
        /// 自旋链接的模组蠕虫）也能收满整条，不会出现"扒走一节其余还在动"
        /// </summary>
        /// <param name="output">复用容器</param>
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

            AppendAiLinkChain(root, output);
        }

        /// <summary>收集群组成员 whoAmI，含 ai 体节链补集</summary>
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

            chainScratch.Clear();
            AppendAiLinkChain(root, chainScratch);
            for (int i = 0; i < chainScratch.Count; i++) {
                int who = chainScratch[i].whoAmI;
                if (!output.Contains(who)) {
                    output.Add(who);
                }
            }
            chainScratch.Clear();
        }

        //ai 链步行的复用容器
        private static readonly List<NPC> chainScratch = [];

        /// <summary>
        /// 蠕虫 ai 链补集：vanilla 蠕虫 AI 的体节契约是 ai[0]=身后节、ai[1]=身前节
        /// （NPC.AI_006_Worms 生成链），除 EoW 外原版蠕虫同时共享 realLife，
        /// 但 EoW（强制 realLife=-1）与部分模组蠕虫只有 ai 链。沿双向链走满整条虫；
        /// 链接有效性取双向一致（对方反向链指回自己）且两端同 aiStyle，
        /// 计时器巧合的脏 ai 值卷不进无关 NPC
        /// </summary>
        private static void AppendAiLinkChain(NPC root, List<NPC> output) {
            WalkAiChain(root, output, towardHead: true);
            WalkAiChain(root, output, towardHead: false);
        }

        private static void WalkAiChain(NPC from, List<NPC> output, bool towardHead) {
            NPC current = from;
            for (int guard = 0; guard < Main.maxNPCs; guard++) {
                int nextIndex = (int)(towardHead ? current.ai[1] : current.ai[0]);
                if (nextIndex < 0 || nextIndex >= Main.maxNPCs || nextIndex == current.whoAmI) {
                    return;
                }
                NPC next = Main.npc[nextIndex];
                if (next?.active != true || next.aiStyle != current.aiStyle) {
                    return;
                }
                int backIndex = (int)(towardHead ? next.ai[0] : next.ai[1]);
                if (backIndex != current.whoAmI) {
                    return;
                }
                if (output.Contains(next)) {
                    //已收录（realLife/体节表先收过，或链成环），继续沿链走防中段断集
                    current = next;
                    continue;
                }
                output.Add(next);
                current = next;
            }
        }

        /// <summary>
        /// 新分配列表返回成员，热点路径用 <see cref="CollectGroup(NPC, List{NPC}, bool)"/> 复用版
        /// </summary>
        public static List<NPC> GetGroup(NPC root) {
            List<NPC> list = [];
            CollectGroup(root, list, false);
            return list;
        }

        /// <summary>对群组活跃成员执行 action，不可 null</summary>
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

        //已假设 n.active
        private static bool IsMember(NPC n, int anchor, List<int> segList) {
            if (anchor >= 0 && ResolveAnchor(n) == anchor) {
                return true;
            }
            if (segList != null && segList.Contains(n.type)) {
                return true;
            }
            return false;
        }

        //不查 active，调用方保证 n != null
        private static int ResolveAnchor(NPC n) {
            int rl = n.realLife;
            if (rl >= 0 && rl < Main.maxNPCs && Main.npc[rl].active) {
                return rl;
            }
            return n.whoAmI;
        }

        //体节表查找，找不到返回 null
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

        //两 type 是否同体节表
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
