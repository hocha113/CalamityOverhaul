using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.UIs.EntryDecisions
{
    /// <summary>入世决策注册表，仅本地；由 <see cref="EntryDecisionUI"/> 单通道展示</summary>
    internal static class EntryDecisionManager
    {
        private static readonly List<EntryDecision> decisions = [];

        /// <summary>进世界宽限帧，期间不展示</summary>
        public const int GraceFrames = 240;

        /// <summary>进世界后逻辑帧，由 <see cref="EntryDecisionSystem"/> 推进</summary>
        public static int SessionFrames { get; internal set; }

        /// <summary>宽限期已过</summary>
        public static bool GraceElapsed => SessionFrames >= GraceFrames;

        public static IReadOnlyList<EntryDecision> Decisions => decisions;

        public static bool HasAny => decisions.Count > 0;

        /// <summary>注册，同实例去重；dedServ 忽略</summary>
        public static void Register(EntryDecision decision) {
            if (Main.dedServ || decision == null) {
                return;
            }
            if (decisions.Contains(decision)) {
                return;
            }
            decisions.Add(decision);
        }

        /// <summary>剔除失效，UI 每帧</summary>
        public static void TickValidate() {
            for (int i = decisions.Count - 1; i >= 0; i--) {
                if (!decisions[i].StillValid) {
                    EntryDecision removed = decisions[i];
                    decisions.RemoveAt(i);
                    removed.Cancelled();
                }
            }
        }

        /// <summary>清空，世界进出时，不写数据</summary>
        public static void CancelAll() {
            for (int i = decisions.Count - 1; i >= 0; i--) {
                EntryDecision removed = decisions[i];
                decisions.RemoveAt(i);
                removed.Cancelled();
            }
        }
    }

    /// <summary>世界进出清决策表并复位宽限</summary>
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
