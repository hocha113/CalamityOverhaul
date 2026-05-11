using System.Collections.Generic;

namespace CalamityOverhaul.Content.TimeFreezes
{
    /// <summary>
    /// 变速齿轮 —— 全局时间速度管理器
    /// <br/>多个时缓效果（骇客时间冻结、斯安威斯坦时缓等）通过此系统注册各自的时间缩放系数
    /// <br/>其他系统（赛博空间、弹幕特效等）读取 <see cref="TimeScale"/> 来适配伪时缓效果
    /// <br/>多个源同时生效时，取最慢的（最小值）
    /// </summary>
    internal class TimeGear : ICWRLoader
    {
        void ICWRLoader.UnLoadData() => Reset();

        /// <summary>
        /// 当前全局时间速度因子 (0=完全冻结, 1=正常速度)
        /// <br/>多个时缓源取最小值
        /// </summary>
        public static float TimeScale { get; private set; } = 1f;

        /// <summary>
        /// 是否有任何时缓效果正在生效
        /// </summary>
        public static bool IsTimeSlowed => TimeScale < 1f;

        private static readonly Dictionary<string, float> scaleSources = new();

        /// <summary>
        /// 注册一个时间缩放源，多个源同时生效时取最小值
        /// </summary>
        /// <param name="key">缩放源标识符，相同key会覆盖旧值</param>
        /// <param name="scale">缩放系数，0=完全冻结，1=正常速度</param>
        public static void Register(string key, float scale) {
            scaleSources[key] = MathHelper.Clamp(scale, 0f, 1f);
            Recalculate();
        }

        /// <summary>
        /// 移除一个时间缩放源
        /// </summary>
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

        /// <summary>
        /// 立即重置所有状态
        /// </summary>
        public static void Reset() {
            scaleSources.Clear();
            TimeScale = 1f;
        }
    }
}
