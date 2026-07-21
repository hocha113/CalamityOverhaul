using CalamityOverhaul.Content.NPCs.BrutalNPCs.Common;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer
{
    internal class DestroyerTailAI : DestroyerBodyAI, ICWRLoader
    {
        public override int TargetID => NPCID.TheDestroyerTail;
        private static int iconIndex;
        void ICWRLoader.LoadData() {
            CWRMod.Instance.AddBossHeadTexture(CWRConstant.NPC + "BTD/BTD_Tril", -1);
            iconIndex = ModContent.GetModBossHeadSlot(CWRConstant.NPC + "BTD/BTD_Tril");
        }
        public override void BossHeadSlot(ref int index) {
            index = iconIndex;
        }
        public override void BossHeadRotation(ref float rotation) => rotation = npc.rotation + MathHelper.Pi;

        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D value = Tail.Value;
            Rectangle rectangle = value.GetRectangle(frame, 4);
            Vector2 drawPos = npc.Center - Main.screenPosition;
            Vector2 origin = rectangle.Size() / 2;
            float seed = (npc.whoAmI % 64) / 64f;

            //头视觉+尾充能波
            int controllerId = (int)npc.realLife;
            var (visMode, visIntensity, visProgress) = ReadSegmentVisual(controllerId, out float wave);
            MechBossThermalRenderer.DrawOutlineHalo(spriteBatch, value, drawPos, rectangle,
                npc.rotation + MathHelper.Pi, origin, npc.scale, SpriteEffects.None,
                visMode, visIntensity, visProgress);

            bool shaderApplied = MechBossThermalRenderer.BeginThermalShader(spriteBatch, value, rectangle,
                visMode, visIntensity, visProgress, seed);
            spriteBatch.Draw(value, drawPos, rectangle, drawColor,
                npc.rotation + MathHelper.Pi, origin, npc.scale, SpriteEffects.None, 0);
            if (shaderApplied) {
                MechBossThermalRenderer.EndThermalShader(spriteBatch);
            }

            Texture2D value2 = Tail_Glow.Value;
            spriteBatch.Draw(value2, drawPos, rectangle, Color.White,
                npc.rotation + MathHelper.Pi, origin, npc.scale, SpriteEffects.None, 0);

            //充能波叠加
            if (wave > 0.05f) {
                Color hot = new Color(255, 165, 75, 0) * wave;
                spriteBatch.Draw(value2, drawPos, rectangle, hot,
                    npc.rotation + MathHelper.Pi, origin, npc.scale * 1.04f, SpriteEffects.None, 0);
            }
            return false;
        }

        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            return false;
        }
    }
}
