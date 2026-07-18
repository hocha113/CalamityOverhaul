using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI
{
    /// <summary>
    /// 气力快照：玩法层每帧上报的原始读数。
    /// UI 侧的回切/洇进/残痕/脉冲等动画状态全部由 HUD 自行推导，不进本结构
    /// </summary>
    internal readonly struct OniVigorSnapshot
    {
        /// <summary>当前气力</summary>
        public readonly float Value;
        /// <summary>气力上限</summary>
        public readonly float MaxValue;

        public OniVigorSnapshot(float value, float maxValue) {
            Value = value;
            MaxValue = maxValue;
        }

        /// <summary>0~1 填充比，上限非正视为空</summary>
        public float Ratio => MaxValue > 0f ? MathHelper.Clamp(Value / MaxValue, 0f, 1f) : 0f;
    }

    /// <summary>气力数据源契约。玩法层实现后经 <see cref="OniVigor.SetSource"/> 挂接</summary>
    internal interface IOniVigorSource
    {
        /// <summary>取该玩家当前气力，返回 false 表示本帧无读数（HUD 回落演示源）</summary>
        bool TryGetVigor(Player player, out OniVigorSnapshot snapshot);
    }

    /// <summary>
    /// 气力数据入口。玩法层就绪后用 <see cref="SetSource"/> 挂接真实数据源——数值本体应住在
    /// ModPlayer 上（不得存 static，多人下会串号），本类只持有"提供者"；
    /// 未挂接时走演示源，预览与调教 HUD 动画用
    /// </summary>
    internal static class OniVigor
    {
        private static IOniVigorSource source;
        private static readonly OniVigorPreviewSource preview = new();

        /// <summary>挂接真实玩法数据源；传 null 回落演示源</summary>
        public static void SetSource(IOniVigorSource s) => source = s;

        /// <summary>取玩家气力读数（HUD 每帧一次）</summary>
        public static OniVigorSnapshot Get(Player player) {
            if (source != null && source.TryGetVigor(player, out OniVigorSnapshot snap)) {
                return snap;
            }
            preview.TryGetVigor(player, out OniVigorSnapshot demo);
            return demo;
        }
    }

    /// <summary>
    /// 演示源：缓慢回气 + 随机出招耗气的编舞，专为预览消耗残痕、洇墨恢复、回满收笔等动画。
    /// 仅本地 UI 预览，不进任何玩法/网络路径
    /// </summary>
    internal sealed class OniVigorPreviewSource : IOniVigorSource
    {
        private const float Max = 100f;
        private float value = 72f;
        private int spendCountdown = 300;
        private int lastTick = -1;

        public bool TryGetVigor(Player player, out OniVigorSnapshot snapshot) {
            //UI 的 Update 可能一帧内多次取值，按逻辑帧推进
            int tick = (int)Main.GameUpdateCount;
            if (tick != lastTick) {
                lastTick = tick;
                value = Math.Min(Max, value + 0.055f);
                if (--spendCountdown <= 0) {
                    spendCountdown = Main.rand.Next(300, 640);
                    value = Math.Max(0f, value - Main.rand.NextFloat(16f, 40f));
                }
            }
            snapshot = new OniVigorSnapshot(value, Max);
            return true;
        }
    }
}
