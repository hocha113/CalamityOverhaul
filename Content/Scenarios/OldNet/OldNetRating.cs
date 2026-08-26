using CalamityOverhaul.Content.Scenarios.OldNet.Gen;
using System;

namespace CalamityOverhaul.Content.Scenarios.OldNet
{
    /// <summary>风格加成旗标（弹出时判定，战报屏揭晓；仅会话内使用，不入档）</summary>
    [Flags]
    internal enum OldNetStyleFlags : byte
    {
        None = 0,
        /// <summary>幽灵潜行：全程未被目击、零击杀、档位没上过 T2</summary>
        Ghost = 1 << 0,
        /// <summary>高热生还：进过 T4 且安全断链</summary>
        HeatSurvivor = 1 << 1,
        /// <summary>热断链：完成 10 秒站桩强制断链</summary>
        HotExtract = 1 << 2,
        /// <summary>收网撤离：收网协议激活下安全断链</summary>
        DragnetEscape = 1 << 3,
    }

    /// <summary>
    /// 深潜评级算分（06 点子 2.1）：基础分随会话实时累积（HUD 评级字母的数据源），
    /// 弹出结算与风格加成一次性判定（战报屏揭晓）。
    /// 权重与阈值全在 OldNetMetrics 06 常量区，首轮数值必调
    /// </summary>
    internal static class OldNetRating
    {
        //评级序（历史最佳持久化 BestGradeIndex 用同一序）
        internal const int GradeD = 0;
        internal const int GradeC = 1;
        internal const int GradeB = 2;
        internal const int GradeA = 3;
        internal const int GradeS = 4;

        private static readonly string[] Letters = ["D", "C", "B", "A", "S"];

        //评级配色（面板色族派生）：D 灰红 / C 灰 / B 琥珀 / A 冷青 / S 白金
        private static readonly Color[] Colors = [
            new(196, 120, 110),
            new(150, 160, 175),
            new(255, 170, 60),
            new(140, 200, 210),
            new(232, 236, 230),
        ];

        internal static string Letter(int gradeIndex) => Letters[Math.Clamp(gradeIndex, GradeD, GradeS)];

        internal static Color GradeColor(int gradeIndex) => Colors[Math.Clamp(gradeIndex, GradeD, GradeS)];

        /// <summary>基础分：铭刻 ×12 + 深度 ×0.4 + 采集 ×6（实时累积，HUD 跳字）</summary>
        internal static int BaseScore(OldNetPlayer session) {
            float score = session.SettledTotal * OldNetMetrics.RatingSettledWeight
                + session.MaxDepthCols * OldNetMetrics.RatingDepthWeight
                + session.HarvestCount * OldNetMetrics.RatingHarvestWeight;
            return (int)score;
        }

        internal static int GradeIndexFor(int score) {
            if (score >= OldNetMetrics.RatingGradeS) {
                return GradeS;
            }
            if (score >= OldNetMetrics.RatingGradeA) {
                return GradeA;
            }
            if (score >= OldNetMetrics.RatingGradeB) {
                return GradeB;
            }
            if (score >= OldNetMetrics.RatingGradeC) {
                return GradeC;
            }
            return GradeD;
        }

        /// <summary>
        /// 弹出结算（CacheReport 快照时调用，此刻 director 会话仍有效）：
        /// 安全登出加成与风格加成先入总，烧断/死亡最后总分 ×0.4，损失要疼但已铭刻的意义保留
        /// </summary>
        internal static (int score, int gradeIndex, OldNetStyleFlags styles) Compute(
            OldNetPlayer session, UI.OldNetExitKind kind) {
            bool safe = kind == UI.OldNetExitKind.SafeLogout;
            int score = BaseScore(session);
            OldNetStyleFlags styles = OldNetStyleFlags.None;

            if (safe) {
                score += OldNetMetrics.RatingSafeExitBonus;
            }
            //幽灵潜行（余震/热断链的自招响应经 countAsSpotted=false 豁免，不破此判定）
            if (session.SpottedCount == 0 && session.PatrolKills == 0
                && session.TurretKills == 0 && session.MaxTierReached <= 1) {
                styles |= OldNetStyleFlags.Ghost;
                score += OldNetMetrics.RatingStyleGhost;
            }
            if (safe && session.MaxTierReached >= 4) {
                styles |= OldNetStyleFlags.HeatSurvivor;
                score += OldNetMetrics.RatingStyleHeat;
            }
            if (session.HotExtractDone) {
                styles |= OldNetStyleFlags.HotExtract;
                score += OldNetMetrics.RatingStyleHotExtract;
            }
            if (safe && NPCs.OldNetICEDirector.DragnetActive) {
                styles |= OldNetStyleFlags.DragnetEscape;
                score += OldNetMetrics.RatingStyleDragnet;
            }
            if (!safe) {
                score = (int)(score * OldNetMetrics.RatingDisasterMul);
            }
            return (score, GradeIndexFor(score), styles);
        }

        /// <summary>风格加成分值（战报屏风格行呈现用）</summary>
        internal static int StyleBonus(OldNetStyleFlags flag) => flag switch {
            OldNetStyleFlags.Ghost => OldNetMetrics.RatingStyleGhost,
            OldNetStyleFlags.HeatSurvivor => OldNetMetrics.RatingStyleHeat,
            OldNetStyleFlags.HotExtract => OldNetMetrics.RatingStyleHotExtract,
            OldNetStyleFlags.DragnetEscape => OldNetMetrics.RatingStyleDragnet,
            _ => 0,
        };
    }
}
