using CalamityOverhaul.Content.Scenarios.Kiyume.Gen;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Scenarios.Kiyume.Fog
{
    /// <summary>
    /// 雾线潮汐：一条水平的雾面在退潮行与涨潮行之间长周期涨落。<br/>
    /// 主周期三分钟，叠一层不整除的副周期，免得读成节拍器。<br/>
    /// 计时器进世界归零，且从<b>涨满</b>起步，加载屏最后一眼是雾海淹掉村子，
    /// 落地第一眼就该接着那一帧，然后雾退下去把村子还给你。入场演出就是这条潮汐本身。<br/>
    /// 本轮纯表现，各端各算不发包；将来玩法真去消费浓度时，这条时钟必须换成服务器权威的
    /// </summary>
    internal static class KiyumeFogTide
    {
        /// <summary>主周期（tick）：涨一次退一次三分钟</summary>
        internal const int CycleTicks = 10800;
        /// <summary>副周期（tick）：与主周期不整除，叠出不规则的呼吸</summary>
        internal const int WobbleTicks = 4300;
        /// <summary>副周期振幅（潮位比例）</summary>
        internal const float WobbleAmp = 0.22f;

        private static long ticks;

        /// <summary>潮位 0=退潮（雾贴地）1=涨潮（村子沉进雾海）</summary>
        internal static float Tide { get; private set; }

        /// <summary>当前雾线基准世界 Y（px），未含湖侧倾斜</summary>
        internal static float LineWorldY { get; private set; } = KiyumeMetrics.FogLineLowRow * 16f;

        internal static void Reset() {
            ticks = 0;
            Tide = 1f;
            LineWorldY = KiyumeMetrics.FogLineHighRow * 16f;
        }

        internal static void Update() {
            ticks++;
            Tide = KiyumeFogDebug.ForceTide >= 0f
                ? Math.Clamp(KiyumeFogDebug.ForceTide, 0f, 1f)
                : Evaluate(ticks * Math.Max(KiyumeFogDebug.TideSpeedMul, 0.01f));

            //主世界看样：世界行是鬼梦的，主世界够不着，把雾线锚到玩家脚下再让潮位驱动上下摆
            float span = (KiyumeMetrics.FogLineLowRow - KiyumeMetrics.FogLineHighRow) * 16f;
            if (!KiyumeWorld.Active && KiyumeFogDebug.ForceEnable && Main.LocalPlayer?.active == true) {
                LineWorldY = Main.LocalPlayer.Bottom.Y + 96f - Tide * span;
                return;
            }
            LineWorldY = MathHelper.Lerp(
                KiyumeMetrics.FogLineLowRow, KiyumeMetrics.FogLineHighRow, Tide) * 16f;
        }

        //主周期降余弦（从 1 起，进世界正在退潮）+ 副周期正弦扰动
        private static float Evaluate(float t) {
            float main = 0.5f + 0.5f * MathF.Cos(t / CycleTicks * MathHelper.TwoPi);
            float wobble = MathF.Sin(t / WobbleTicks * MathHelper.TwoPi);
            return MathHelper.Clamp(main + wobble * WobbleAmp * 0.5f, 0f, 1f);
        }

        /// <summary>该列的雾面世界 Y：越靠湖抬得越高，雾是从湖里蒸上来的</summary>
        internal static float SurfaceAt(float worldX) {
            float t = MathHelper.Clamp(
                1f - (worldX - KiyumeMetrics.LakeRightPx) / KiyumeMetrics.TiltSpanPx, 0f, 1f);
            float lean = t * t * (3f - 2f * t);
            return LineWorldY - KiyumeMetrics.LakeTiltPx * lean;
        }

        internal static string StatusLine() =>
            $"潮位{Tide:F2} 雾线行{LineWorldY / 16f:F0}"
            + (KiyumeFogDebug.ForceTide >= 0f ? "(钉死)" : $" t={ticks}");
    }
}
