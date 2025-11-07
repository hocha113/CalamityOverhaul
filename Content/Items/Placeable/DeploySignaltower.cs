using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Placeable
{
    internal class DeploySignaltower : ModItem
    {
        public override string Texture => CWRConstant.Item + "Placeable/DeploySignaltower";
    }

    internal class DeploySignaltowerTile : ModTile
    {
        public override string Texture => CWRConstant.Item + "Placeable/DeploySignaltowerTile";//6*14多格物块
    }
}
