using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.Common
{
    /// <summary>机械Boss视觉状态容器(按 whoAmI 索引)</summary>
    /// <para>Destroyer/Prime/Twins 头部各自 Push，体节 Draw Read；&gt;5 帧未刷新过期</para>
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

        /// <summary>Push 控制器(通常 Boss 头)视觉状态</summary>
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

        /// <summary>Read 控制器视觉状态；过期/未推送返回 Idle 零强度</summary>
        public static (MechBossVisualMode mode, float intensity, float progress) Read(int controllerNpcId) {
            if (!_states.TryGetValue(controllerNpcId, out var e)) {
                return (MechBossVisualMode.Idle, 0f, 0f);
            }
            if (Main.GameUpdateCount - e.Frame > 5) {
                return (MechBossVisualMode.Idle, 0f, 0f);
            }
            return (e.Mode, e.Intensity, e.Progress);
        }

        /// <summary>Boss 死亡/场景重置时 Clear，防字典膨胀</summary>
        public static void Clear(int controllerNpcId) {
            _states.Remove(controllerNpcId);
        }
    }
}
