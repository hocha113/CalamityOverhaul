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
        public override bool PreDraw(SpriteBatch spriteBatch, NPC npc, ref BossBarDrawParams drawParams) {
            if ((npc.type == NPCID.TheDestroyer || npc.type == NPCID.TheDestroyerBody
                || npc.type == NPCID.TheDestroyerTail) && !HeadPrimeAI.DontReform()) {
                drawParams.IconTexture = DestroyerHeadAI.HeadIcon.Value;
                drawParams.IconFrame = DestroyerHeadAI.HeadIcon.Value.GetRectangle();
                drawParams.LifeMax = npc.lifeMax;
                if (npc.type != NPCID.TheDestroyer) {
                    int headIndex = -1;//为了让这里的代码看起来是给人看的，我选择将条件判断分开写
                    if (npc.realLife >= 0 && npc.realLife < Main.maxNPCs) {
                        headIndex = npc.realLife;//如果是身体，根据realLife找到头部的索引
                    }
                    if (!Main.npc[headIndex].active || Main.npc[headIndex].type != NPCID.TheDestroyer) {
                        headIndex = -1;//检测头部是否正常，判断一下ID防止连到其他NPC去了
                    }
                    if (headIndex >= 0 && headIndex < Main.maxNPCs) {//如果找到正常的头部了就设置一下头部的血量
                        drawParams.Life = Main.npc[headIndex].life;
                    }
                }
            }
            return true;
        }
    }
}
