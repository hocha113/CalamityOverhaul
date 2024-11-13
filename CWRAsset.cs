using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;

namespace CalamityOverhaul
{
    internal class CWRAsset : ICWRLoader
    {
        internal static Asset<Texture2D> icon_small;
        void ICWRLoader.LoadAsset() {
            icon_small = CWRUtils.GetT2DAsset("CalamityOverhaul/icon_small");
        }
        void ICWRLoader.UnLoadData() {
            icon_small = null;
        }
    }
}
