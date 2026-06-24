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

        /// <summary>TimeScale&lt;1 即有时缓</summary>
        public static bool IsTimeSlowed => TimeScale < 1f;

        private static readonly Dictionary<string, float> scaleSources = new();

        /// <summary>注册缩放源，同 key 覆盖</summary>
        /// <param name="key">源标识</param>
        /// <param name="scale">0 冻结，1 正常</param>
        public static void Register(string key, float scale) {
            scaleSources[key] = MathHelper.Clamp(scale, 0f, 1f);
            Recalculate();
        }

        /// <summary>移除缩放源</summary>
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

        /// <summary>排除指定源后的时间因子，供该源自身免疫自己的缩放（如世界 8% 但本人按外部时间结算）</summary>
        /// <param name="excludeKey">需要忽略的源标识</param>
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

        /// <summary>Unload 清空全部源</summary>
        public static void Reset() {
            scaleSources.Clear();
            TimeScale = 1f;
        }

        /// <summary>按 time scale 从整帧倒计时扣除；scale≤0 时不推进</summary>
        /// <param name="frames">剩余帧数</param>
        /// <param name="carry">小数余量，跨帧累积</param>
        /// <param name="scale">时间因子，&lt;0 时使用 <see cref="TimeScale"/></param>
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

        /// <summary>按 time scale 返回本帧应推进的整帧数（用于 count-up 计时）；scale≤0 时返回 0</summary>
        /// <param name="carry">小数余量，跨帧累积</param>
        /// <param name="scale">时间因子，&lt;0 时使用 <see cref="TimeScale"/></param>
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
