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
        [VaultLoaden(CWRConstant.Placeholder)]
        public static Asset<Texture2D> Placeholder_Transparent = null;
        [VaultLoaden(CWRConstant.Placeholder2)]
        public static Asset<Texture2D> Placeholder_White = null;
        [VaultLoaden(CWRConstant.Placeholder3)]
        public static Asset<Texture2D> Placeholder_ERROR = null;
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
        public static Asset<Texture2D> SoftGlow = null;//64*64的圆点灰度图（径向衰减），Additive叠加绘制圆形光晕/光源，染色时颜色A值通常设为0以避免遮挡
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> Fire = null;//512*512的火焰帧动画序列（多帧白色火苗黑底），Additive叠加作为火焰粒子、燃烧飘动的逐帧贴图
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> Fog = null;//256*256的柔性团状烟雾灰度蒙版，中心密集四周逐渐透明，适合叠加做烟尘、雾气、魔法蒸汽
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> PerlinNoise = null;//512×512 Perlin灰度，Shader扭曲/溶解
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> Cyclone = null;//128×128同心旋涡，气旋/冲击波径向采样
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> DiffusionCircle = null;//360*360的柔和圆环扩散蒙版，中心透明外围模糊光环，Additive叠加用作冲击波光圈、脉冲扩散特效
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
        [VaultLoaden(CWRConstant.ColorBar)]
        public static Asset<Texture2D> BloodRed_Bar = null;
        [VaultLoaden(CWRConstant.ColorBar)]
        public static Asset<Texture2D> AbsoluteZero_Bar = null;
        [VaultLoaden(CWRConstant.ColorBar)]
        public static Asset<Texture2D> DragonRage_Bar = null;
        [VaultLoaden(CWRConstant.ColorBar)]
        public static Asset<Texture2D> DarklightGreatsword_Bar = null;
        [VaultLoaden(CWRConstant.ColorBar)]
        public static Asset<Texture2D> BrinyBaron_Bar = null;
        [VaultLoaden(CWRConstant.ColorBar)]
        public static Asset<Texture2D> Excelsus_Bar = null;
        [VaultLoaden(CWRConstant.ColorBar)]
        public static Asset<Texture2D> AegisBlade_Bar = null;
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
