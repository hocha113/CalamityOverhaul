using CalamityOverhaul.Content.NPCs.Core;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye
{
    internal class RetinazerAI : NPCCoverage
    {
        public override int TargetID => NPCID.Retinazer;
        public static bool Accompany;
        public static int[] ai = new int[8];
        public override void SetProperty() {
            SpazmatismAI.SetAccompany(npc, out Accompany);
            ai = new int[8];
        }

        public override bool AI() {
            npc.dontTakeDamage = false;
            if (SpazmatismAI.AccompanyAI(npc, ref ai, Accompany)) {
                return false;
            }
            ai[0]++;
            return true;
        }
    }
}
