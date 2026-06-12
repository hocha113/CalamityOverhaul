using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.States;
using InnoVault.StateMachines;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime
{
    /// <summary>
    /// 机械臂控制器基类：维护与头部的从属关系、驱动各自的状态机、
    /// 响应头部状态（编队收拢/环绕、转阶段殉爆、狂暴期退场）。
    /// 具体攻击行为全部由 <see cref="States.Arms"/> 下的状态类实现
    /// </summary>
    internal abstract class PrimeArm : CWRNPCOverride
    {
        /// <summary>十字绞杀封位锚点（头部正式进入绞杀状态的瞬间冻结，与预警线对齐）</summary>
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
        /// <summary>转阶段殉爆延迟（帧）——四肢按各自延迟依次爆裂，形成演出节拍</summary>
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
            if (CWRWorld.CanTimeFrozen()) {
                CWRNpc.DoTimeFrozen(npc);
                return false;
            }

            //Mechdusa（机械混合体）形态交还原版AI
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

            if (!HeadPrimeAI.DontReform()) {
                npc.aiStyle = -1;
            }

            //头部已不在：机械臂立即失能坠毁（服务端单点决策，避免客户端凭空消失后被同步回来）
            if (!head.active || head.type != NPCID.SkeletronPrime) {
                KillSelfOnServer();
                return false;
            }

            //机械臂全程免疫所有 debuff
            for (int i = 0; i < npc.buffImmune.Length; i++) {
                npc.buffImmune[i] = true;
            }

            EnsureStateMachine();
            UpdateContext();

            PrimeStateIndex headState = HeadPrimeAI.GetStateIndex(head);
            int headPhase = (int)head.ai[PrimeAiSlots.HeadPhase];

            //转阶段：收拢编队，按各自延迟依次殉爆。
            //必须先于狂暴击杀兜底判定——转阶段一进入就会挂出 Rage 标记
            if (headState == PrimeStateIndex.PhaseTransition) {
                RunDetonationSequence();
                return false;
            }

            //头部进入狂暴/死亡演出：四肢不应再存在（转阶段演出漏网的兜底）
            if (headPhase >= PrimePhase.Rage) {
                KillSelfOnServer();
                return false;
            }

            //头部脱战离场：跟随退场
            if (headState == PrimeStateIndex.Despawn && npc.timeLeft > 10) {
                npc.timeLeft = 10;
            }

            //编队接管：冲撞与白昼狂暴期环绕成旋转护盾
            if (HandleFormationOverride(headState)) {
                return false;
            }

            //生成宽限计时
            ai[0]++;
            armContext.DontAttack = ai[0] < PrimeAiSlots.ArmSpawnGraceFrames;

            UpdateVisualDecay();
            ArmPreUpdate();
            armStateMachine.Update();
            ArmPostUpdate();
            return false;
        }

        /// <summary>状态机更新前的控制器逻辑（距离安全网、专属视觉驱动等）</summary>
        protected virtual void ArmPreUpdate() { }

        /// <summary>状态机更新后的控制器逻辑（帧动画等）</summary>
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

            //客户端中途加入时从同步槽恢复服务端当前状态
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

        /// <summary>
        /// 编队接管：头部冲撞或白昼狂暴 → 拉开为高速旋转护盾编队。
        /// <para>"蓄力已可见"的收尾攻击（<see cref="PrimeFacts.IsCommittedArmState"/>）
        /// 不被硬切：该臂先兑现这一手再延后入列（头部调度侧也会等待，见指挥状态），
        /// 消解头部冲刺与机械臂蓄力的时序冲突。
        /// 唯一例外是电弧风车——链锁必须立即拉起，改为把蓄力干净取消回基态。</para>
        /// </summary>
        private bool HandleFormationOverride(PrimeStateIndex headState) {
            PrimeCommandKind command = HeadPrimeAI.GetActiveCommand(head);

            bool crossActive = headState == PrimeStateIndex.CrossExecute || command == PrimeCommandKind.CrossExecute;
            if (!crossActive) {
                crossAnchorLatched = false;
            }

            bool committed = PrimeFacts.IsCommittedArmState((int)npc.ai[PrimeAiSlots.ArmStateSlot]);

            if (headState == PrimeStateIndex.TetherSpin) {
                //电弧风车 40 帧内就要拉链就位，等不了收尾：干净取消蓄力（走 OnExit 清理）
                //而非冻结，顺带杜绝"冻结恢复后预警早已过期却凭空开火"的旧问题
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
            //TetherSpin 期间头部把收紧半径广播在指令槽（720→420），未广播/异常值时取默认
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

        /// <summary>
        /// 十字绞杀封位：指令预热期跟踪玩家收拢；头部正式进入绞杀状态的瞬间冻结锚点
        /// （与预警线/热射线对齐），此后各臂钉死在封位上沿射线向心瞄准
        /// </summary>
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

        /// <summary>转阶段殉爆：收拢贴身编队，预热火花，按延迟自爆</summary>
        private void RunDetonationSequence() {
            npc.dontTakeDamage = true;
            npc.damage = 0;
            npc.velocity = Vector2.Zero;

            float rot = MathHelper.TwoPi / 4 * FormationIndex + head.rotation;
            Vector2 toPoint = head.Center + rot.ToRotationVector2() * head.width;
            npc.Center = Vector2.Lerp(npc.Center, toPoint, 0.25f);
            npc.rotation = head.Center.To(npc.Center).ToRotation() - MathHelper.PiOver2;

            //殉爆倒计时（localAI 本地推进，两端各自走完一致的节拍）
            npc.localAI[2]++;

            //临爆前的窜火预兆
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

        /// <summary>共享视觉衰减：后坐力回落与硝烟</summary>
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
