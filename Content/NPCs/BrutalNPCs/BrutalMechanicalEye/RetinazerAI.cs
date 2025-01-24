using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye
{
    internal class RetinazerAI : SpazmatismAI
    {
        public override int TargetID => NPCID.Retinazer;
        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (HeadPrimeAI.DontReform()) {
                return true;
            }

            Texture2D mainValue = CWRUtils.GetT2DValue(CWRConstant.NPC + "BEYE/Retinazer");
            Rectangle rectangle = CWRUtils.GetRec(mainValue, frameIndex, 4);
            SpriteEffects spriteEffects = npc.spriteDirection > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically;
            float drawRot = npc.rotation + MathHelper.PiOver2;
            if (IsSecondPhase()) {
                mainValue = CWRUtils.GetT2DValue(CWRConstant.NPC + "BEYE/RetinazerAlt");
                rectangle = CWRUtils.GetRec(mainValue, frameIndex, 4);
                Main.EntitySpriteDraw(mainValue, npc.Center - Main.screenPosition, rectangle
                , Color.White, drawRot, rectangle.Size() / 2, npc.scale, spriteEffects, 0);
                return false;
            }
            Main.EntitySpriteDraw(mainValue, npc.Center - Main.screenPosition, rectangle
            , Color.White, drawRot, rectangle.Size() / 2, npc.scale, spriteEffects, 0);
            return false;
        }
    }
}
