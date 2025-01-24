using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime;
using CalamityOverhaul.Content.NPCs.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer
{
    internal class ProbeAI : NPCOverride
    {
        public override int TargetID => NPCID.Probe;
        public override bool AI() => true;
        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (HeadPrimeAI.DontReform()) {
                return true;
            }

            SpriteEffects spriteEffects = SpriteEffects.None;
            float drawRot = npc.rotation;
            if (npc.spriteDirection > 0) {
                spriteEffects = SpriteEffects.FlipHorizontally;
            }

            Texture2D value = CWRUtils.GetT2DValue(CWRConstant.NPC + "BTD/Probe");
            spriteBatch.Draw(value, npc.Center - Main.screenPosition
                , null, drawColor, drawRot, value.Size() / 2, npc.scale, spriteEffects, 0);
            Texture2D value2 = CWRUtils.GetT2DValue(CWRConstant.NPC + "BTD/Probe_Glow");
            spriteBatch.Draw(value2, npc.Center - Main.screenPosition
                , null, Color.White, drawRot, value.Size() / 2, npc.scale, spriteEffects, 0);
            return false;
        }
        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            return !HeadPrimeAI.DontReform();
        }
    }
}
