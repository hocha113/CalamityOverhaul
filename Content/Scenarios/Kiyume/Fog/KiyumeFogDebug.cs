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
        public static float EatSpread = 32f;
        /// <summary>血湖水面烬光反射带强度：近景唯一锐利水平线</summary>
        public static float WaterGlow = 1f;

        //===雾吃光染色（CPU 密度纹理侧）===

        /// <summary>暗处雾可见度地板：0 全黑、1 无明暗差。压低才拉得开亮暗对比</summary>
        public static float LightVisFloor = 0.18f;
        /// <summary>亮处雾向烬色偏移的最大插值：窗火把周围的雾烘成暖团的力度</summary>
        public static float LightTintStrength = 0.85f;

        //===玩家推雾===

        /// <summary>玩家身位推雾半径（px）</summary>
        public static float PlayerPushRadius = 40f;
        /// <summary>玩家推雾羽化带宽（px）</summary>
        public static float PlayerPushFeather = 140f;
        /// <summary>玩家推雾强度：1=身位处全清，0.7=压到三成，贴身留薄雾更有氛围</summary>
        public static float PlayerPushStrength = 0.7f;

        //===贴地残雾与瀑布雾（P1-pkg1）===

        /// <summary>贴地雾带高（px，≈7 tile）。与 KiyumeHoundMetrics.GroundFogBandPx 数值同源，改一处必改两处</summary>
        public static float GroundFogHeightPx = 110f;
        /// <summary>地下裙边（px），盖坡坎接缝</summary>
        public static float GroundFogSkirtPx = 30f;
        /// <summary>贴地雾带体不透明度系数</summary>
        public static float GroundFogAlpha = 0.55f;
        /// <summary>潮汐露出跨度（px）：雾线上方 6 tile 内从 0 长回满强。
        /// 与 KiyumeHoundMetrics.GroundExposeSpanPx 数值同源，改一处必改两处</summary>
        public static float GroundFogExposeSpanPx = 96f;
        /// <summary>贴地雾风相（px/s，正=向东）：雾从湖里上岸往村里渗</summary>
        public static float GroundWindPxPerSec = 12f;
        /// <summary>瀑口落差阈值（px，4 tile 覆盖檐口）</summary>
        public static float FogFallThresholdPx = 64f;
        /// <summary>每屏雾帘数上限（按落差降序取）</summary>
        public static int FogFallMax = 6;

        //===雾海质感与体积（P1-pkg2）===

        /// <summary>浪冠撕裂混合：rim 被高频噪声撕成断续浪光，0=旧的连续亮线</summary>
        public static float CrestBreak = 0.8f;
        /// <summary>近表半透：雾面下 120px 内主体透明度让出的比例（刚沉的屋顶影影绰绰），0=旧遮蔽。
        /// 感知差方向偏袒玩家（看着略可见、实际已隐蔽），裁决 2 免侦测联动</summary>
        public static float NearSurfaceGhost = 0.26f;
        /// <summary>灯火倒影强度：沉在雾面下的窗火在海面拉竖条反光（仅背景通道）</summary>
        public static float ReflectGlow = 0.6f;
        /// <summary>蒸腾柱化幅度：湖上雾墙一柱一柱升腾（仅背景通道）</summary>
        public static float SteamColumnar = 0.44f;
        /// <summary>远带体积雾不透明度：0=整层关闭（降级开关，恢复两层现状）</summary>
        public static float FarLayerAlpha = 0.30f;
        /// <summary>远带视差系数：相机平移折算比，越小离得越远</summary>
        public static float FarParallax = 0.85f;
        /// <summary>斜射天光楔强度：血暮天漏下的烬色光柱（仅滤镜通道），0=无痕关闭</summary>
        public static float GodrayStrength = 0.7f;

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
