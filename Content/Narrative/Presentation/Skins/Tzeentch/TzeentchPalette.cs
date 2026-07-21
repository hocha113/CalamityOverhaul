using System;

namespace CalamityOverhaul.Content.Narrative.Presentation.Skins.Tzeentch
{
    /// <summary>色板对齐 TzeentchPanel.fx</summary>
    internal static class TzeentchPalette
    {
        //深色基底,对齐着色器
        public static readonly Color Void = new(7, 4, 14);
        public static readonly Color Deep = new(19, 11, 42);
        public static readonly Color DeepEdge = new(44, 24, 86);

        //前景变数色
        public static readonly Color Azure = new(60, 150, 255);
        public static readonly Color Violet = new(150, 80, 240);
        public static readonly Color Magenta = new(235, 70, 190);
        public static readonly Color Gold = new(245, 205, 110);
        public static readonly Color Halo = new(210, 230, 255);

        /// <summary>天蓝→蓝紫→品红循环,phase 任意</summary>
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
