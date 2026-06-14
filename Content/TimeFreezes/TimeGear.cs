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

        /// <summary>Unload 清空全部源</summary>
        public static void Reset() {
            scaleSources.Clear();
            TimeScale = 1f;
        }
    }
}
