using CalamityOverhaul.Common;
using CalamityOverhaul.Content.QuestLogs.Core;
using CalamityOverhaul.Content.QuestLogs.QLNodes;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.TileModify
{
    internal class ModifyShadowOrb : GlobalTile
    {
        public override void KillTile(int i, int j, int type, ref bool fail, ref bool effectOnly, ref bool noItem) {
            Tile tile = Framing.GetTileSafely(i, j);
            if (CWRServerConfig.Instance.QuestLog && tile.TileFrameX <= 0 && tile.TileFrameY <= 0) {
                var node = QuestNode.GetQuest<FindShadowOrb>();
                if (node is not null && node.IsUnlocked && node.Objectives?.Count > 0) {
                    node.Objectives[0].CurrentProgress = 1;
                }
            }
        }
    }
}
