using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime;
using CalamityOverhaul.Content.NPCs.Core;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer
{
    internal class DestroyerHeadAI : NPCOverride, ICWRLoader
    {
        public override int TargetID => NPCID.TheDestroyer;
        internal static Asset<Texture2D> Head;
        internal static Asset<Texture2D> Head_Glow;
        void ICWRLoader.LoadAsset() {
            Head = CWRUtils.GetT2DAsset(CWRConstant.NPC + "BTD/Head");
            Head_Glow = CWRUtils.GetT2DAsset(CWRConstant.NPC + "BTD/Head_Glow");
        }
        void ICWRLoader.UnLoadData() {
            Head = null;
            Head_Glow = null;
        }

        public override bool AI() => true;

        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (HeadPrimeAI.DontReform()) {
                return true;
            }

            Texture2D value = Head.Value;
            spriteBatch.Draw(value, npc.Center - Main.screenPosition
                , null, drawColor, npc.rotation + MathHelper.Pi, value.Size() / 2, npc.scale, SpriteEffects.None, 0);
            Texture2D value2 = Head_Glow.Value;
            spriteBatch.Draw(value2, npc.Center - Main.screenPosition
                , null, Color.White, npc.rotation + MathHelper.Pi, value.Size() / 2, npc.scale, SpriteEffects.None, 0);
            return false;
        }

        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            return HeadPrimeAI.DontReform();
        }
    }
}
