using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.States;
using InnoVault.StateMachines;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime
{
    /// <summary>臂 NPCOverride 基类，行为见 States.Arms</summary>
    internal abstract class PrimeArm : CWRNPCOverride
    {
        /// <summary>十字封位锚点，绞杀瞬间冻结</summary>
        private Vector2 crossAnchor;
        private bool crossAnchorLatched;
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
        internal PrimeArmStateContext armContext;
        internal VaultStateMachine<PrimeArmStateContext> armStateMachine;

        /// <summary>初始状态工厂</summary>
        protected abstract PrimeArmStateBase CreateInitialState();
        /// <summary>转阶段殉爆延迟，帧</summary>
        protected abstract int DetonationDelay { get; }
        /// <summary>环绕编队的角位索引（0~3）</summary>
        protected abstract int FormationIndex { get; }

        public sealed override bool? CanCWROverride() {
            return null;
        }

        public sealed override void SetProperty() {
            armContext = null;
            armStateMachine = null;
        }

        public override bool AI() {
            //Mechdusa交还原版AI
            if (NPC.IsMechQueenUp) {
                return true;
            }

            bossRush = CWRRef.GetBossRushActive();
            masterMode = Main.masterMode || bossRush;
            death = CWRRef.GetDeathMode() || bossRush;
            head = Main.npc[(int)npc.ai[PrimeAiSlots.ArmHeadIndex]];
            player = Main.player[npc.target];
            npc.spriteDirection = -(int)npc.ai[PrimeAiSlots.ArmSide];
            npc.damage = 0;
            npc.dontTakeDamage = false;

            RegisterWorldIndex();
            HeadPrimeAI.FindPlayer(npc);
            HeadPrimeAI.CheakRam(out cannonAlive, out viceAlive, out sawAlive, out laserAlive);

            npc.aiStyle = -1;

            //头没了则臂坠毁，服务端决策
            if (!head.active || head.type != NPCID.SkeletronPrime) {
                KillSelfOnServer();
                return false;
            }

            //免疫debuff
            for (int i = 0; i < npc.buffImmune.Length; i++) {
                npc.buffImmune[i] = true;
            }

            EnsureStateMachine();
            UpdateContext();

            //服务端广播位置，客户端傀儡
            if (!VaultUtils.isClient) {
                npc.netUpdate = true;
            }

            PrimeStateIndex headState = HeadPrimeAI.GetStateIndex(head);
            int headPhase = (int)head.ai[PrimeAiSlots.HeadPhase];

            //转阶段殉爆
            //先于狂暴兜底挂Rage
            if (headState == PrimeStateIndex.PhaseTransition) {
                RunDetonationSequence();
                return false;
            }

            //狂暴/死亡时四肢兜底清除
            if (headPhase >= PrimePhase.Rage) {
                KillSelfOnServer();
                return false;
            }

            //脱战跟随退场
            if (headState == PrimeStateIndex.Despawn && npc.timeLeft > 10) {
                npc.timeLeft = 10;
            }

            //冲撞/白昼编队环绕
            if (HandleFormationOverride(headState)) {
                return false;
            }

            //生成宽限计时
            ai[0]++;
            armContext.DontAttack = ai[0] < PrimeAiSlots.ArmSpawnGraceFrames;

            UpdateVisualDecay();
            ArmPreUpdate();

            //客户端只呈现同步位置
            bool clientShadow = VaultUtils.isClient;
            Vector2 savedPos = npc.position;

            armStateMachine.Update();

            if (clientShadow) {
                npc.position = savedPos;
                npc.velocity = Vector2.Zero;
            }

            ArmPostUpdate();
            return false;
        }

        /// <summary>状态机更新前</summary>
        protected virtual void ArmPreUpdate() { }

        /// <summary>状态机更新后</summary>
        protected virtual void ArmPostUpdate() { }

        #region 状态机维护

        private void EnsureStateMachine() {
            armContext ??= new PrimeArmStateContext {
                Npc = npc,
                Owner = this
            };

            if (armStateMachine != null) {
                return;
            }

            armStateMachine = new NpcStateMachine<PrimeArmStateContext>(armContext, aiSlot: PrimeAiSlots.ArmStateSlot);

            //中途加入从同步槽恢复
            IVaultState<PrimeArmStateContext> syncedState = null;
            int syncedStateId = (int)npc.ai[PrimeAiSlots.ArmStateSlot];
            if (VaultUtils.isClient && syncedStateId > 0) {
                syncedState = VaultStateRegistry<PrimeArmStateContext>.Create(syncedStateId);
            }
            armStateMachine.SetInitialState(syncedState ?? CreateInitialState());
        }

        private void UpdateContext() {
            armContext.Npc = npc;
            armContext.Head = head;
            armContext.Target = player;
            armContext.Owner = this;
            armContext.BossRush = bossRush;
            armContext.MasterMode = masterMode;
            armContext.Death = death;
            armContext.ViceAlive = viceAlive;
            armContext.CannonAlive = cannonAlive;
            armContext.SawAlive = sawAlive;
            armContext.LaserAlive = laserAlive;
        }

        #endregion

        #region 头部联动

        private void RegisterWorldIndex() {
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
        }

        /// <summary>编队环绕，收尾蓄力见 IsCommittedArmState</summary>
        private bool HandleFormationOverride(PrimeStateIndex headState) {
            PrimeCommandKind command = HeadPrimeAI.GetActiveCommand(head);

            bool crossActive = headState == PrimeStateIndex.CrossExecute || command == PrimeCommandKind.CrossExecute;
            if (!crossActive) {
                crossAnchorLatched = false;
            }

            bool committed = PrimeFacts.IsCommittedArmState((int)npc.ai[PrimeAiSlots.ArmStateSlot]);

            if (headState == PrimeStateIndex.TetherSpin) {
                //TetherSpin硬取消蓄力
                if (committed && !VaultUtils.isClient) {
                    armStateMachine.ChangeState(CreateInitialState());
                    npc.netUpdate = true;
                }
                return ApplyTetherFormation();
            }

            if (committed) {
                return false;
            }

            if (headState == PrimeStateIndex.BarrageCommand) {
                return ApplyBarrageFormation();
            }
            if (crossActive) {
                return ApplyCrossFormation(headState);
            }

            bool spin = headState is PrimeStateIndex.SpinDash or PrimeStateIndex.RageDash or PrimeStateIndex.DayEnrage;
            if (!spin) {
                return false;
            }

            return ApplyOrbitFormation(head.width * 2, 0.5f);
        }

        private bool ApplyOrbitFormation(float radius, float lerp) {
            npc.dontTakeDamage = true;
            npc.damage = 0;
            npc.velocity = Vector2.Zero;
            HeadPrimeAI headOverride = head.GetOverride<HeadPrimeAI>();
            float rot = headOverride.ai[PrimeAiSlots.OverrideOrbitClock] * 0.2f + MathHelper.TwoPi / 4 * FormationIndex;
            Vector2 toPoint = head.Center + rot.ToRotationVector2() * radius;
            float origRot = head.Center.To(npc.Center).ToRotation();
            npc.Center = Vector2.Lerp(npc.Center, toPoint, lerp);
            npc.rotation = origRot - MathHelper.PiOver2;
            return true;
        }

        private bool ApplyTetherFormation() {
            npc.dontTakeDamage = true;
            npc.damage = 0;
            npc.velocity = Vector2.Zero;
            //TetherSpin收紧半径在指令槽
            float radius = head.ai[PrimeAiSlots.HeadCommandSlot];
            if (radius < 100f || radius > 900f) {
                radius = 280f;
            }
            float rot = MathHelper.TwoPi / 4 * FormationIndex + head.rotation;
            Vector2 corner = head.Center + rot.ToRotationVector2() * radius;
            npc.Center = Vector2.Lerp(npc.Center, corner, 0.18f);
            npc.rotation = head.Center.To(npc.Center).ToRotation() - MathHelper.PiOver2;
            return true;
        }

        private bool ApplyBarrageFormation() {
            npc.dontTakeDamage = true;
            npc.damage = 0;
            float offset = (FormationIndex - 1.5f) * 70f;
            Vector2 toPoint = head.Center + new Vector2(offset, 90f);
            npc.Center = Vector2.Lerp(npc.Center, toPoint, 0.22f);
            npc.rotation = (player.Center - npc.Center).ToRotation() - MathHelper.PiOver2;
            return true;
        }

        /// <summary>十字封位，绞杀瞬间冻锚点</summary>
        private bool ApplyCrossFormation(PrimeStateIndex headState) {
            npc.dontTakeDamage = true;
            npc.damage = 0;

            if (headState == PrimeStateIndex.CrossExecute) {
                if (!crossAnchorLatched) {
                    crossAnchor = player.Center;
                    crossAnchorLatched = true;
                }
            }
            else {
                crossAnchorLatched = false;
            }

            Vector2 center = crossAnchorLatched ? crossAnchor : player.Center;
            Vector2 toPoint = center + PrimeCrossExecuteState.ArmSlots[FormationIndex];
            npc.Center = Vector2.Lerp(npc.Center, toPoint, crossAnchorLatched ? 0.2f : 0.14f);
            npc.rotation = (center - npc.Center).ToRotation() - MathHelper.PiOver2;
            return true;
        }

        /// <summary>转阶段殉爆</summary>
        private void RunDetonationSequence() {
            npc.dontTakeDamage = true;
            npc.damage = 0;
            npc.velocity = Vector2.Zero;

            float rot = MathHelper.TwoPi / 4 * FormationIndex + head.rotation;
            Vector2 toPoint = head.Center + rot.ToRotationVector2() * head.width;
            npc.Center = Vector2.Lerp(npc.Center, toPoint, 0.25f);
            npc.rotation = head.Center.To(npc.Center).ToRotation() - MathHelper.PiOver2;

            //殉爆倒计时
            npc.localAI[2]++;

            //临爆窜火
            if (!VaultUtils.isServer && npc.localAI[2] > DetonationDelay - 14 && Main.GameUpdateCount % 2 == 0) {
                Dust dust = Dust.NewDustDirect(npc.position, npc.width, npc.height,
                    DustID.FireworkFountain_Red, 0, 0, 100, Color.OrangeRed, Main.rand.NextFloat(1.2f, 2f));
                dust.noGravity = true;
            }

            if (npc.localAI[2] >= DetonationDelay && !VaultUtils.isClient) {
                npc.life = 0;
                npc.HitEffect();
                npc.active = false;
                npc.netUpdate = true;
            }
        }

        private void KillSelfOnServer() {
            if (VaultUtils.isClient) {
                return;
            }
            npc.life = 0;
            npc.HitEffect();
            npc.active = false;
            npc.netUpdate = true;
        }

        #endregion

        /// <summary>后坐/硝烟衰减</summary>
        private void UpdateVisualDecay() {
            armContext.RecoilIntensity *= 0.88f;
            if (armContext.RecoilIntensity < 0.1f) {
                armContext.RecoilIntensity = 0f;
            }

            if (!VaultUtils.isServer && armContext.RecoilIntensity > 2f && Main.rand.NextBool(2)) {
                Vector2 smokePos = npc.Center + armContext.AimDirection * 45f;
                Vector2 smokeVel = armContext.AimDirection * Main.rand.NextFloat(1f, 3f) + Main.rand.NextVector2Circular(1, 1);
                Dust dust = Dust.NewDustDirect(smokePos, 1, 1, DustID.Smoke, smokeVel.X, smokeVel.Y,
                    100, default, Main.rand.NextFloat(1.2f, 2.0f));
                dust.noGravity = false;
                dust.velocity *= 0.8f;
            }
        }
    }
}
