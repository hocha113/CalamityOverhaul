using InnoVault.TileProcessors;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Tools
{
    internal class SirenMusicalBox : ModItem
    {
        public override string Texture => CWRConstant.Item + "Tools/SirenMusicalBox";
    }

    internal class SirenMusicalBoxTile : ModTile
    {
        public override string Texture => CWRConstant.Item + "Tools/SirenMusicalBoxTile";
    }

    internal class SirenMusicalBoxTP : TileProcessor
    {
        public override int TargetTileID => ModContent.TileType<SirenMusicalBoxTile>();
    }
}
