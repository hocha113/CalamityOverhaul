using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye
{
    internal class RetinazerAI : SpazmatismAI, ICWRLoader
    {
        public override int TargetID => NPCID.Retinazer;
        internal static Asset<Texture2D> RetinazerAsset;
        internal static Asset<Texture2D> RetinazerAltAsset;
        void ICWRLoader.LoadAsset() {
            RetinazerAsset = CWRUtils.GetT2DAsset(CWRConstant.NPC + "BEYE/Retinazer");
            RetinazerAltAsset = CWRUtils.GetT2DAsset(CWRConstant.NPC + "BEYE/RetinazerAlt");
        }
        void ICWRLoader.UnLoadData() {
            RetinazerAsset = null;
            RetinazerAltAsset = null;
        }
        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (HeadPrimeAI.DontReform()) {
                return true;
            }

            Texture2D mainValue = RetinazerAsset.Value;
            Rectangle rectangle = CWRUtils.GetRec(mainValue, frameIndex, 4);
            SpriteEffects spriteEffects = npc.spriteDirection > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically;
            float drawRot = npc.rotation + MathHelper.PiOver2;
            if (IsSecondPhase()) {
                mainValue = RetinazerAltAsset.Value;
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
