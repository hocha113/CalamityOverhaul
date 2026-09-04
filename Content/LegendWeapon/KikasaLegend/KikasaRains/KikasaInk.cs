using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains
{
    /// <summary>
    /// 墨雨普攻的共享色板与小工具。墨黑为体、血色为芯，黑保住墨感,
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

        //鬼青三件:伞下鬼与鬼滴的魂色,不随域形态冷暖走，鬼火自有其色
        /// <summary>鬼滴墨体:近黑偏青</summary>
        public static readonly Color GhostBody = new(16, 26, 30);

        /// <summary>鬼滴墨缘</summary>
        public static readonly Color GhostDeep = new(34, 66, 72);

        /// <summary>鬼滴芯:青白鬼火,替掉血芯</summary>
        public static readonly Color GhostCore = new(150, 220, 214);

        //浓血四件:血湖形态(KikasaBloodForm)的血珠/血柱/血索色板,不过 CoolTint——
        //血形态只在非鬼雨存在。取值锚定血月祭坛三阶(#2A0407→#6B0B12→#A8121C)与
        //领域血滴色,珠子要读作"湖里的血"而不是另一种红;血是暗的,没有白芯
        /// <summary>血体:暗血,比湖面沉一档</summary>
        public static readonly Color BloodBody = new(78, 10, 15);

        /// <summary>血缘:表面张力挂边,最暗最饱和</summary>
        public static readonly Color BloodDeep = new(40, 4, 8);

        /// <summary>血亮:体心相对亮的鲜血</summary>
        public static readonly Color BloodBright = new(150, 18, 26);

        /// <summary>血湿光:各向异性窄反射带,小面积</summary>
        public static readonly Color BloodSheen = new(232, 108, 94);

        /// <summary>凝血:入水后的珠体,红进红要靠更沉的色读出轮廓</summary>
        public static readonly Color BloodClot = new(34, 5, 9);

        /// <summary>确定性散列 0~1:绘制与多端一致的抖动都用它,不掷 Main.rand</summary>
        public static float Hash(int seed, int salt) {
            uint h = (uint)(seed * 374761393 + salt * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return (h ^ (h >> 16)) % 65536u / 65536f;
        }

        //==================== 普攻音效 ====================
        //不拿 SoundID.Drip / Waterfall:两者是 Ambient 洞穴滴水,战斗里等于没声。
        //一律原版:伞骨闷扫 / 水矢甩墨 / 溅水 / 水枪喷流。Splash 默认 IgnoreNew,连打会吞,这里一律 ReplaceOldest。

        /// <summary>撑伞/翻面/收伞:伞骨闷扫</summary>
        internal static SoundStyle UmbrellaWhoosh => SoundID.DD2_MonkStaffSwing;

        /// <summary>甩雨出手:水矢甩墨</summary>
        internal static SoundStyle InkFlick => SoundID.Item21;

        /// <summary>泼溅/落点/蓄墨换档:溅水</summary>
        internal static SoundStyle InkSplash => SoundID.Splash;

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
