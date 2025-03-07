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
            if (npc.type == NPCID.TheDestroyer && !HeadPrimeAI.DontReform()) {
                drawParams.IconTexture = DestroyerHeadAI.HeadIcon.Value;
                drawParams.IconFrame = CWRUtils.GetRec(DestroyerHeadAI.HeadIcon.Value);
            }
            return true;
        }
    }
}
