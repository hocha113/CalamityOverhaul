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
        [VaultLoaden(CWRConstant.UI + "JMF")]
        public static Asset<Texture2D> UI_JMF = null;
        [VaultLoaden(CWRConstant.Other + "AimTarget")]
        public static Asset<Texture2D> AimTarget = null;
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> LightShot = null;//256*128的箭头状光束灰度图，从右端点向左发散成彗星/光锥尾迹，Additive叠加用于子弹闪光、冲击光斑、激光/导弹拖尾
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> LightShotAlt = null;//256*128的LightShot变体，尾部更平直紧凑，用作需要更细瘦拖尾时的替代，使用方式同LightShot
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> Airflow = null;//256*256的横向流线柔和灰度噪声，明暗带呈水平波浪走向，适合做风压、气流、水流的扭曲蒙版或滚动UV采样
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> Extra_193 = null;//256*256的类Voronoi细胞/网状灰度图，白色脊线围住暗色细胞，适合做能量网格、护盾裂纹、电浆场的扭曲/发光蒙版
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> Spray = null;//512*512的3x3不规则烟雾碎块序列帧（含透明通道），用于喷射、爆裂碎片、粉尘飞溅的帧动画粒子
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> StarTexture_White = null;//326*326的白色4芒星（十字耀斑）图，带透明度用于Mask或乘色叠加，通过SpriteBatch.Color染色后得到任意色闪光
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> StarTexture = null;//326*326的黑底白色4芒星（十字耀斑），需Additive叠加绘制，常用于重击/爆炸/宝物闪光点的核心高光
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> SoftGlow = null;//64*64的圆点灰度图（径向衰减），Additive叠加绘制圆形光晕/光源，染色时颜色A值通常设为0以避免遮挡
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> Fire = null;//512*512的火焰帧动画序列（多帧白色火苗黑底），Additive叠加作为火焰粒子、燃烧飘动的逐帧贴图
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> Fog = null;//256*256的柔性团状烟雾灰度蒙版，中心密集四周逐渐透明，适合叠加做烟尘、雾气、魔法蒸汽
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> PerlinNoise = null;//512*512的柏林噪声灰度图，低频平滑云状纹理，常用于Shader采样做扭曲偏移、流体UV、溶解边缘
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> Cyclone = null;//128*128的同心圆旋涡纹理（白底灰环），用于气旋、冲击波、涡流的极坐标/径向采样或旋转叠加
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> DiffusionCircle = null;//360*360的柔和圆环扩散蒙版，中心透明外围模糊光环，Additive叠加用作冲击波光圈、脉冲扩散特效
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> ThunderTrail = null;//256*128的闪电/能量拖尾灰度图，用于PrimitiveDrawing的Trail Shader采样，这个纹理来自珊瑚石，谢谢你瓶中微光 :)
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> TileHightlight = null;//153*153的物块高亮描边蒙版，用于Tile悬停/交互时的高光外框叠加，这个纹理来自珊瑚石，谢谢你瓶中微光 :)
        [VaultLoaden(CWRConstant.UI + "Generator/ElectricPower")]
        public static Asset<Texture2D> ElectricPower = null;
        [VaultLoaden(CWRConstant.UI + "Generator/ElectricPowerFull")]
        public static Asset<Texture2D> ElectricPowerFull = null;
        [VaultLoaden(CWRConstant.UI + "Generator/ElectricPowerGlow")]
        public static Asset<Texture2D> ElectricPowerGlow = null;
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
