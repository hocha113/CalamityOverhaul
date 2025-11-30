using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Items.OldDuchests
{
    internal class OldDuchest : ModItem
    {
        public override string Texture => CWRConstant.Item_Placeable + "OldDuchest";
    }

    internal class OldDuchestTile : ModTile
    {
        //宽6格，高4格，共两帧，第一帧为关闭状态，第二帧为打开状态
        public override string Texture => CWRConstant.Item_Placeable + "OldDuchestTile";
    }
}
