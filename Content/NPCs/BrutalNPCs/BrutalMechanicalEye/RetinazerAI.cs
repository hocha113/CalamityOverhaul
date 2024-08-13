using CalamityOverhaul.Content.NPCs.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye
{
    internal class RetinazerAI : NPCCoverage
    {
        public override int TargetID => NPCID.Retinazer;
        public static bool Accompany;
        public static int[] ai = new int[SpazmatismAI.maxAINum];
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
            if (Accompany && SpazmatismAI.IsCCK(npc, ai)) {
                Main.instance.LoadNPC(npc.type);
                Texture2D mainValue = TextureAssets.Npc[npc.type].Value;
                Main.EntitySpriteDraw(mainValue, npc.Center - Main.screenPosition, CWRUtils.GetRec(mainValue, frameIndex, 6)
                , drawColor, npc.rotation, CWRUtils.GetOrig(mainValue, 6), npc.scale, SpriteEffects.None, 0);
                return false;
            }
            return true;
        }
    }
}
