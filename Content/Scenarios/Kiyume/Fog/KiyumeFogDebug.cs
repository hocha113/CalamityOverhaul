namespace CalamityOverhaul.Content.Scenarios.Kiyume.Fog
{
    /// <summary>
    /// 调试与调参静态口（用户是第一 QA）：主世界强制开雾、钉死潮位、全部时间常数与图层强度热调。<br/>
    /// TestItem 触发片段在交付报告里，不入库
    /// </summary>
    public static class KiyumeFogDebug
    {
        /// <summary>主世界强制开雾预览（伪装 KiyumeWorld.Active，仅本地表现）</summary>
        public static bool ForceEnable;

        /// <summary>≥0 时把潮位钉死在该值（0=退潮 1=涨潮）；-1 走真实潮汐</summary>
        public static float ForceTide = -1f;
        /// <summary>潮汐倍速：看涨落效果时拉到 20 以上，别真等三分钟</summary>
        public static float TideSpeedMul = 1f;

        //===模拟时间常数===

        /// <summary>驱散半衰期（tick）：清雾区/潮退时雾让开的速度</summary>
        public static float DispelHalfLifeTicks = 8f;
        /// <summary>驱散结束后雾保持不动的延迟（tick）</summary>
        public static float RegatherDelayTicks = 40f;
        /// <summary>回聚半衰期（tick）：潮涨时雾漫回来的速度，慢于驱散</summary>
        public static float RegatherHalfLifeTicks = 30f;
        /// <summary>全局浓度倍率</summary>
        public static float DensityMul = 1f;

        //===渲染强度===

        /// <summary>背景雾层（PostDrawTiles，砖与敌人之上、玩家之下）不透明度系数</summary>
        public static float BackLayerAlpha = 0.80f;
        /// <summary>前景瘴气层（Filters.Scene）不透明度系数</summary>
        public static float FrontLayerAlpha = 0.40f;
        /// <summary>雾面亮边强度：这是"雾有水位"最直接的视觉证据，调 0 就退化成一团均质雾</summary>
        public static float SurfaceGlow = 1f;
        /// <summary>雾吃光强度：亮点在雾里晕开而不是穿透过来</summary>
        public static float EatLight = 0.85f;
        /// <summary>吃光晕开半径（屏幕px）</summary>
        public static float EatSpread = 9f;

        //潮位轮换表：退潮 / 半涨 / 涨满 / 交给真实潮汐
        private static readonly float[] cycleTides = [0f, 0.5f, 1f, -1f];
        private static readonly string[] cycleNames = [
            "退潮(雾贴地,只淹滩涂)", "半涨(村子淹到窗口)", "涨满(只剩屋顶)", "关闭(走真实潮汐)"
        ];
        private static int cycleIndex = -1;

        /// <summary>轮换钉死潮位（配合 ForceEnable 在主世界逐档看雾），返回当前档描述</summary>
        public static string CycleTide() {
            cycleIndex = (cycleIndex + 1) % cycleTides.Length;
            ForceTide = cycleTides[cycleIndex];
            return $"[鬼梦雾] 潮位: {cycleNames[cycleIndex]}";
        }

        /// <summary>一行状态摘要（窗口/浓度/潮位/抑制数），挂 tooltip 或日志用</summary>
        public static string StatusLine() => KiyumeFogSim.StatusLine();
    }
}
