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
        public sealed override bool? CanCWROverride() {
            return null;
        }

        public sealed override void SetProperty() {
        }

        public override bool AI() {
            if (CWRWorld.CanTimeFrozen()) {
                CWRNpc.DoTimeFrozen(npc);
                return false;
            }

            bossRush = CWRRef.GetBossRushActive();
            masterMode = Main.masterMode || bossRush;
            death = CWRRef.GetDeathMode() || bossRush;
            head = Main.npc[(int)npc.ai[1]];
            player = Main.player[npc.target];
            npc.spriteDirection = -(int)npc.ai[0];
            npc.damage = 0;
            if (npc.type == NPCID.PrimeLaser) {
                CWRWorld.primeLaser = npc.whoAmI;
            }
            else if (npc.type == NPCID.PrimeCannon) {
                CWRWorld.primeCannon = npc.whoAmI;
            }
            else if (npc.type == NPCID.PrimeSaw) {
                CWRWorld.primeSaw = npc.whoAmI;
            }
            else if (npc.type == NPCID.PrimeVice) {
                CWRWorld.primeVice = npc.whoAmI;
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
                //手臂的"被头部消灭"必须服务端单点决策，否则客户端单方面 active=false
                //会让该手臂在客户端凭空消失，但服务端继续保留并不停同步回来，造成抖动
                if (!VaultUtils.isClient) {
                    npc.life = 0;
                    npc.HitEffect();
                    npc.active = false;
                    npc.netUpdate = true;
                }
                return false;
            }

            return ArmBehavior();
        }

        public virtual bool ArmBehavior() {
            return true;
        }
    }
}
