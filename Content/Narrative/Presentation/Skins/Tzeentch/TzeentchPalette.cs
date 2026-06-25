using System;

namespace CalamityOverhaul.Content.Narrative.Presentation.Skins.Tzeentch
{
    /// <summary>奸奇皮肤统一色板：深色基底对齐 TzeentchPanel.fx，鲜明变数色供前景轮转</summary>
    internal static class TzeentchPalette
    {
        //深色基底:CPU 回退背景使用,与着色器主色保持一致
        public static readonly Color Void = new(7, 4, 14);
        public static readonly Color Deep = new(19, 11, 42);
        public static readonly Color DeepEdge = new(44, 24, 86);

        //鲜明变数色:前景粒子/辉光/描边轮转使用
        public static readonly Color Azure = new(60, 150, 255);
        public static readonly Color Violet = new(150, 80, 240);
        public static readonly Color Magenta = new(235, 70, 190);
        public static readonly Color Gold = new(245, 205, 110);
        public static readonly Color Halo = new(210, 230, 255);

        /// <summary>变数循环：天蓝→蓝紫→品红 无缝轮转，phase 任意实数</summary>
        public static Color Cycle(float phase) {
            phase -= (float)Math.Floor(phase);
            float seg = phase * 3f;
            if (seg < 1f) {
                return Color.Lerp(Azure, Violet, seg);
            }
            if (seg < 2f) {
                return Color.Lerp(Violet, Magenta, seg - 1f);
            }
            return Color.Lerp(Magenta, Azure, seg - 2f);
        }
    }
}
