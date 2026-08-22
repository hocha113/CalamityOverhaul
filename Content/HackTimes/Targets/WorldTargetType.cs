using CalamityOverhaul.Content.HackTimes.Scannables;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.HackTimes.Targets
{
    /// <summary>
    /// 世界状态目标。没有实体，指向地表线以上的空白天空即命中，
    /// 优先级压到液体(-10)之下做全场兜底
    /// </summary>
    internal class WorldTargetType : HackTargetType
    {
        public override HackTargetKind Kind => HackTargetKind.World;

        public override int HoverPriority => -20;

        #region 扫描面板文案（键随 HackTargetType.WorldTargetType.* 走）

        public static LocalizedText ScanWorld { get; private set; }
        public static LocalizedText ScanClock { get; private set; }
        public static LocalizedText ScanWeather { get; private set; }
        public static LocalizedText ScanMoon { get; private set; }
        public static LocalizedText ScanBiome { get; private set; }
        public static LocalizedText ScanEvent { get; private set; }
        public static LocalizedText ScanBoss { get; private set; }

        public static LocalizedText DayLabel { get; private set; }
        public static LocalizedText NightLabel { get; private set; }
        public static LocalizedText WeatherClear { get; private set; }
        public static LocalizedText WeatherCloudy { get; private set; }
        public static LocalizedText WeatherRain { get; private set; }
        public static LocalizedText WeatherStorm { get; private set; }
        public static LocalizedText EventNone { get; private set; }
        public static LocalizedText EventBloodMoon { get; private set; }
        public static LocalizedText EventEclipse { get; private set; }
        public static LocalizedText EventPumpkinMoon { get; private set; }
        public static LocalizedText EventFrostMoon { get; private set; }
        public static LocalizedText EventSlimeRain { get; private set; }
        public static LocalizedText EventInvasion { get; private set; }

        public static LocalizedText BiomeForest { get; private set; }
        public static LocalizedText BiomeDesert { get; private set; }
        public static LocalizedText BiomeSnow { get; private set; }
        public static LocalizedText BiomeJungle { get; private set; }
        public static LocalizedText BiomeCorruption { get; private set; }
        public static LocalizedText BiomeCrimson { get; private set; }
        public static LocalizedText BiomeHallow { get; private set; }
        public static LocalizedText BiomeDungeon { get; private set; }
        public static LocalizedText BiomeMushroom { get; private set; }
        public static LocalizedText BiomeOcean { get; private set; }
        public static LocalizedText BiomeUnderworld { get; private set; }

        public override void SetStaticDefaults() {
            ScanWorld = this.GetLocalization(nameof(ScanWorld), () => "World");
            ScanClock = this.GetLocalization(nameof(ScanClock), () => "Time");
            ScanWeather = this.GetLocalization(nameof(ScanWeather), () => "Weather");
            ScanMoon = this.GetLocalization(nameof(ScanMoon), () => "Moon Phase");
            ScanBiome = this.GetLocalization(nameof(ScanBiome), () => "Biome");
            ScanEvent = this.GetLocalization(nameof(ScanEvent), () => "Event");
            ScanBoss = this.GetLocalization(nameof(ScanBoss), () => "Active Bosses");

            DayLabel = this.GetLocalization(nameof(DayLabel), () => "Day");
            NightLabel = this.GetLocalization(nameof(NightLabel), () => "Night");
            WeatherClear = this.GetLocalization(nameof(WeatherClear), () => "Clear");
            WeatherCloudy = this.GetLocalization(nameof(WeatherCloudy), () => "Cloudy");
            WeatherRain = this.GetLocalization(nameof(WeatherRain), () => "Rain");
            WeatherStorm = this.GetLocalization(nameof(WeatherStorm), () => "Storm");
            EventNone = this.GetLocalization(nameof(EventNone), () => "Stable");
            EventBloodMoon = this.GetLocalization(nameof(EventBloodMoon), () => "Blood Moon");
            EventEclipse = this.GetLocalization(nameof(EventEclipse), () => "Solar Eclipse");
            EventPumpkinMoon = this.GetLocalization(nameof(EventPumpkinMoon), () => "Pumpkin Moon");
            EventFrostMoon = this.GetLocalization(nameof(EventFrostMoon), () => "Frost Moon");
            EventSlimeRain = this.GetLocalization(nameof(EventSlimeRain), () => "Slime Rain");
            EventInvasion = this.GetLocalization(nameof(EventInvasion), () => "Invasion");

            BiomeForest = this.GetLocalization(nameof(BiomeForest), () => "Forest");
            BiomeDesert = this.GetLocalization(nameof(BiomeDesert), () => "Desert");
            BiomeSnow = this.GetLocalization(nameof(BiomeSnow), () => "Tundra");
            BiomeJungle = this.GetLocalization(nameof(BiomeJungle), () => "Jungle");
            BiomeCorruption = this.GetLocalization(nameof(BiomeCorruption), () => "Corruption");
            BiomeCrimson = this.GetLocalization(nameof(BiomeCrimson), () => "Crimson");
            BiomeHallow = this.GetLocalization(nameof(BiomeHallow), () => "Hallow");
            BiomeDungeon = this.GetLocalization(nameof(BiomeDungeon), () => "Dungeon");
            BiomeMushroom = this.GetLocalization(nameof(BiomeMushroom), () => "Glowing Mushroom");
            BiomeOcean = this.GetLocalization(nameof(BiomeOcean), () => "Ocean");
            BiomeUnderworld = this.GetLocalization(nameof(BiomeUnderworld), () => "Underworld");
        }

        #endregion

        public override IHackTarget TryDetectHovered(Vector2 mouseWorld) {
            if (!WorldScannable.TryGetScannableSky(mouseWorld)) {
                return null;
            }
            return new WorldScannable(mouseWorld);
        }
    }
}
