using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime;
using CalamityOverhaul.Content.NPCs.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer
{
    internal class DestroyerBodyAI : NPCOverride
    {
        public override int TargetID => NPCID.TheDestroyerBody;

        public override bool AI() => true;

        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (HeadPrimeAI.DontReform()) {
                return true;
            }

            Texture2D value = CWRUtils.GetT2DValue(CWRConstant.NPC + "BTD/Body");
            Texture2D value2 = CWRUtils.GetT2DValue(CWRConstant.NPC + "BTD/Body_Glow");

            if (npc.whoAmI % 2 == 0) {
                value = CWRUtils.GetT2DValue(CWRConstant.NPC + "BTD/BodyAlt");
                value2 = CWRUtils.GetT2DValue(CWRConstant.NPC + "BTD/BodyAlt_Glow");
            }

            spriteBatch.Draw(value, npc.Center - Main.screenPosition
                , null, drawColor, npc.rotation + MathHelper.Pi, value.Size() / 2, npc.scale, SpriteEffects.None, 0);

            spriteBatch.Draw(value2, npc.Center - Main.screenPosition
                , null, Color.White, npc.rotation + MathHelper.Pi, value.Size() / 2, npc.scale, SpriteEffects.None, 0);
            return false;
        }

        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            return HeadPrimeAI.DontReform();
        }
    }
}
