using CalamityOverhaul.Content.QuestLogs.Core;
using CalamityOverhaul.Content.QuestLogs.QLNodes;
using InnoVault.TileProcessors;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;

namespace CalamityOverhaul.Content.TileProcessors
{
    internal class ShadowOrbTP : TileProcessor
    {
        public override int TargetTileID => TileID.ShadowOrbs;
        public override void OnKill() {
            if (!VaultUtils.isServer) {
                QuestNode.GetQuest<FindShadowOrb>().Objectives[0].CurrentProgress = 1;
            }
            if (!VaultUtils.isClient && CWRWorld.Death) {
                //根据世界类型生成对应的怪物，猩红生成猩红喀迈拉，腐化生成噬魂怪
                int npcID = WorldGen.crimson ? NPCID.Crimera : NPCID.EaterofSouls;
                NPC.NewNPC(new EntitySource_TileBreak(Position.X, Position.Y), Position.X * 16 + 16, Position.Y * 16 + 16, npcID);
            }
        }
    }
}
