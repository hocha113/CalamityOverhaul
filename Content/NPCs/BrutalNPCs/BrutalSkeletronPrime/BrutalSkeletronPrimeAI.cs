using CalamityOverhaul.Content.NPCs.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime
{
    internal class BrutalSkeletronPrimeAI : NPCSet
    {
        public override int targetID => NPCID.SkeletronPrime;
        int frame = 0;
        bool spwanArm;
        int primeCannon;
        int primeSaw;
        int primeVice;
        int primeLaser;

        public override bool? AI(NPC npc, Mod mod) {
            npc.TargetClosest();
            Player player = Main.player[npc.target];
            if (!player.Alives()) {
                npc.ai[0] = 1;
            }
            switch (npc.ai[0]) {
                case 0:
                    leisureAI(npc, player);
                    break;
            }
            
            return false;
        }

        private void leisureAI(NPC npc, Player player) {
            npc.Move(player.Center + new Vector2(0, -450), 12, 0);
            if (!spwanArm) {
                SoundEngine.PlaySound(SoundID.Roar);
                spanArm(npc);
                spwanArm = true;
            }
        }

        private void killArm() {
            Main.npc[primeCannon].active = false;
            Main.npc[primeSaw].active = false;
            Main.npc[primeVice].active = false;
            Main.npc[primeLaser].active = false;
        }

        private void spanArm(NPC npc, int limit = 0) {
            if (limit == 1 || limit == 0) {
                primeCannon = NPC.NewNPC(npc.GetSource_FromAI(), (int)npc.Center.X, (int)npc.Center.Y, NPCID.PrimeCannon, npc.whoAmI);
                Main.npc[primeCannon].ai[0] = -1f;
                Main.npc[primeCannon].ai[1] = npc.whoAmI;
                Main.npc[primeCannon].target = npc.target;
                Main.npc[primeCannon].netUpdate = true;
            }
            if (limit == 2 || limit == 0) {
                primeSaw = NPC.NewNPC(npc.GetSource_FromAI(), (int)npc.Center.X, (int)npc.Center.Y, NPCID.PrimeSaw, npc.whoAmI);
                Main.npc[primeSaw].ai[0] = 1f;
                Main.npc[primeSaw].ai[1] = npc.whoAmI;
                Main.npc[primeSaw].target = npc.target;
                Main.npc[primeSaw].netUpdate = true;
            }
            if (limit == 3 || limit == 0) {
                primeVice = NPC.NewNPC(npc.GetSource_FromAI(), (int)npc.Center.X, (int)npc.Center.Y, NPCID.PrimeVice, npc.whoAmI);
                Main.npc[primeVice].ai[0] = -1f;
                Main.npc[primeVice].ai[1] = npc.whoAmI;
                Main.npc[primeVice].target = npc.target;
                Main.npc[primeVice].ai[3] = 150f;
                Main.npc[primeVice].netUpdate = true;
            }
            if (limit == 4 || limit == 0) {
                primeLaser = NPC.NewNPC(npc.GetSource_FromAI(), (int)npc.Center.X, (int)npc.Center.Y, NPCID.PrimeLaser, npc.whoAmI);
                Main.npc[primeLaser].ai[0] = 1f;
                Main.npc[primeLaser].ai[1] = npc.whoAmI;
                Main.npc[primeLaser].target = npc.target;
                Main.npc[primeLaser].ai[3] = 150f;
                Main.npc[primeLaser].netUpdate = true;
            }
        }

        public override bool? Draw(Mod mod, NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) => false;

        public override bool PostDraw(Mod mod, NPC NPC, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D mainValue = CalamityMod.CalamityMod.ChadPrime.Value;
            Main.EntitySpriteDraw(mainValue, NPC.Center - Main.screenPosition, CWRUtils.GetRec(mainValue, frame, 6)
                , drawColor, NPC.rotation, CWRUtils.GetOrig(mainValue, 6), NPC.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
