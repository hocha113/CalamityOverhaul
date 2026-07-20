using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.UIs.EntryDecisions
{
    /// <summary>
    /// 入世决策注册表，仅本地客户端
    /// <br/>统一收纳世界进入时产生的待确认事项，由 <see cref="EntryDecisionUI"/> 单通道展示，
    /// 避免多个系统各自弹窗抢占屏幕
    /// </summary>
    internal static class EntryDecisionManager
    {
        private static readonly List<EntryDecision> decisions = [];

        /// <summary>进世界后的宽限帧数，期间不展示任何通知，让开场时刻喘口气</summary>
        public const int GraceFrames = 240;

        /// <summary>进世界后经过的逻辑帧数，由 <see cref="EntryDecisionSystem"/> 推进</summary>
        public static int SessionFrames { get; internal set; }

        /// <summary>宽限期已过，允许展示通知</summary>
        public static bool GraceElapsed => SessionFrames >= GraceFrames;

        public static IReadOnlyList<EntryDecision> Decisions => decisions;

        public static bool HasAny => decisions.Count > 0;

        /// <summary>注册决策，同实例去重；dedServ 忽略</summary>
        public static void Register(EntryDecision decision) {
            if (Main.dedServ || decision == null) {
                return;
            }
            if (decisions.Contains(decision)) {
                return;
            }
            decisions.Add(decision);
        }

        /// <summary>剔除失效决策，UI 每帧调用</summary>
        public static void TickValidate() {
            for (int i = decisions.Count - 1; i >= 0; i--) {
                if (!decisions[i].StillValid) {
                    EntryDecision removed = decisions[i];
                    decisions.RemoveAt(i);
                    removed.Cancelled();
                }
            }
        }

        /// <summary>清空全部决策，世界进出时调用，不写任何数据</summary>
        public static void CancelAll() {
            for (int i = decisions.Count - 1; i >= 0; i--) {
                EntryDecision removed = decisions[i];
                decisions.RemoveAt(i);
                removed.Cancelled();
            }
        }
    }

    /// <summary>世界进出清理决策表并复位宽限计时</summary>
    internal class EntryDecisionSystem : ModSystem
    {
        public override void OnWorldLoad() {
            EntryDecisionManager.CancelAll();
            EntryDecisionManager.SessionFrames = 0;
        }

        public override void OnWorldUnload() {
            EntryDecisionManager.CancelAll();
            EntryDecisionManager.SessionFrames = 0;
        }

        public override void ClearWorld() {
            EntryDecisionManager.CancelAll();
            EntryDecisionManager.SessionFrames = 0;
        }

        public override void PostUpdateEverything() {
            if (Main.dedServ || Main.gamePaused) {
                return;
            }
            if (EntryDecisionManager.SessionFrames < int.MaxValue) {
                EntryDecisionManager.SessionFrames++;
            }
        }
    }
}
