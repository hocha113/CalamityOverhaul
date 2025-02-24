using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer
{
    //制造出这个该死的东西是出于无奈的
    internal class DestroyerDrawHeadIconNPC : ModNPC
    {
        public override string Texture => CWRConstant.Placeholder;
        public override void SetDefaults() {
            NPC.life = NPC.lifeMax = 10000;
            NPC.width = NPC.height = 32;
            NPC.dontTakeDamage = true;
            NPC.damage = 0;
        }

        public override void BossHeadSlot(ref int index) {
            if (!HeadPrimeAI.DontReform()) {
                index = DestroyerHeadAI.iconIndex;
            }
        }

        public override void BossHeadRotation(ref float rotation) => rotation = NPC.rotation + MathHelper.Pi;

        public override bool CheckActive() => false;

        public override void AI() {
            NPC.dontTakeDamage = true;
            NPC head = CWRUtils.GetNPCInstance((int)NPC.ai[0]);
            if (head.Alives()) {
                NPC.Center = head.Center;
                NPC.rotation = head.rotation;
            }
            else {
                NPC.active = false;
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            return false;
        }
    }
}
