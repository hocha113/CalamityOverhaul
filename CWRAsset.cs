using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;

namespace CalamityOverhaul
{
    internal class CWRAsset : ICWRLoader
    {
        public static Asset<Texture2D> icon_small;
        public static Asset<Texture2D> Dusts_SoulFire;
        public static Asset<Texture2D> IceParcloseAsset;
        void ICWRLoader.LoadAsset() {
            icon_small = CWRUtils.GetT2DAsset("CalamityOverhaul/icon_small");
            Dusts_SoulFire = CWRUtils.GetT2DAsset(CWRConstant.Dust + "SoulFire");
            IceParcloseAsset = CWRUtils.GetT2DAsset(CWRConstant.Projectile + "IceParclose");
        }
        void ICWRLoader.UnLoadData() {
            icon_small = null;
            Dusts_SoulFire = null;
            IceParcloseAsset = null;
        }
    }
}
