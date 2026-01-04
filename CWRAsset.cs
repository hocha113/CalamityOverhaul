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
        public static Asset<Texture2D> LightShot = null;
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> LightShotAlt = null;
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> Airflow = null;
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> Extra_193 = null;
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> Spray = null;
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> StarTexture_White = null;
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> StarTexture = null;
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> SoftGlow = null;//大小64*64的圆点灰度图，一般用于叠加绘制圆形光效，颜色的A值需要设置为0
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> Fire = null;
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> Fog = null;
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> Cyclone = null;
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> DiffusionCircle = null;
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> ThunderTrail = null;//这个纹理来自珊瑚石，谢谢你瓶中微光 :)
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> TileHightlight = null;//这个纹理来自珊瑚石，谢谢你瓶中微光 :)
        [VaultLoaden(CWRConstant.UI + "Generator/ElectricPower")]
        public static Asset<Texture2D> ElectricPower = null;
        [VaultLoaden(CWRConstant.UI + "Generator/ElectricPowerFull")]
        public static Asset<Texture2D> ElectricPowerFull = null;
        [VaultLoaden(CWRConstant.UI + "Generator/ElectricPowerGlow")]
        public static Asset<Texture2D> ElectricPowerGlow = null;
        [VaultLoaden(CWRConstant.UI + "Generator/GeneratorPanel")]
        internal static Asset<Texture2D> Panel { get; private set; }
        [VaultLoaden("@CalamityMod/UI/DraedonsArsenal/ChargerWeaponSlot")]
        internal static Asset<Texture2D> SlotTex { get; private set; }
        [VaultLoaden("@CalamityMod/UI/DraedonsArsenal/ChargeMeterBorder")]
        internal static Asset<Texture2D> BarTop { get; private set; }
        [VaultLoaden("@CalamityMod/UI/DraedonsArsenal/ChargeMeter")]
        internal static Asset<Texture2D> BarFull { get; private set; }
        [VaultLoaden("@CalamityMod/UI/DraedonsArsenal/PowerCellSlot_Empty")]
        internal static Asset<Texture2D> EmptySlot { get; private set; }
        [VaultLoaden("@CalamityMod/Particles/SemiCircularSmear")]
        public static Asset<Texture2D> SemiCircularSmear = null;
        [VaultLoaden("@CalamityMod/UI/MiscTextures/GenericBarBack")]
        public static Asset<Texture2D> GenericBarBack = null;
        [VaultLoaden("@CalamityMod/UI/MiscTextures/GenericBarFront")]
        public static Asset<Texture2D> GenericBarFront = null;
        [VaultLoaden("@CalamityMod/Particles/MediumMist")]
        public static Asset<Texture2D> MediumMist = null;
        [VaultLoaden("@CalamityMod/UI/DraedonSummoning/DraedonContactPanel")]
        public static Asset<Texture2D> DraedonContactPanel = null;
    }
}
