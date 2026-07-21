using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;

namespace CalamityOverhaul.Content.Items.Melee.DivineSourceBlades
{
    /// <summary>
    /// 神源之刃特效资源与贴图锚点（由 AurumSlash 系统移植）
    /// </summary>
    internal static class DivineSourceBladeFX
    {
        public const string BladeTexture = CWRConstant.Item_Melee + "DivineSourceBlade";

        /// <summary>贴图 100×164 剑柄抓握点</summary>
        public static readonly Vector2 GripPixel = new(15f, 151f);
        /// <summary>贴图 100×164 剑尖</summary>
        public static readonly Vector2 TipPixel = new(94f, 8f);

        public static Effect BladeGlow => EffectLoader.DivineSourceBladeGlow?.Value;
        public static Effect Arc => EffectLoader.DivineSourceArc?.Value;
        public static Effect Crescent => EffectLoader.DivineSourceCrescent?.Value;
        public static Effect Impact => EffectLoader.DivineSourceImpact?.Value;

        public static Texture2D SoftGlow => CWRAsset.SoftGlow?.Value;
        public static Texture2D BlankStar => CWRAsset.StarTexture_White?.Value;
        public static Texture2D LightShot => CWRAsset.LightShot?.Value;
        public static Texture2D Noise => CWRAsset.Fog?.Value ?? CWRAsset.PerlinNoise?.Value;
        public static Texture2D WaveFallback => CWRAsset.SemiCircularSmear?.Value;
    }
}
