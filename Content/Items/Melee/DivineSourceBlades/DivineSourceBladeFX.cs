using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;

namespace CalamityOverhaul.Content.Items.Melee.DivineSourceBlades
{
    /// <summary>
    /// 金源灭却刃特效资源、贴图锚点与蓝/金双色板
    /// </summary>
    internal static class DivineSourceBladeFX
    {
        public const string BladeTexture = CWRConstant.Item_Melee + "DivineSourceBlade";

        /// <summary>贴图 100×164 剑柄抓握点</summary>
        public static readonly Vector2 GripPixel = new(15f, 151f);
        /// <summary>贴图 100×164 剑尖</summary>
        public static readonly Vector2 TipPixel = new(94f, 8f);

        //刀身蓝色科技色板，取色自贴图刃部
        public static readonly Color TechWhite = new(235, 250, 255);
        public static readonly Color CyanBright = new(118, 222, 246);
        public static readonly Color AzureBlue = new(28, 132, 205);
        public static readonly Color ElectricBlue = new(36, 72, 200);
        public static readonly Color DeepNavy = new(10, 28, 104);

        //充能金色色板，取色自贴图护手金饰
        public static readonly Color AuricCream = new(255, 250, 226);
        public static readonly Color AuricGold = new(233, 210, 130);
        public static readonly Color AuricAmber = new(224, 148, 70);

        /// <summary>充能时把蓝系颜色向金色拉近</summary>
        public static Color Blend(Color blue, Color gold, float mix) => Color.Lerp(blue, gold, mix);

        public static Effect BladeGlow => EffectLoader.DivineSourceBladeGlow?.Value;
        public static Effect TechArc => EffectLoader.DivineSourceTechArc?.Value;
        public static Effect Crescent => EffectLoader.DivineSourceCrescent?.Value;
        public static Effect Impact => EffectLoader.DivineSourceImpact?.Value;

        public static Texture2D SoftGlow => CWRAsset.SoftGlow?.Value;
        public static Texture2D BlankStar => CWRAsset.StarTexture_White?.Value;
        public static Texture2D LightShot => CWRAsset.LightShot?.Value;
        //旧 shader 沿用原噪声链路，TechArc 在调用点显式绑 s1=PerlinNoise
        public static Texture2D Noise => CWRAsset.Fog?.Value ?? CWRAsset.PerlinNoise?.Value;
        public static Texture2D PerlinNoise => CWRAsset.PerlinNoise?.Value;
        public static Texture2D WaveFallback => CWRAsset.SemiCircularSmear?.Value;
    }
}
