using CalamityMod;
using CalamityMod.Events;
using CalamityMod.NPCs;
using CalamityMod.World;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime
{
    internal abstract class PrimeArm : CWRNPCOverride
    {
        internal bool bossRush;
        internal bool masterMode;
        internal bool death;
        internal bool viceAlive;
        internal bool cannonAlive;
        internal bool sawAlive;
        internal bool laserAlive;
        internal NPC head;
        internal Player player;
        internal int frame;
        internal bool dontAttack;
        internal CalamityGlobalNPC calNPC => npc.Calamity();
        public sealed override bool? CanCWROverride() {
            if (CWRWorld.MachineRebellion) {
                return true;
            }
            return null;
        }

        public sealed override void SetProperty() {
            if (CWRWorld.MachineRebellion) {
                npc.life = npc.lifeMax *= 20;
                npc.damage = npc.defDamage *= 2;
            }
        }

        public override bool AI() {
            if (CWRWorld.CanTimeFrozen()) {
                CWRNpc.DoTimeFrozen(npc);
                return false;
            }

            bossRush = BossRushEvent.BossRushActive;
            masterMode = Main.masterMode || bossRush;
            death = CalamityWorld.death || bossRush;
            head = Main.npc[(int)npc.ai[1]];
            player = Main.player[npc.target];
            npc.spriteDirection = -(int)npc.ai[0];
            npc.damage = 0;
            if (npc.type == NPCID.PrimeLaser) {
                CalamityGlobalNPC.primeLaser = npc.whoAmI;
            }
            else if (npc.type == NPCID.PrimeCannon) {
                CalamityGlobalNPC.primeCannon = npc.whoAmI;
            }
            else if (npc.type == NPCID.PrimeSaw) {
                CalamityGlobalNPC.primeSaw = npc.whoAmI;
            }
            else if (npc.type == NPCID.PrimeVice) {
                CalamityGlobalNPC.primeVice = npc.whoAmI;
            }
            HeadPrimeAI.FindPlayer(npc);
            HeadPrimeAI.CheakDead(npc, head);
            HeadPrimeAI.CheakRam(out cannonAlive, out viceAlive, out sawAlive, out laserAlive);
            if (!HeadPrimeAI.DontReform()) {
                npc.aiStyle = -1;
            }
            npc.dontTakeDamage = false;
            if (HeadPrimeAI.SetArmRot(npc, head, npc.type)) {
                return false;
            }

            if (head.ai[0] == 3 || head.ai[1] == 2f) {
                npc.life = 0;
                npc.HitEffect();
                npc.active = false;
                npc.netUpdate = true;
                return false;
            }

            return ArmBehavior();
        }

        public virtual bool ArmBehavior() {
            return true;
        }
    }
}
