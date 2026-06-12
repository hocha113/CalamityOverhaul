using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.Common
{
    /// <summary>
    /// 机械Boss视觉状态共享容器（按 NPC whoAmI 索引）
    /// <br/>支持多Boss并存：毁灭者头部、机械骷髅王头部、魔焰眼、激光眼各自维护独立状态。
    /// <br/>调用方在 AI 或 Draw 中通过 <see cref="Push"/> 写入，
    /// 对应的躯干/肢体在 Draw 中通过 <see cref="Read"/> 读取，从而保持整套机械的滤镜一致。
    /// <br/>状态自动过期（>5帧未刷新视为失效），避免Boss离场后视觉滞留。
    /// </summary>
    internal static class MechBossVisualState
    {
        private struct Entry
        {
            public MechBossVisualMode Mode;
            public float Intensity;
            public float Progress;
            public long Frame;
        }

        private static readonly Dictionary<int, Entry> _states = new();

        /// <summary>
        /// 推送某个控制器（一般是Boss头部NPC）的视觉状态
        /// </summary>
        public static void Push(int controllerNpcId, MechBossVisualMode mode, float intensity, float progress = 0f) {
            intensity = MathHelper.Clamp(intensity, 0f, 1f);
            progress = MathHelper.Clamp(progress, 0f, 1f);
            _states[controllerNpcId] = new Entry {
                Mode = mode,
                Intensity = intensity,
                Progress = progress,
                Frame = Main.GameUpdateCount,
            };
            //顺带转发给机械氛围天空做警报/过载聚合，免去天空侧每帧遍历NPC
            MachineEffect.ReportSkyMood(mode, intensity, progress);
        }

        /// <summary>
        /// 读取某个控制器（Boss头部）当前视觉状态。
        /// 状态过期或未推送时返回常态低强度，避免画面残留。
        /// </summary>
        public static (MechBossVisualMode mode, float intensity, float progress) Read(int controllerNpcId) {
            if (!_states.TryGetValue(controllerNpcId, out var e)) {
                return (MechBossVisualMode.Idle, 0f, 0f);
            }
            if (Main.GameUpdateCount - e.Frame > 5) {
                return (MechBossVisualMode.Idle, 0f, 0f);
            }
            return (e.Mode, e.Intensity, e.Progress);
        }

        /// <summary>
        /// Boss死亡或场景重置时可调用，避免字典持续膨胀
        /// </summary>
        public static void Clear(int controllerNpcId) {
            _states.Remove(controllerNpcId);
        }
    }
}
