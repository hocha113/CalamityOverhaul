using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer
{
    internal class DestroyerBossBar : GlobalBossBar
    {
        private static bool IsDestroyerSegment(int type) =>
            type == NPCID.TheDestroyer || type == NPCID.TheDestroyerBody || type == NPCID.TheDestroyerTail;

        public override bool PreDraw(SpriteBatch spriteBatch, NPC npc, ref BossBarDrawParams drawParams) {
            //非目标直接返回，不改任何东西，确保不会影响其它 NPC
            if (!IsDestroyerSegment(npc.type) || HeadPrimeAI.DontReform()) {
                return true;
            }

            //安全获取图标
            if (DestroyerHeadAI.HeadIcon?.Value != null) {
                drawParams.IconTexture = DestroyerHeadAI.HeadIcon.Value;
                drawParams.IconFrame = DestroyerHeadAI.HeadIcon.Value.GetRectangle();
            }

            //头部直接使用自身血量
            if (npc.type == NPCID.TheDestroyer) {
                drawParams.Life = npc.life;
                drawParams.LifeMax = npc.lifeMax;
                return true;
            }

            //身体或尾部尝试同步头部
            int headIndex = npc.realLife;
            if (headIndex >= 0 && headIndex < Main.maxNPCs) {
                NPC head = Main.npc[headIndex];
                if (head.active && head.type == NPCID.TheDestroyer) {
                    drawParams.Life = head.life;
                    drawParams.LifeMax = head.lifeMax;
                    return true;
                }
            }

            //回退，没找到正常头部，就用本段自身数据，避免异常
            drawParams.Life = npc.life;
            drawParams.LifeMax = npc.lifeMax;
            return true;
        }
    }
}