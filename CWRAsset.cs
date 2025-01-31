using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

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
        public static Asset<Texture2D> AimTarget;
        public static Asset<Texture2D> GenericBarBack;
        public static Asset<Texture2D> GenericBarFront;
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
            AimTarget = CWRUtils.GetT2DAsset(CWRConstant.Other + "AimTarget");
            GenericBarBack = CWRUtils.GetT2DAsset("CalamityMod/UI/MiscTextures/GenericBarBack");
            GenericBarFront = CWRUtils.GetT2DAsset("CalamityMod/UI/MiscTextures/GenericBarFront");
            TextureAssets.Item[ItemID.IceSickle] = CWRUtils.GetT2DAsset(CWRConstant.Item_Melee + "IceSickle");
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
            AimTarget = null;
            GenericBarBack = null;
            GenericBarFront = null;
        }
    }
}
