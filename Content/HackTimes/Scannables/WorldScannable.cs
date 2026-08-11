using CalamityOverhaul.Content.HackTimes.Targets;
using System;
using Terraria;
using Terraria.Localization;

namespace CalamityOverhaul.Content.HackTimes.Scannables
{
    /// <summary>
    /// 世界状态扫描目标。没有实体——kind 本身就是身份，任意两个
    /// <see cref="WorldScannable"/> 视作同一目标（<see cref="TargetEquals"/>）。<br/>
    /// <see cref="WorldCenter"/> 只是悬停时捕获的天空锚点，用于锁定框与运镜；
    /// 协议逻辑一律不吃这个座标（反查出的实例拿到的是世界中央的兜底锚点）
    /// </summary>
    internal class WorldScannable : IHackTarget
    {
        private readonly Vector2 anchor;

        /// <summary>反查/快照侧构造，锚点落在世界中央地表上空（仅表现用途）</summary>
        public WorldScannable() {
            anchor = FallbackAnchor();
        }

        /// <summary>悬停侧构造，锚点 = 指向的天空世界坐标</summary>
        public WorldScannable(Vector2 skyAnchor) {
            anchor = skyAnchor;
        }

        public Vector2 WorldCenter => anchor;

        //世界总在，目标永远有效——效果生命周期只由时长决定
        public bool IsValid => true;

        public bool IsHackable => true;

        #region 扫描面板

        public int ScanRowCount => 7;

        public void BuildScanData(string[] labels, string[] values, Color[] colors) {
            labels[0] = WorldTargetType.ScanWorld.Value;
            values[0] = Main.worldName ?? "?";
            colors[0] = HackTheme.TextBright;

            labels[1] = WorldTargetType.ScanClock.Value;
            values[1] = BuildClockText();
            colors[1] = Main.dayTime ? HackTheme.Uploading : HackTheme.AccentAlt;

            labels[2] = WorldTargetType.ScanWeather.Value;
            values[2] = BuildWeatherText();
            colors[2] = Main.raining ? HackTheme.AccentAlt : HackTheme.TextBright;

            labels[3] = WorldTargetType.ScanMoon.Value;
            values[3] = GetMoonPhaseText();
            colors[3] = HackTheme.TextBright;

            labels[4] = WorldTargetType.ScanBiome.Value;
            values[4] = GetLocalBiomeText();
            colors[4] = HackTheme.Accent;

            labels[5] = WorldTargetType.ScanEvent.Value;
            values[5] = GetEventText(out bool eventActive);
            colors[5] = eventActive ? HackTheme.Danger : HackTheme.TextDim;

            labels[6] = WorldTargetType.ScanBoss.Value;
            int bossCount = CountActiveBosses();
            values[6] = bossCount.ToString();
            colors[6] = bossCount > 0 ? HackTheme.Danger : HackTheme.TextDim;
        }

        #endregion

        #region IHackTarget

        public HackTargetType TargetType => HackTargetType.Get<WorldTargetType>();

        public Vector2 LockFrameHalfSize => new(66f, 42f);

        public string LockFrameTitle => Main.worldName ?? string.Empty;

        public bool TryGetLockFrameStatus(out string text, out Color color) {
            text = BuildClockText();
            color = Main.dayTime ? HackTheme.Uploading : HackTheme.AccentAlt;
            return true;
        }

        public bool ApplyHack(QuickHackDef hack, Player caster) {
            int casterIndex = caster?.whoAmI ?? Main.myPlayer;
            //世界目标没有专用入口，直接走统一权威入口
            return HackEffectTracker.ApplyAuthorityEffect(hack, this, casterIndex,
                0, 0, 0f, 0) != null;
        }

        //单例语义：世界只有一个
        public bool TargetEquals(IHackTarget other) => other is WorldScannable;

        #endregion

        #region 悬停判定

        /// <summary>
        /// 指向天空：世界坐标在地表线以上、脚下无 tile 也无液体。
        /// 有 tile/液体时物块与液体目标本就按优先级先接管，这里再排除一次
        /// 只是让"空白天空"的语义自洽
        /// </summary>
        public static bool TryGetScannableSky(Vector2 worldPos) {
            int tileX = (int)(worldPos.X / 16f);
            int tileY = (int)(worldPos.Y / 16f);
            if (tileX < 0 || tileX >= Main.maxTilesX
                || tileY < 0 || tileY >= Main.maxTilesY) {
                return false;
            }
            if (tileY >= Main.worldSurface) return false;
            Tile tile = Main.tile[tileX, tileY];
            return !tile.HasTile && tile.LiquidAmount == 0;
        }

