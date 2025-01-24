using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime;
using CalamityOverhaul.Content.NPCs.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer
{
    internal class DestroyerTailAI : NPCOverride
    {
        public override int TargetID => NPCID.TheDestroyerTail;

        public override bool AI() => true;

        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (HeadPrimeAI.DontReform()) {
                return true;
            }

            Texture2D value = CWRUtils.GetT2DValue(CWRConstant.NPC + "BTD/Tail");
            spriteBatch.Draw(value, npc.Center - Main.screenPosition
                , null, drawColor, npc.rotation + MathHelper.Pi, value.Size() / 2, npc.scale, SpriteEffects.None, 0);
            Texture2D value2 = CWRUtils.GetT2DValue(CWRConstant.NPC + "BTD/Tail_Glow");
            spriteBatch.Draw(value2, npc.Center - Main.screenPosition
                , null, Color.White, npc.rotation + MathHelper.Pi, value.Size() / 2, npc.scale, SpriteEffects.None, 0);
            return false;
        }

        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            return HeadPrimeAI.DontReform();
        }
    }
}
