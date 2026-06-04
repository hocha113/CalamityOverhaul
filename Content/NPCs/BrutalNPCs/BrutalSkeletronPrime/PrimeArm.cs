using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core;
using InnoVault.StateMachines;
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
        internal PrimeArmStateContext armStateContext;
        internal VaultStateMachine<PrimeArmStateContext> armStateMachine;
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
            head = Main.npc[(int)npc.ai[PrimeAiSlots.ArmHeadIndex]];
            player = Main.player[npc.target];
            npc.spriteDirection = -(int)npc.ai[PrimeAiSlots.ArmSide];
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

            if (PrimeFacts.IsDeathPerformance(head) || head.ai[PrimeAiSlots.HeadMainState] == 3 || head.ai[PrimeAiSlots.HeadAttackState] == 2f) {
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

        internal void EnsureArmStateMachine(IVaultState<PrimeArmStateContext> initialState) {
            armStateContext ??= new PrimeArmStateContext {
                Npc = npc,
                Owner = this
            };

            UpdateArmStateContext();

            if (armStateMachine != null) {
                return;
            }

            armStateMachine = new NpcStateMachine<PrimeArmStateContext>(armStateContext, aiSlot: PrimeAiSlots.ArmState);
            IVaultState<PrimeArmStateContext> syncedState = null;
            int syncedStateId = (int)npc.ai[PrimeAiSlots.ArmState];
            if (VaultUtils.isClient && syncedStateId > 0) {
                syncedState = VaultStateRegistry<PrimeArmStateContext>.Create(syncedStateId);
            }
            armStateMachine.SetInitialState(syncedState ?? initialState);
        }

        internal void UpdateArmStateContext() {
            if (armStateContext == null) {
                return;
            }
            armStateContext.Npc = npc;
            armStateContext.Head = head;
            armStateContext.Target = player;
            armStateContext.Owner = this;
            armStateContext.BossRush = bossRush;
            armStateContext.MasterMode = masterMode;
            armStateContext.Death = death;
            armStateContext.ViceAlive = viceAlive;
            armStateContext.CannonAlive = cannonAlive;
            armStateContext.SawAlive = sawAlive;
            armStateContext.LaserAlive = laserAlive;
            armStateContext.DontAttack = dontAttack;
        }

        public virtual bool ArmBehavior() {
            return true;
        }
    }
}
