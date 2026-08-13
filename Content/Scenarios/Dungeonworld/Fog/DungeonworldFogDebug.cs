namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Fog
{
    /// <summary>
    /// 调试与调参静态口（用户是第一 QA）：主世界强制开雾、伪造深度、全部时间常数热调。<br/>
    /// TestItem 触发片段在交付报告里（镜像 DungeonworldPreview"片段在报告"惯例），不入库
    /// </summary>
    public static class DungeonworldFogDebug
    {
        /// <summary>主世界强制开雾预览（伪装 Dungeonworld.Active，仅本地表现）</summary>
        public static bool ForceEnable;

        /// <summary>≥0 时全屏按此世界行采样浓度曲线（伪造深度）；-1 用雾元真实行</summary>
        public static float FakeWorldRow = -1f;

        //===模拟时间常数（默认值与 FOG.md §2.2 参数表同源）===

        /// <summary>亮度→驱散强度；bright≥1/该值 全清</summary>
        public static float LightDispel = 1.25f;
        /// <summary>驱散半衰期（tick）：6 → 半秒后残留 3%</summary>
        public static float DispelHalfLifeTicks = 6f;
        /// <summary>光离开后雾保持不动的延迟（tick）</summary>
        public static float RegatherDelayTicks = 48f;
        /// <summary>回聚半衰期（tick）：延迟结束后 ~1.2s 合拢到 95%</summary>
        public static float RegatherHalfLifeTicks = 24f;
        /// <summary>全局浓度倍率</summary>
        public static float DensityMul = 1f;
        /// <summary>背景雾层（PostDrawTiles，砖与敌人之上、玩家之下）不透明度系数</summary>
        public static float BackLayerAlpha = 0.78f;
        /// <summary>前景瘴气层（Filters.Scene）不透明度系数</summary>
        public static float FrontLayerAlpha = 0.42f;

        //伪造深度轮换表：L3/L4/L5/L6/L7/深渊 各层代表行 + 关闭
        private static readonly float[] cycleRows = [1000f, 2200f, 3400f, 4800f, 5490f, 5860f, -1f];
        private static readonly string[] cycleNames = [
            "L3 档案馆(纸尘灰)", "L4 水牢(湿沼绿)", "L5 万骨窖(骨白)",
            "L6 机关层(炉锈橙·峰值)", "L7 倒吊教堂(冥紫·稀薄)", "深渊带(渊紫压黑)", "关闭(用真实行)"
        ];
        private static int cycleIndex = -1;

        /// <summary>轮换伪造层（配合 ForceEnable 在主世界逐层看雾），返回当前层描述</summary>
        public static string CycleLayer() {
            cycleIndex = (cycleIndex + 1) % cycleRows.Length;
            FakeWorldRow = cycleRows[cycleIndex];
            return $"[深牢迷雾] 伪造深度: {cycleNames[cycleIndex]}";
        }

        /// <summary>一行状态摘要（窗口/浓度/抑制数），挂 tooltip 或日志用</summary>
        public static string StatusLine() => DungeonworldFogSim.StatusLine();
    }
}
