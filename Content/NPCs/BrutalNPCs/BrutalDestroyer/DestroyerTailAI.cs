using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer
{
    internal class DestroyerTailAI : DestroyerBodyAI
    {
        public override int TargetID => NPCID.TheDestroyerTail;

        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (HeadPrimeAI.DontReform()) {
                return true;
            }

            Texture2D value = Tail.Value;
            spriteBatch.Draw(value, npc.Center - Main.screenPosition
                , null, drawColor, npc.rotation + MathHelper.Pi, value.Size() / 2, npc.scale, SpriteEffects.None, 0);
            Texture2D value2 = Tail_Glow.Value;
            spriteBatch.Draw(value2, npc.Center - Main.screenPosition
                , null, Color.White, npc.rotation + MathHelper.Pi, value.Size() / 2, npc.scale, SpriteEffects.None, 0);
            return false;
        }

        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            return HeadPrimeAI.DontReform();
        }
    }
}
