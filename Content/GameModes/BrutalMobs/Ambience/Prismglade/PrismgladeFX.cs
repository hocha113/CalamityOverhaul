using Terraria;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Prismglade
{
    /// <summary>
    /// 神圣之地环境氛围的共用风味表：彩虹取色与妖精五色统一从这里取，
    /// 保证虹尘、妖精光球、圣晶折射、棱光审判四个家族色感一致（镜像 EvilBiomeFX 的风味表模式）
    /// </summary>
    internal static class PrismgladeFX
    {
        /// <summary>棱彩取色：色相自动回绕到 [0,1)</summary>
        public static Color Prism(float hue, float sat = 0.85f, float lum = 0.62f)
            => Main.hslToRgb((hue % 1f + 1f) % 1f, sat, lum);

        /// <summary>妖精五色（粉/青/绿/金/紫），光球逐只轮取</summary>
        public static readonly float[] FairyHues = [0.90f, 0.52f, 0.33f, 0.12f, 0.76f];
    }
}
