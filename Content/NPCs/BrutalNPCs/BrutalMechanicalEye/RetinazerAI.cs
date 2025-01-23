using CalamityOverhaul.Content.NPCs.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye
{
    internal class RetinazerAI : NPCOverride
    {
        public override int TargetID => NPCID.Retinazer;
        public static bool Accompany;
        private static int frameIndex;
        private static int frameCount;
        public override void SetProperty() => SpazmatismAI.SetAccompany(npc, ref ai, out Accompany);
        public override bool AI() {
            if (++frameCount > 5) {
                if (++frameIndex > 5) {
                    frameIndex = 3;
                }
                frameCount = 0;
            }

            npc.dontTakeDamage = false;

            if (SpazmatismAI.AccompanyAI(npc, ref ai, Accompany)) {
                return false;
            }
            if (SpazmatismAI.ProtogenesisAI(npc, ref ai)) {
                return false;
            }

            return true;
        }

        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D mainValue = CWRUtils.GetT2DValue(CWRConstant.NPC + "BEYE/Retinazer");
            Rectangle rectangle = CWRUtils.GetRec(mainValue, frameIndex, 4);
            SpriteEffects spriteEffects = npc.spriteDirection > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically;
            float drawRot = npc.rotation + MathHelper.PiOver2;
            if ((Accompany && SpazmatismAI.IsCCK(npc, ai))) {
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
