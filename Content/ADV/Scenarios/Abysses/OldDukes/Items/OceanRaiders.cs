using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Items
{
    internal class OceanRaiders : ModItem
    {
        public override string Texture => CWRConstant.Item_Placeable + "OceanRaiders";
    }

    internal class OceanRaidersTile : ModTile
    {
        //宽11格物块，高6格物块
        public override string Texture => CWRConstant.Item_Placeable + "OceanRaidersTile";
    }
}
