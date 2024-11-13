using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;

namespace CalamityOverhaul
{
    internal class CWRAsset : ICWRLoader
    {
        internal static Asset<Texture2D> icon_small;
        internal static Asset<Texture2D> Dusts_SoulFire;
        void ICWRLoader.LoadAsset() {
            icon_small = CWRUtils.GetT2DAsset("CalamityOverhaul/icon_small");
            Dusts_SoulFire = CWRUtils.GetT2DAsset(CWRConstant.Dust + "SoulFire");
        }
        void ICWRLoader.UnLoadData() {
            icon_small = null;
            Dusts_SoulFire = null;
        }
    }
}
