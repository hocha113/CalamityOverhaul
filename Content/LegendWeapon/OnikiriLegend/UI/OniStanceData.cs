using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI
{
    /// <summary>架势快照,玩法层每帧原始读数,演出由 HUD 自推</summary>
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

    /// <summary>架势数据源,经 <see cref="OniStance.SetSource"/> 挂接</summary>
    internal interface IOniStanceSource
    {
        /// <summary>取架势,false=本帧无读数(HUD 回落演示源)</summary>
        bool TryGetStance(Player player, out OniStanceSnapshot snapshot);
    }

    /// <summary>
    /// 架势入口.<see cref="SetSource"/> 挂真实源(数值住 ModPlayer,禁 static);
    /// 未挂接走演示源
    /// </summary>
    internal static class OniStance
    {
        private static IOniStanceSource source;
        private static readonly OniStancePreviewSource preview = new();

        /// <summary>挂真实源,null 回落演示</summary>
        public static void SetSource(IOniStanceSource s) => source = s;

        /// <summary>取架势读数(HUD 每帧)</summary>
        public static OniStanceSnapshot Get(Player player) {
            if (source != null && source.TryGetStance(player, out OniStanceSnapshot snap)) {
                return snap;
            }
            preview.TryGetStance(player, out OniStanceSnapshot demo);
            return demo;
        }
    }

    /// <summary>演示源,本地 UI 预览用,不进玩法/网络</summary>
    internal sealed class OniStancePreviewSource : IOniStanceSource
    {
        private const float Max = 100f;
        private float value;
        private int gainCountdown = 60;
        private int fullHold;
        private int lastTick = -1;

        public bool TryGetStance(Player player, out OniStanceSnapshot snapshot) {
            //Update 可能同帧多次取值,按逻辑帧推进
            int tick = (int)Main.GameUpdateCount;
            if (tick != lastTick) {
                lastTick = tick;
                if (value >= Max) {
                    //满势驻留约三秒后模拟拔刀
                    if (++fullHold >= 200) {
                        fullHold = 0;
                        value = 0f;
                    }
                }
                else if (--gainCountdown <= 0) {
                    gainCountdown = Main.rand.Next(24, 80);
                    value = Math.Min(Max, value + Main.rand.NextFloat(5f, 13f));
                    //偶发耗势,预览短促回鞘
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
