using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;

namespace CalamityOverhaul.Content.Demo
{
    /// <summary>Demo 特效贴图装载点：商业级手绘 VFX 素材，形状在 RGB 亮度或 Alpha 通道（见各注释）</summary>
    internal static class DemoAssets
    {
        //---- 刀光/月牙（亮度型：黑底白形，直接加色或作 mask）----
        /// <summary>直线笔刷拉丝，弧向平铺用（RGB+Alpha 双通道渐变）</summary>
        [VaultLoaden("CalamityOverhaul/Content/Demo/Textures/Slash/SlashBrush01")]
        public static Asset<Texture2D> SlashBrush01 { get; set; }
        /// <summary>硬边月牙填充（亮度型）</summary>
        [VaultLoaden("CalamityOverhaul/Content/Demo/Textures/Slash/CrescentEdge01")]
        public static Asset<Texture2D> CrescentEdge01 { get; set; }
        /// <summary>锯齿撕裂月牙笔刷（亮度型）</summary>
        [VaultLoaden("CalamityOverhaul/Content/Demo/Textures/Slash/SlashJagged01")]
        public static Asset<Texture2D> SlashJagged01 { get; set; }
        /// <summary>横向拉丝条纹（亮度型）</summary>
        [VaultLoaden("CalamityOverhaul/Content/Demo/Textures/Slash/SlashStreak01")]
        public static Asset<Texture2D> SlashStreak01 { get; set; }

        //---- 冲击爆点（亮度型）----
        /// <summary>放射状爆点尖刺</summary>
        [VaultLoaden("CalamityOverhaul/Content/Demo/Textures/Impact/RayBurst01")]
        public static Asset<Texture2D> RayBurst01 { get; set; }
        /// <summary>三向长条闪光</summary>
        [VaultLoaden("CalamityOverhaul/Content/Demo/Textures/Impact/RayCross01")]
        public static Asset<Texture2D> RayCross01 { get; set; }
        /// <summary>镜头光斑式星爆</summary>
        [VaultLoaden("CalamityOverhaul/Content/Demo/Textures/Impact/StarFlare01")]
        public static Asset<Texture2D> StarFlare01 { get; set; }
        /// <summary>致密核心星爆</summary>
        [VaultLoaden("CalamityOverhaul/Content/Demo/Textures/Impact/StarFlare02")]
        public static Asset<Texture2D> StarFlare02 { get; set; }
        /// <summary>四芒星光点（火花粒子用）</summary>
        [VaultLoaden("CalamityOverhaul/Content/Demo/Textures/Impact/StarGlow01")]
        public static Asset<Texture2D> StarGlow01 { get; set; }
        /// <summary>水平速度线大图（1024，随机截条）</summary>
        [VaultLoaden("CalamityOverhaul/Content/Demo/Textures/Impact/SpeedLines01")]
        public static Asset<Texture2D> SpeedLines01 { get; set; }
        /// <summary>命中火花序列帧 2×2×128（Alpha 型）</summary>
        [VaultLoaden("CalamityOverhaul/Content/Demo/Textures/Impact/HitSparkSheet01")]
        public static Asset<Texture2D> HitSparkSheet01 { get; set; }
        /// <summary>锯齿冲击撕裂形（Alpha 型）</summary>
        [VaultLoaden("CalamityOverhaul/Content/Demo/Textures/Impact/HitJagged01")]
        public static Asset<Texture2D> HitJagged01 { get; set; }

        //---- 撕裂/扩散形状 ----
        /// <summary>冲击扩散尖刺形（Alpha 型）</summary>
        [VaultLoaden("CalamityOverhaul/Content/Demo/Textures/Shape/TearSpread01")]
        public static Asset<Texture2D> TearSpread01 { get; set; }
        /// <summary>扩散环（白 RGB + Alpha 形状）</summary>
        [VaultLoaden("CalamityOverhaul/Content/Demo/Textures/Shape/Ring01")]
        public static Asset<Texture2D> Ring01 { get; set; }

        //---- 烟雾（白 RGB + Alpha 形状，AlphaBlend 染色用）----
        /// <summary>烟团序列帧 2×2×512</summary>
        [VaultLoaden("CalamityOverhaul/Content/Demo/Textures/Smoke/SmokeSheet01")]
        public static Asset<Texture2D> SmokeSheet01 { get; set; }
        /// <summary>拉丝烟缕（亮度型）</summary>
        [VaultLoaden("CalamityOverhaul/Content/Demo/Textures/Smoke/SmokeWisp01")]
        public static Asset<Texture2D> SmokeWisp01 { get; set; }

        //---- 噪声 ----
        /// <summary>柔性大块噪声，侵蚀/参差用（亮度型，可平铺）</summary>
        [VaultLoaden("CalamityOverhaul/Content/Demo/Textures/Noise/NoiseSoft01")]
        public static Asset<Texture2D> NoiseSoft01 { get; set; }
    }
}
