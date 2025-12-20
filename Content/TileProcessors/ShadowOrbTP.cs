using CalamityOverhaul.Common;
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
        public override bool IsDaed() {
            //专门判断一下帧，防止堆叠放置，因为暗影珠的原点判定有问题
            if (Tile.TileFrameX > 0 || Tile.TileFrameY > 0) {
                return true;//牛魔的为什么珠子的四个角都会被算作是左上角？官方自己写的错误？
            }
            return base.IsDaed();
        }
        public override void OnKill() {
            if (!VaultUtils.isServer && CWRServerConfig.Instance.QuestLog && Tile.TileFrameX <= 0 && Tile.TileFrameY <= 0) {
                var node = QuestNode.GetQuest<FindShadowOrb>();
                if (node is not null && node.IsUnlocked && node.Objectives?.Count > 0) {
                    node.Objectives[0].CurrentProgress = 1;
                }
            }
            if (!VaultUtils.isClient && CWRWorld.Death) {
                //根据世界类型生成对应的怪物，猩红生成猩红喀迈拉，腐化生成噬魂怪
                int npcID = WorldGen.crimson ? NPCID.Crimera : NPCID.EaterofSouls;
                for (int i = 0; i < 4; i++) {
                    NPC.NewNPC(new EntitySource_TileBreak(Position.X, Position.Y)
                        , Position.X * 16 + Width / 2, Position.Y * 16 + Height / 2, npcID);
                }
            }
        }
    }
}