        private static Vector2 FallbackAnchor() {
            float surfaceY = (float)(Main.worldSurface * 16.0) - 640f;
            return new Vector2(Main.maxTilesX * 8f, Math.Max(surfaceY, 640f));
        }

        #endregion

        #region 世界状态读数

        //Main.time 换算 24 小时制，昼起点 4:30
        private static string BuildClockText() {
            double time = Main.time;
            if (!Main.dayTime) time += 54000.0;
            time = time / 86400.0 * 24.0 - 19.5;
            if (time < 0.0) time += 24.0;
            int hours = (int)time;
            int minutes = (int)((time - hours) * 60.0);
            string half = Main.dayTime
                ? WorldTargetType.DayLabel.Value
                : WorldTargetType.NightLabel.Value;
            return $"{hours:D2}:{minutes:D2} {half}";
        }

        private static string BuildWeatherText() {
            string weather;
            if (Main.raining) {
                weather = Main.maxRaining >= 0.6f
                    ? WorldTargetType.WeatherStorm.Value
                    : WorldTargetType.WeatherRain.Value;
            }
            else if (Main.cloudAlpha > 0f) {
                weather = WorldTargetType.WeatherCloudy.Value;
            }
            else {
                weather = WorldTargetType.WeatherClear.Value;
            }
            int wind = (int)Math.Abs(Main.windSpeedCurrent * 100f);
            string arrow = Main.windSpeedCurrent < 0f ? "<" : ">";
            return $"{weather} / {arrow}{wind}mph";
        }

        private static string GetMoonPhaseText() {
            //复用原版六分仪的月相文案，白嫖全语言本地化
            string key = Main.moonPhase switch {
                0 => "GameUI.FullMoon",
                1 => "GameUI.WaningGibbous",
                2 => "GameUI.ThirdQuarter",
                3 => "GameUI.WaningCrescent",
                4 => "GameUI.NewMoon",
                5 => "GameUI.WaxingCrescent",
                6 => "GameUI.FirstQuarter",
                _ => "GameUI.WaxingGibbous",
            };
            return Language.GetTextValue(key);
        }

        //面板只在本机绘制，读本机玩家所处的群系
        private static string GetLocalBiomeText() {
            Player player = Main.LocalPlayer;
            if (player == null || !player.active) {
                return WorldTargetType.BiomeForest.Value;
            }
            if (player.ZoneDungeon) return WorldTargetType.BiomeDungeon.Value;
            if (player.ZoneUnderworldHeight) return WorldTargetType.BiomeUnderworld.Value;
            if (player.ZoneCorrupt) return WorldTargetType.BiomeCorruption.Value;
            if (player.ZoneCrimson) return WorldTargetType.BiomeCrimson.Value;
            if (player.ZoneHallow) return WorldTargetType.BiomeHallow.Value;
            if (player.ZoneGlowshroom) return WorldTargetType.BiomeMushroom.Value;
            if (player.ZoneJungle) return WorldTargetType.BiomeJungle.Value;
            if (player.ZoneSnow) return WorldTargetType.BiomeSnow.Value;
            if (player.ZoneDesert) return WorldTargetType.BiomeDesert.Value;
            if (player.ZoneBeach) return WorldTargetType.BiomeOcean.Value;
            return WorldTargetType.BiomeForest.Value;
        }

        private static string GetEventText(out bool eventActive) {
            eventActive = true;
            if (Main.bloodMoon) return WorldTargetType.EventBloodMoon.Value;
            if (Main.eclipse) return WorldTargetType.EventEclipse.Value;
            if (Main.pumpkinMoon) return WorldTargetType.EventPumpkinMoon.Value;
            if (Main.snowMoon) return WorldTargetType.EventFrostMoon.Value;
            if (Main.slimeRain) return WorldTargetType.EventSlimeRain.Value;
            if (Main.invasionType > 0) return WorldTargetType.EventInvasion.Value;
            eventActive = false;
            return WorldTargetType.EventNone.Value;
        }

        internal static int CountActiveBosses() {
            int count = 0;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc.active && npc.boss) count++;
            }
            return count;
        }

        #endregion
    }
}
