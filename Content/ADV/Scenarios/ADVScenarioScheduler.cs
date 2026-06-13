using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.ADV.Scenarios
{
    /// <summary>
    /// ADV 场景调度器，每帧 Tick 调用 Update 并评估 ScenarioPolicy
    /// </summary>
    internal static class ADVScenarioScheduler
    {
        // 阻塞器提供者，每帧按位合并
        private static readonly List<Func<ScenarioBlockers>> blockerProviders = [];

        /// <summary>
        /// 注册阻塞器提供者，按位合并为全局阻塞状态
        /// 在 Mod 加载阶段调用（如 PostSetupContent）
        /// </summary>
        public static void RegisterBlocker(Func<ScenarioBlockers> provider) {
            blockerProviders.Add(provider);
        }

        /// <summary>
        /// 每帧由 ADVPlayer.PostUpdate 调用
        /// </summary>
        public static void Tick(ADVSave save, Player player) {
            // 1. 预计算全局阻塞
            ScenarioBlockers currentBlockers = ScenarioBlockers.None;
            foreach (var provider in blockerProviders) {
                currentBlockers |= provider();
            }


            // 2. 遍历所有场景
            ADVScenarioBase bestCandidate = null;
            int bestPriority = int.MinValue;

            foreach (var scenario in ADVScenarioBase.Instances) {
                // Update 每帧无条件调用
                scenario.Update(save, player);

                var policy = scenario.Policy;
                if (policy == null) {
                    continue;
                }

                // 已完成则跳过触发
                if (policy.IsCompleted(save)) {
                    continue;
                }

                // 阻塞器交集则跳过
                if ((currentBlockers & policy.BlockedBy) != 0) {
                    continue;
                }

                //自定义触发条件
                if (policy.CanTrigger != null && !policy.CanTrigger(save, player)) {
                    continue;
                }

                // 条件满足，参与优先级竞选
                if (policy.Priority > bestPriority) {
                    bestPriority = policy.Priority;
                    bestCandidate = scenario;
                }
            }

            // 3. 触发选中场景
            if (bestCandidate == null) {
                return;
            }

            if (!bestCandidate.StartScenario()) {
                return;
            }

            // 标记完成
            bestCandidate.Policy.MarkCompleted?.Invoke(save);

            // 触发后回调
            bestCandidate.Policy.OnStarted?.Invoke(save, player);
        }

        /// <summary>
        /// Mod卸载时清理所有状态
        /// </summary>
        internal static void Unload() {
            blockerProviders.Clear();
        }
    }
}
