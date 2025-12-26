using Microsoft.Xna.Framework.Graphics;

namespace CalamityOverhaul.Content.LegendWeapon.MurasamaLegend.MurasamaProj
{
    /// <summary>
    /// 村正次元斩终结技资源加载器
    /// </summary>
    [VaultLoaden(CWRConstant.Masking)]
    internal class MuraSlayAllAssets
    {
        /// <summary>
        /// 透明贴图路径
        /// </summary>
        public static string TransparentImg => CWRConstant.Placeholder2;

        /// <summary>
        /// 三角形碎片贴图
        /// </summary>
        public static Texture2D Triangle { get; private set; }

        /// <summary>
        /// 半圆弧贴图
        /// </summary>
        public static Texture2D Roundtry { get; private set; }

        /// <summary>
        /// 半圆弧变体贴图
        /// </summary>
        public static Texture2D Roundtry2 { get; private set; }

        /// <summary>
        /// 斩击线贴图
        /// </summary>
        public static Texture2D Iinetry { get; private set; }

        /// <summary>
        /// 屏幕颜色错乱shader
        /// </summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Effect ScreenColorMess { get; private set; }

        /// <summary>
        /// 径向模糊shader
        /// </summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Effect RadialBlur { get; private set; }

        /// <summary>
        /// 滤镜shader
        /// </summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Effect Filter { get; private set; }
    }
}
