using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Placeable
{
    internal class StarflowPlatedBlock : ModItem
    {
        public override string Texture => CWRConstant.Item + "Placeable/StarflowPlatedBlock";
    }

    internal class StarflowPlatedBlockTile : ModTile
    {
        public override string Texture => CWRConstant.Item + "Placeable/StarflowPlatedBlock";//1*1单格物块，所以可以直接复用物品纹理作为物块纹理
    }
}
