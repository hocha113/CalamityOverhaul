using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.SupCal.End.EternalBlazingNows
{
    internal class EbnBarsDisplay : ModResourceDisplaySet
    {
        //反射加载心脏的纹理
        [VaultLoaden(CWRConstant.ADV)]
        public static Asset<Texture2D> EbnLife;//单颗心脏的填充部分，大小22*22
        [VaultLoaden(CWRConstant.ADV)]
        public static Asset<Texture2D> EbnLifeBack;//单颗心脏的背景部分，大小30*30，也就是说，边框宽度是4
    }
}
