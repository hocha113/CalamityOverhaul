using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer
{
    internal class ProbeAI : CWRNPCOverride
    {
        public override int TargetID => NPCID.Probe;
        [VaultLoaden(CWRConstant.NPC + "BTD/")]
        private static Asset<Texture2D> Probe { get; set; }
        [VaultLoaden(CWRConstant.NPC + "BTD/")]
        private static Asset<Texture2D> Probe_Glow { get; set; }
        public override bool? CanCWROverride() {
            if (CWRWorld.MachineRebellion) {
                return true;
            }
            return null;
        }
        public override void SetProperty() {
            if (CWRWorld.MachineRebellion) {
                npc.life = npc.lifeMax *= 32;
                npc.defDefense = npc.defense = 20;
                npc.defDamage = npc.damage *= 3;
                npc.knockBackResist = 0.1f;//在机械暴乱中拥有很强的抗击退能力
                npc.scale += 0.3f;
            }
        }
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

            Texture2D value = Probe.Value;
            spriteBatch.Draw(value, npc.Center - Main.screenPosition
                , null, drawColor, drawRot, value.Size() / 2, npc.scale, spriteEffects, 0);
            Texture2D value2 = Probe_Glow.Value;
            spriteBatch.Draw(value2, npc.Center - Main.screenPosition
                , null, Color.White, drawRot, value.Size() / 2, npc.scale, spriteEffects, 0);
            return false;
        }
        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            return !HeadPrimeAI.DontReform();
        }
    }
}
