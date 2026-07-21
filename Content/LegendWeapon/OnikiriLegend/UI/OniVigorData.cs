using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI
{
    /// <summary>气力快照,玩法层每帧原始读数,动画由 HUD 自推</summary>
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

        /// <summary>0~1 填充比,上限非正视为空</summary>
        public float Ratio => MaxValue > 0f ? MathHelper.Clamp(Value / MaxValue, 0f, 1f) : 0f;
    }

    /// <summary>气力数据源,经 <see cref="OniVigor.SetSource"/> 挂接</summary>
    internal interface IOniVigorSource
    {
        /// <summary>取气力,false=本帧无读数(HUD 回落演示源)</summary>
        bool TryGetVigor(Player player, out OniVigorSnapshot snapshot);
    }

    /// <summary>
    /// 气力入口.<see cref="SetSource"/> 挂真实源(数值住 ModPlayer,禁 static);
    /// 未挂接走演示源
    /// </summary>
    internal static class OniVigor
    {
        private static IOniVigorSource source;
        private static readonly OniVigorPreviewSource preview = new();

        /// <summary>挂真实源,null 回落演示</summary>
        public static void SetSource(IOniVigorSource s) => source = s;

        /// <summary>取气力读数(HUD 每帧)</summary>
        public static OniVigorSnapshot Get(Player player) {
            if (source != null && source.TryGetVigor(player, out OniVigorSnapshot snap)) {
                return snap;
            }
            preview.TryGetVigor(player, out OniVigorSnapshot demo);
            return demo;
        }
    }

    /// <summary>演示源,本地 UI 预览用,不进玩法/网络</summary>
    internal sealed class OniVigorPreviewSource : IOniVigorSource
    {
        private const float Max = 100f;
        private float value = 72f;
        private int spendCountdown = 300;
        private int lastTick = -1;

        public bool TryGetVigor(Player player, out OniVigorSnapshot snapshot) {
            //Update 可能同帧多次取值,按逻辑帧推进
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
