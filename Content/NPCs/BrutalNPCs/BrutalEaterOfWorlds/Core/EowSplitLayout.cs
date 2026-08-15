namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEaterOfWorlds.Core
{
    /// <summary>
    /// 分裂布局的唯一裁定处：给定体节总数与分组数，推导每节归属与首节身份<br/>
    /// 输入全部来自同步数据(头 override ai + 体节 npc.ai[0] 序号)，各端计算一致
    /// </summary>
    internal static class EowSplitLayout
    {
        /// <summary>最大分组数(大招四分)</summary>
        internal const int MaxGroups = 4;

        /// <summary>体节序号(0..total-1)在 K 组下的组号；头永远领第0组</summary>
        public static int GroupOf(int ordinal, int totalSegments, int groups) {
            if (groups <= 1 || totalSegments <= 0) {
                return 0;
            }
            int g = ordinal * groups / totalSegments;
            return g >= groups ? groups - 1 : g;
        }

        /// <summary>组 g 的首节序号；g=0 由真头领队，返回-1</summary>
        public static int LeaderOrdinal(int totalSegments, int groups, int g) {
            if (g <= 0 || groups <= 1) {
                return -1;
            }
            //向上取整的组边界，与 GroupOf 一致
            int start = (g * totalSegments + groups - 1) / groups;
            //保证边界点确实属于组 g（整除误差回拨）
            while (start > 0 && GroupOf(start - 1, totalSegments, groups) >= g) {
                start--;
            }
            while (start < totalSegments && GroupOf(start, totalSegments, groups) < g) {
                start++;
            }
            return start;
        }

        /// <summary>该序号是否为某组首节(不含头组)</summary>
        public static bool IsLeader(int ordinal, int totalSegments, int groups, out int group) {
            group = 0;
            if (groups <= 1 || totalSegments <= 0 || ordinal <= 0) {
                return false;
            }
            group = GroupOf(ordinal, totalSegments, groups);
            if (group == 0) {
                return false;
            }
            return GroupOf(ordinal - 1, totalSegments, groups) != group;
        }

        /// <summary>组尾序号(该组最后一节)</summary>
        public static int TailOrdinal(int totalSegments, int groups, int g) {
            if (groups <= 1) {
                return totalSegments - 1;
            }
            int next = g + 1;
            if (next >= groups) {
                return totalSegments - 1;
            }
            int nextStart = LeaderOrdinal(totalSegments, groups, next);
            return (nextStart <= 0 ? totalSegments : nextStart) - 1;
        }

        /// <summary>撕裂点闪烁强度：距任一组边界的链上距离→0~1，供体节染色</summary>
        public static float BoundaryGlow(int ordinal, int totalSegments, int groups) {
            if (groups <= 1 || totalSegments <= 0) {
                return 0f;
            }
            float best = float.MaxValue;
            for (int g = 1; g < groups; g++) {
                int b = LeaderOrdinal(totalSegments, groups, g);
                if (b < 0) {
                    continue;
                }
                float d = System.Math.Abs(ordinal - b + 0.5f);
                if (d < best) {
                    best = d;
                }
            }
            return MathHelper.Clamp(1f - best / 3.5f, 0f, 1f);
        }
    }
}
