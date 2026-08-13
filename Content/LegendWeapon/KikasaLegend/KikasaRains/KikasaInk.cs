using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains
{
    /// <summary>
    /// 墨雨普攻的共享色板与小工具。墨黑为体、血色为芯——黑保住墨感,
    /// 血芯保住鬼伞血统;全部过 <see cref="KikasaDomain.CoolTint"/>,
    /// 域内鬼雨异化时自动冷化(域外恒为血侧)
    /// </summary>
    internal static class KikasaInk
    {
        /// <summary>墨体:近黑,微透血底</summary>
        public static Color InkBody => KikasaDomain.CoolTint(new(24, 13, 17), new(14, 18, 23));

        /// <summary>墨缘:比体略亮的暗血沿,给体积</summary>
        public static Color InkDeep => KikasaDomain.CoolTint(new(56, 20, 28), new(34, 44, 51));

        /// <summary>血芯:墨条中心透出的一线暗红</summary>
        public static Color BloodCore => KikasaDomain.CoolTint(new(172, 40, 42), new(98, 124, 132));

        /// <summary>湿反光:小面积 A=0 加色玻头</summary>
        public static Color WetSheen => KikasaDomain.CoolTint(new(238, 122, 106), new(178, 202, 208));

        /// <summary>确定性散列 0~1:绘制与多端一致的抖动都用它,不掷 Main.rand</summary>
        public static float Hash(int seed, int salt) {
            uint h = (uint)(seed * 374761393 + salt * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return (h ^ (h >> 16)) % 65536u / 65536f;
        }
    }
}
