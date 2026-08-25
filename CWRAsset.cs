using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;

namespace CalamityOverhaul
{
    internal class CWRAsset : ICWRLoader
    {
        [VaultLoaden("CalamityOverhaul/icon_small")]
        public static Asset<Texture2D> icon_small = null;
        [VaultLoaden(CWRConstant.Projectile + "IceParclose")]
        public static Asset<Texture2D> IceParcloseAsset = null;
        [VaultLoaden(CWRConstant.Asset + "Players/Quiver_back")]
        public static Asset<Texture2D> Quiver_back_Asset = null;
        [VaultLoaden(CWRConstant.Asset + "Players/IceGod_back")]
        public static Asset<Texture2D> IceGod_back_Asset = null;
        [VaultLoaden(CWRConstant.UI + "JAR")]
        public static Asset<Texture2D> UI_JAR = null;
        [VaultLoaden(CWRConstant.Other + "AimTarget")]
        public static Asset<Texture2D> AimTarget = null;
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> LightShot = null;//256×128箭头灰度，Additive，子弹/激光拖尾
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> LightShotAlt = null;//LightShot变体，尾部更紧凑
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> Airflow = null;//256×256横向流线灰度，风压/水流UV
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> Extra_193 = null;//256×256 Voronoi灰度，能量网格/护盾蒙版
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> Spray = null;//512×512烟雾3×3帧序列，喷射/粉尘粒子
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> StarTexture_White = null;//326×326白4芒星，Mask/乘色闪光
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> StarTexture = null;//326×326黑底4芒星，Additive重击/爆炸高光
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> SoftGlow = null;//64 圆点灰度，Additive 光晕，染色常 A=0
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> Fire = null;//512 火焰帧序列，Additive 粒子
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> Fog = null;//256 单帧烟羽，白RGB+真alpha，AlphaBlend 可直接染色；烟团统一用它
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> PerlinNoise = null;//512×512 Perlin灰度，Shader扭曲/溶解
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> Cyclone = null;//128×128同心旋涡，气旋/冲击波径向采样
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> DiffusionCircle = null;//360 扩散环，Additive 冲击波
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> ThunderTrail = null;//256×128闪电拖尾，Trail Shader(珊瑚石致谢)
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> TileHightlight = null;//153×153物块高亮蒙版(珊瑚石致谢)
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> MaskLaserLine = null;
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> SlashFlatBlurHVMirror = null;
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> DiffusionCircle3 = null;
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> TransverseTwill = null;
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> SplitTrail = null;
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> Extra_98 = null;
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> NormalMatrix = null;
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> Line = null;
        //---- 刀光/月牙(亮度型，黑底白形) ----
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> SlashBrush01 = null;//直线笔刷拉丝，弧向平铺用（RGB+Alpha双通道渐变）
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> CrescentEdge01 = null;//硬边月牙填充（亮度型）
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> SlashJagged01 = null;//锯齿撕裂月牙笔刷（亮度型）
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> SlashStreak01 = null;//横向拉丝条纹（亮度型）
        //---- 冲击爆点(亮度型) ----
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> RayBurst01 = null;//放射状爆点尖刺
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> RayCross01 = null;//三向长条闪光
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> StarFlare01 = null;//镜头光斑式星爆
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> StarFlare02 = null;//致密核心星爆
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> StarGlow01 = null;//四芒星光点（火花粒子用）
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> SpeedLines01 = null;//水平速度线大图（1024，随机截条）
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> HitSparkSheet01 = null;//命中火花序列帧2×2×128（Alpha型）
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> HitJagged01 = null;//锯齿冲击撕裂形（Alpha型）
        //---- 撕裂/扩散形状 ----
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> TearSpread01 = null;//冲击扩散尖刺形（Alpha型）
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> Ring01 = null;//名义扩散环，实为硬外缘+盘内灰雾斑的脏光盘；禁新增消费，环形冲击改用 ShockRing shader 或 DiffusionCircle4/5（VFX.md Ring01 禁令）
        //---- 烟雾(烟团见上方 Fog) ----
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> SmokeWisp01 = null;//拉丝烟缕（亮度型）
        //---- 噪声 ----
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> NoiseSoft01 = null;//柔性大块噪声，侵蚀/参差用（亮度型，可平铺）
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> Shutter = null;//无头人形 Alpha 遮罩，无头鬼影本体
        [VaultLoaden(CWRConstant.ColorBar)]
        public static Asset<Texture2D> BloodRed_Bar = null;
        [VaultLoaden(CWRConstant.ColorBar)]
        public static Asset<Texture2D> AbsoluteZero_Bar = null;
        [VaultLoaden(CWRConstant.ColorBar)]
        public static Asset<Texture2D> DragonRage_Bar = null;
        [VaultLoaden(CWRConstant.ColorBar)]
        public static Asset<Texture2D> DarklightGreatsword_Bar = null;
        [VaultLoaden(CWRConstant.ColorBar)]
        public static Asset<Texture2D> CursedflameFist_Bar = null;//咒焰血拳拖尾，亮绿核→锈橙→焦棕
        [VaultLoaden(CWRConstant.ColorBar)]
        public static Asset<Texture2D> BrinyBaron_Bar = null;
        [VaultLoaden(CWRConstant.ColorBar)]
        public static Asset<Texture2D> Excelsus_Bar = null;
        [VaultLoaden(CWRConstant.ColorBar)]
        public static Asset<Texture2D> AegisBlade_Bar = null;
        [VaultLoaden(CWRConstant.ColorBar)]
        public static Asset<Texture2D> Flawless_Bar = null;//青→墨青，化境刀光渐变
        [VaultLoaden("@CalamityMod/UI/DraedonsArsenal/ChargeMeterBorder")]
        internal static Asset<Texture2D> BarTop { get; private set; }
        [VaultLoaden("@CalamityMod/UI/DraedonsArsenal/ChargeMeter")]
        internal static Asset<Texture2D> BarFull { get; private set; }
        [VaultLoaden("@CalamityMod/Particles/SemiCircularSmear")]
        public static Asset<Texture2D> SemiCircularSmear = null;
        [VaultLoaden("@CalamityMod/UI/MiscTextures/GenericBarBack")]
        public static Asset<Texture2D> GenericBarBack = null;
        [VaultLoaden("@CalamityMod/UI/MiscTextures/GenericBarFront")]
        public static Asset<Texture2D> GenericBarFront = null;
        [VaultLoaden("@CalamityMod/UI/DraedonSummoning/DraedonContactPanel")]
        public static Asset<Texture2D> DraedonContactPanel = null;
    }
}
