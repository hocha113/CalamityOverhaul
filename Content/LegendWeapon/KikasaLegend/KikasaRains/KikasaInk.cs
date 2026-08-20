using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

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

        //==================== 普攻音效 ====================
        //不拿 SoundID.Drip / Waterfall:两者是 Ambient 洞穴滴水,战斗里等于没声。
        //灾厄在场走湿掌/水花;缺灾厄回退原版水矢/溅水。Splash 默认 IgnoreNew,连打会吞,这里一律 ReplaceOldest。

        /// <summary>撑伞/翻面/收伞:伞骨闷扫</summary>
        internal static SoundStyle UmbrellaWhoosh
            => "CalamityMod/Sounds/Item/SwooshMid".GetSound(SoundID.DD2_MonkStaffSwing);

        /// <summary>甩雨出手:湿掌甩墨</summary>
        internal static SoundStyle InkFlick
            => $"CalamityMod/Sounds/Custom/WetSlap{Main.rand.Next(1, 5)}".GetSound(SoundID.Item21);

        /// <summary>泼溅/落点/蓄墨换档:水花</summary>
        internal static SoundStyle InkSplash
            => $"CalamityMod/Sounds/Item/WaterSplash{(Main.rand.NextBool() ? 1 : 2)}".GetSound(SoundID.Splash);

        /// <summary>倾覆开闸:水枪喷流,给墨瀑一条持续感</summary>
        internal static SoundStyle InkSpray => SoundID.Item13;

        internal static void Play(SoundStyle style, Vector2 pos, float volume, float pitch, int maxInstances = 3) {
            SoundEngine.PlaySound(style with {
                Volume = volume,
                Pitch = pitch,
                MaxInstances = maxInstances,
                SoundLimitBehavior = SoundLimitBehavior.ReplaceOldest
            }, pos);
        }
    }
}
