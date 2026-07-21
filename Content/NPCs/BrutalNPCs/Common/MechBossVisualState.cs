using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.Common
{
    /// <summary>视觉状态，按 whoAmI 索引</summary>
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

        /// <summary>Push 控制器视觉</summary>
        public static void Push(int controllerNpcId, MechBossVisualMode mode, float intensity, float progress = 0f) {
            intensity = MathHelper.Clamp(intensity, 0f, 1f);
            progress = MathHelper.Clamp(progress, 0f, 1f);
            _states[controllerNpcId] = new Entry {
                Mode = mode,
                Intensity = intensity,
                Progress = progress,
                Frame = Main.GameUpdateCount,
            };
            //转发天空警报聚合
            MachineEffect.ReportSkyMood(mode, intensity, progress);
        }

        /// <summary>Read，过期回 Idle</summary>
        public static (MechBossVisualMode mode, float intensity, float progress) Read(int controllerNpcId) {
            if (!_states.TryGetValue(controllerNpcId, out var e)) {
                return (MechBossVisualMode.Idle, 0f, 0f);
            }
            if (Main.GameUpdateCount - e.Frame > 5) {
                return (MechBossVisualMode.Idle, 0f, 0f);
            }
            return (e.Mode, e.Intensity, e.Progress);
        }

        /// <summary>Clear，防字典膨胀</summary>
        public static void Clear(int controllerNpcId) {
            _states.Remove(controllerNpcId);
        }
    }
}
