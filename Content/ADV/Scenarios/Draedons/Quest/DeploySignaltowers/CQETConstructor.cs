using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Draedons.Quest.DeploySignaltowers
{
    /// <summary>
    /// 量子塔自我构建器
    /// </summary>
    internal class CQETConstructor : ModItem
    {
        public override string Texture => CWRConstant.Item_Tools + "CQETConstructor";
    }

    internal class CQETConstructorTile : ModTile
    {
        public override string Texture => CWRConstant.Item_Tools + "CQETConstructorTile";//2*2大小的实体物块
    }

    //检测到量子塔自我构建器后进行的处理
    //检测左边两格为StarflowPlatedBlock，右侧两格为StarflowPlatedBlock，并往上检测高14格都为StarflowPlatedBlock
    //也就是需要6*14-4=80个StarflowPlatedBlock
    //如果满足，则将这些物块替换为量子塔，也就是移除自己，并在原地放置量子塔 DeploySignaltowerTile，这个多结构物块也是6*14大小，刚好够填充
    internal class CQETConstructorTP : TileProcessor
    {
        public override int TargetTileID => ModContent.TileType<CQETConstructorTile>();

        public override void Update() {
            
        }

        public override void BackDraw(SpriteBatch spriteBatch) {
            
        }
    }
}
