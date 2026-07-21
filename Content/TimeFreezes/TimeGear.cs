using System;
using System.Collections.Generic;

namespace CalamityOverhaul.Content.TimeFreezes
{
    /// <summary>变速齿轮，多源 <see cref="TimeScale"/> 取最小值</summary>
    internal class TimeGear : ICWRLoader
    {
        void ICWRLoader.UnLoadData() => Reset();

        /// <summary>全局时间因子 0~1，多源取最小</summary>
        public static float TimeScale { get; private set; } = 1f;

        public static bool IsTimeSlowed => TimeScale < 1f;

        private static readonly Dictionary<string, float> scaleSources = new();

        /// <summary>注册缩放源，同 key 覆盖；0冻 1正常</summary>
        public static void Register(string key, float scale) {
            scaleSources[key] = MathHelper.Clamp(scale, 0f, 1f);
            Recalculate();
        }

        public static void Unregister(string key) {
            if (scaleSources.Remove(key)) {
                Recalculate();
            }
        }

        private static void Recalculate() {
            float min = 1f;
            foreach (var pair in scaleSources) {
                if (pair.Value < min) min = pair.Value;
            }
            TimeScale = min;
        }

        /// <summary>排除指定源后的因子，源自身免疫己缩放</summary>
        public static float TimeScaleExcluding(string excludeKey) {
            float min = 1f;
            foreach (var pair in scaleSources) {
                if (pair.Key == excludeKey) {
                    continue;
                }
                if (pair.Value < min) min = pair.Value;
            }
            return min;
        }

        public static void Reset() {
            scaleSources.Clear();
            TimeScale = 1f;
        }

        /// <summary>按 scale 扣倒计时；≤0 不推进；scale&lt;0 用 TimeScale</summary>
        public static void ConsumeFrames(ref int frames, ref float carry, float scale = -1f) {
            if (frames <= 0) {
                return;
            }
            float s = scale >= 0f ? scale : TimeScale;
            if (s <= 0f) {
                return;
            }
            carry += s;
            int tick = (int)carry;
            if (tick <= 0) {
                return;
            }
            carry -= tick;
            frames = Math.Max(0, frames - tick);
        }

        /// <summary>本帧推进整帧数(count-up)；≤0 返回 0</summary>
        public static int PullFrameAdvance(ref float carry, float scale = -1f) {
            float s = scale >= 0f ? scale : TimeScale;
            if (s <= 0f) {
                return 0;
            }
            carry += s;
            int tick = (int)carry;
            if (tick <= 0) {
                return 0;
            }
            carry -= tick;
            return tick;
        }
    }
}
