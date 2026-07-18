using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI
{
    /// <summary>
    /// 架势快照：玩法层每帧上报的原始读数。
    /// 拔刀/回鞘/满势点火等演出全部由 HUD 从数值变化自行推导，不进本结构
    /// </summary>
    internal readonly struct OniStanceSnapshot
    {
        /// <summary>当前架势</summary>
        public readonly float Value;
        /// <summary>架势上限</summary>
        public readonly float MaxValue;

        public OniStanceSnapshot(float value, float maxValue) {
            Value = value;
            MaxValue = maxValue;
        }

        /// <summary>0~1 拔刀比,上限非正视为空</summary>
        public float Ratio => MaxValue > 0f ? MathHelper.Clamp(Value / MaxValue, 0f, 1f) : 0f;
    }

    /// <summary>架势数据源契约。玩法层实现后经 <see cref="OniStance.SetSource"/> 挂接</summary>
    internal interface IOniStanceSource
    {
        /// <summary>取该玩家当前架势，返回 false 表示本帧无读数（HUD 回落演示源）</summary>
        bool TryGetStance(Player player, out OniStanceSnapshot snapshot);
    }

    /// <summary>
    /// 架势数据入口。玩法层就绪后用 <see cref="SetSource"/> 挂接真实数据源——数值本体应住在
    /// ModPlayer 上（不得存 static，多人下会串号），本类只持有"提供者"；
    /// 未挂接时走演示源，预览与调教 HUD 动画用
    /// </summary>
    internal static class OniStance
    {
        private static IOniStanceSource source;
        private static readonly OniStancePreviewSource preview = new();

        /// <summary>挂接真实玩法数据源；传 null 回落演示源</summary>
        public static void SetSource(IOniStanceSource s) => source = s;

        /// <summary>取玩家架势读数（HUD 每帧一次）</summary>
        public static OniStanceSnapshot Get(Player player) {
            if (source != null && source.TryGetStance(player, out OniStanceSnapshot snap)) {
                return snap;
            }
            preview.TryGetStance(player, out OniStanceSnapshot demo);
            return demo;
        }
    }

    /// <summary>
    /// 演示源：战斗式随机小增量蓄势 → 满后驻留片刻模拟拔刀清零，偶发中招花掉一截。
    /// 让蓄势爬动、满势点火、拔刀闪、归座与部分回鞘不接玩法也能全部预览。
    /// 仅本地 UI 预览，不进任何玩法/网络路径
    /// </summary>
    internal sealed class OniStancePreviewSource : IOniStanceSource
    {
        private const float Max = 100f;
        private float value;
        private int gainCountdown = 60;
        private int fullHold;
        private int lastTick = -1;

        public bool TryGetStance(Player player, out OniStanceSnapshot snapshot) {
            //UI 的 Update 可能一帧内多次取值，按逻辑帧推进
            int tick = (int)Main.GameUpdateCount;
            if (tick != lastTick) {
                lastTick = tick;
                if (value >= Max) {
                    //满架势驻留约三秒后模拟拔刀释放
                    if (++fullHold >= 200) {
                        fullHold = 0;
                        value = 0f;
                    }
                }
                else if (--gainCountdown <= 0) {
                    gainCountdown = Main.rand.Next(24, 80);
                    value = Math.Min(Max, value + Main.rand.NextFloat(5f, 13f));
                    //偶发出招:花掉一截架势,预览短促回鞘
                    if (value > 55f && value < Max && Main.rand.NextBool(7)) {
                        value = Math.Max(0f, value - Main.rand.NextFloat(22f, 36f));
                    }
                }
            }
            snapshot = new OniStanceSnapshot(value, Max);
            return true;
        }
    }
}
