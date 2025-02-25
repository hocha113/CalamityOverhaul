using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;

namespace CalamityOverhaul
{
    internal class CWRAsset : ICWRLoader
    {
        public static Asset<Texture2D> icon_small;
        public static Asset<Texture2D> IceParcloseAsset;
        public static Asset<Texture2D> Quiver_back_Asset;
        public static Asset<Texture2D> IceGod_back_Asset;
        public static Asset<Texture2D> Placeholder_Transparent;
        public static Asset<Texture2D> Placeholder_White;
        public static Asset<Texture2D> Placeholder_ERROR;
        public static Asset<Texture2D> SemiCircularSmear;
        public static Asset<Texture2D> UI_JAR;
        public static Asset<Texture2D> UI_JMF;
        public static Asset<Texture2D> AimTarget;
        public static Asset<Texture2D> GenericBarBack;
        public static Asset<Texture2D> GenericBarFront;
        public static Asset<Texture2D> MediumMist;
        public static Asset<Texture2D> LightShot;
        public static Asset<Texture2D> LightShotAlt;
        public static Asset<Texture2D> Airflow;
        public static Asset<Texture2D> Extra_193;
        public static Asset<Texture2D> DraedonContactPanel;
        public static Asset<Texture2D> StarTexture_White;
        void ICWRLoader.LoadAsset() {
            icon_small = CWRUtils.GetT2DAsset("CalamityOverhaul/icon_small");
            IceParcloseAsset = CWRUtils.GetT2DAsset(CWRConstant.Projectile + "IceParclose");
            Quiver_back_Asset = CWRUtils.GetT2DAsset(CWRConstant.Asset + "Players/Quiver_back");
            IceGod_back_Asset = CWRUtils.GetT2DAsset(CWRConstant.Asset + "Players/IceGod_back");
            Placeholder_Transparent = CWRUtils.GetT2DAsset(CWRConstant.Placeholder);
            Placeholder_White = CWRUtils.GetT2DAsset(CWRConstant.Placeholder2);
            Placeholder_ERROR = CWRUtils.GetT2DAsset(CWRConstant.Placeholder3);
            SemiCircularSmear = CWRUtils.GetT2DAsset("CalamityMod/Particles/SemiCircularSmear");
            UI_JAR = CWRUtils.GetT2DAsset(CWRConstant.UI + "JAR");
            UI_JMF = CWRUtils.GetT2DAsset(CWRConstant.UI + "JMF");
            AimTarget = CWRUtils.GetT2DAsset(CWRConstant.Other + "AimTarget");
            GenericBarBack = CWRUtils.GetT2DAsset("CalamityMod/UI/MiscTextures/GenericBarBack");
            GenericBarFront = CWRUtils.GetT2DAsset("CalamityMod/UI/MiscTextures/GenericBarFront");
            MediumMist = CWRUtils.GetT2DAsset("CalamityMod/Particles/MediumMist");
            LightShot = CWRUtils.GetT2DAsset(CWRConstant.Masking + "LightShot");
            LightShotAlt = CWRUtils.GetT2DAsset(CWRConstant.Masking + "LightShotAlt");
            Airflow = CWRUtils.GetT2DAsset(CWRConstant.Masking + "Airflow");
            Extra_193 = CWRUtils.GetT2DAsset(CWRConstant.Masking + "Extra_193");
            DraedonContactPanel = CWRUtils.GetT2DAsset("CalamityMod/UI/DraedonSummoning/DraedonContactPanel");
            StarTexture_White = CWRUtils.GetT2DAsset(CWRConstant.Masking + "StarTexture_White");
        }
        void ICWRLoader.UnLoadData() {
            icon_small = null;
            IceParcloseAsset = null;
            Quiver_back_Asset = null;
            IceGod_back_Asset = null;
            Placeholder_Transparent = null;
            Placeholder_White = null;
            Placeholder_ERROR = null;
            SemiCircularSmear = null;
            UI_JAR = null;
            UI_JMF = null;
            AimTarget = null;
            GenericBarBack = null;
            GenericBarFront = null;
            MediumMist = null;
            LightShot = null;
            Airflow = null;
            Extra_193 = null;
            DraedonContactPanel = null;
            StarTexture_White = null;
        }
    }
}
