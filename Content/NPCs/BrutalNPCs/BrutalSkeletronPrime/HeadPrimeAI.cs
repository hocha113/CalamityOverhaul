using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Magic;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Items.Modifys.ModifyBag;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.States;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.StateMachines;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime
{
    /// <summary>机械骷髅王头部 NPCOverride</summary>
    /// <para>战斗由 <see cref="States"/> 状态机驱动；契约见 <see cref="PrimePhase"/>、npc.ai[2]</para>
    internal class HeadPrimeAI : CWRNPCOverride, ICWRLoader, ILocalizedModType
    {
        #region 数据与资源
        public override int TargetID => NPCID.SkeletronPrime;

        public string LocalizationCategory => "BrutalNPCs";
        public static LocalizedText SkeletronPrime_Text { get; private set; }

        /// <summary>触发死亡演出的生命阈值</summary>
        internal const int DeathTriggerLife = 10;
        /// <summary>目标失效判定距离</summary>
        private const int MaxFindDistance = 6000;

        /// <summary>当前正在进行死亡演出的头部 whoAmI（供运镜/玩家锁定快速查询），无则为 -1</summary>
        internal static int ActivePerformanceHead = -1;

        private VaultStateMachine<PrimeStateContext> stateMachine;
        private PrimeStateContext stateContext;
        private Player targetPlayer;
        private int frame;
        private int frameCount;

        [VaultLoaden(CWRConstant.NPC + "BSP/BrutalSkeletron")]
        internal static Asset<Texture2D> HandAsset = null;
        [VaultLoaden(CWRConstant.NPC + "BSP/BSPCannon")]
        internal static Asset<Texture2D> BSPCannon = null;
        [VaultLoaden(CWRConstant.NPC + "BSP/BSPlaser")]
        internal static Asset<Texture2D> BSPlaser = null;
        [VaultLoaden(CWRConstant.NPC + "BSP/BSPPliers")]
        internal static Asset<Texture2D> BSPPliers = null;
        [VaultLoaden(CWRConstant.NPC + "BSP/BSPSAW")]
        internal static Asset<Texture2D> BSPSAW = null;
        [VaultLoaden(CWRConstant.NPC + "BSP/BSPRAM")]
        internal static Asset<Texture2D> BSPRAM = null;
        [VaultLoaden(CWRConstant.NPC + "BSP/BSPRAM_Forearm")]
        internal static Asset<Texture2D> BSPRAM_Forearm = null;
        [VaultLoaden(CWRConstant.NPC + "BSP/BrutalSkeletronGlow")]
        internal static Asset<Texture2D> HandAssetGlow = null;
        [VaultLoaden(CWRConstant.NPC + "BSP/BSPCannonGlow")]
        internal static Asset<Texture2D> BSPCannonGlow = null;
        [VaultLoaden(CWRConstant.NPC + "BSP/BSPlaserGlow")]
        internal static Asset<Texture2D> BSPlaserGlow = null;
        [VaultLoaden(CWRConstant.NPC + "BSP/BSPPliersGlow")]
        internal static Asset<Texture2D> BSPPliersGlow = null;
        [VaultLoaden(CWRConstant.NPC + "BSP/BSPSAWGlow")]
        internal static Asset<Texture2D> BSPSAWGlow = null;
        [VaultLoaden(CWRConstant.NPC + "BSP/BSPRAMGlow")]
        internal static Asset<Texture2D> BSPRAMGlow = null;
        [VaultLoaden(CWRConstant.NPC + "BSP/BSPRAM_ForearmGlow")]
        internal static Asset<Texture2D> BSPRAM_ForearmGlow = null;
        internal static Asset<Texture2D> Vanilla_TwinsBossBag;
        internal static Asset<Texture2D> Vanilla_DestroyerBossBag;
        internal static Asset<Texture2D> Vanilla_SkeletronPrimeBossBag;
        private static int iconIndex;
        #endregion

        #region 加载
        void ICWRLoader.LoadData() {
            string path = CWRConstant.NPC + "BSP/";
            CWRMod.Instance.AddBossHeadTexture(path + "Skeletron_Head", -1);
            iconIndex = ModContent.GetModBossHeadSlot(path + "Skeletron_Head");
        }

        void ICWRLoader.LoadAsset() {
            //先缓存原版的纹理
            Vanilla_TwinsBossBag = TextureAssets.Item[ItemID.TwinsBossBag];
            Vanilla_DestroyerBossBag = TextureAssets.Item[ItemID.DestroyerBossBag];
            Vanilla_SkeletronPrimeBossBag = TextureAssets.Item[ItemID.SkeletronPrimeBossBag];
            if (CWRServerConfig.Instance.BiologyOverhaul) {
                TextureAssets.Item[ItemID.TwinsBossBag] = CWRUtils.GetT2DAsset(CWRConstant.Item + "Bag/TwinBag");
                TextureAssets.Item[ItemID.DestroyerBossBag] = CWRUtils.GetT2DAsset(CWRConstant.Item + "Bag/DestroyerBag");
                TextureAssets.Item[ItemID.SkeletronPrimeBossBag] = CWRUtils.GetT2DAsset(CWRConstant.Item + "Bag/PrimeBag");
            }
            else {//中途关闭生物大修后需要恢复原版纹理
                TextureAssets.Item[ItemID.TwinsBossBag] = Vanilla_TwinsBossBag;
                TextureAssets.Item[ItemID.DestroyerBossBag] = Vanilla_DestroyerBossBag;
                TextureAssets.Item[ItemID.SkeletronPrimeBossBag] = Vanilla_SkeletronPrimeBossBag;
            }
        }

        void ICWRLoader.UnLoadData() {
            if (VaultUtils.isServer) {//下面的操作不能在服务器上运行
                return;
            }
            //无论在什么情况下，修改了原版纹理都需要恢复它
            if (Vanilla_TwinsBossBag != null) {
                TextureAssets.Item[ItemID.TwinsBossBag] = Vanilla_TwinsBossBag;
            }
            if (Vanilla_DestroyerBossBag != null) {
                TextureAssets.Item[ItemID.DestroyerBossBag] = Vanilla_DestroyerBossBag;
            }
            if (Vanilla_SkeletronPrimeBossBag != null) {
                TextureAssets.Item[ItemID.SkeletronPrimeBossBag] = Vanilla_SkeletronPrimeBossBag;
            }
            ActivePerformanceHead = -1;
        }

        public override void SetStaticDefaults() {
            SkeletronPrime_Text = this.GetLocalization(nameof(SkeletronPrime_Text),
                () => "别妄图用这愚蠢的东西杀死我!去死吧有机体!");
        }

        public override bool? CanCWROverride() {
            return null;
        }
        #endregion

        #region 初始化
        public override void SetProperty() {
            for (int i = 0; i < ai.Length; i++) {
                ai[i] = 0;
            }
            int newMaxLife = (int)(npc.lifeMax * 0.7f);
            npc.life = npc.lifeMax = newMaxLife;
            npc.defDefense = npc.defense = 20;
            InitializeStateContext();
        }

        private void InitializeStateContext() {
            stateContext = new PrimeStateContext {
                Npc = npc,
                Owner = this
            };
            stateMachine = new NpcStateMachine<PrimeStateContext>(stateContext, aiSlot: PrimeAiSlots.HeadStateSlot);

            //客户端中途加入时从 npc.ai[2] 恢复服务端当前状态，避免状态desync
            if (VaultUtils.isClient) {
                int serverStateIndex = (int)npc.ai[PrimeAiSlots.HeadStateSlot];
                IVaultState<PrimeStateContext> syncedState = VaultStateRegistry<PrimeStateContext>.Create(serverStateIndex);
                stateMachine.SetInitialState(syncedState ?? new PrimeIntroState());
            }
            else {
                stateMachine.SetInitialState(new PrimeIntroState());
            }
        }
        #endregion

        #region 主AI
        public override bool AI() {
            if (CWRWorld.CanTimeFrozen()) {
                CWRNpc.DoTimeFrozen(npc);
                return false;
            }

            //Mechdusa（机械混合体）形态交还原版AI，只维护原版簿记
            if (IsMechdusa(npc)) {
                NPC.mechQueen = npc.whoAmI;
                return true;
            }

            //延迟初始化保护
            if (stateContext == null || stateMachine == null) {
                InitializeStateContext();
            }

            npc.defense = npc.defDefense;
            npc.reflectsProjectiles = false;
            npc.dontTakeDamage = false;

            FindTarget();
            UpdateStateContext();
            EvaluateGlobalTransitions();

            //客户端：本Boss状态机AI高度依赖未同步的本地玩家坐标(冲撞锁向、悬停死区判定)，
            //本地推进会与服务端严重分歧再被netUpdate拉回，造成来回瞬移。
            //改为纯服务端权威：客户端丢弃状态机算出的运动学(位置还原、速度清零不外推)，
            //只呈现服务端每帧广播的权威位置；视觉副作用(旋转/帧/charge/粒子)仍照常执行
            bool clientShadow = VaultUtils.isClient;
            Vector2 savedPos = npc.position;

            stateMachine.Update();

            if (clientShadow) {
                npc.position = savedPos;
                npc.velocity = Vector2.Zero;
            }

            //Boss急速模式：四肢健在时为头部缓慢供血
            if (stateContext.BossRush && npc.life < npc.lifeMax - 20) {
                LifeRecovery();
            }

            UpdateMechThermalVisualState();

            //编队旋转时钟（供机械臂环绕编队取角）
            ai[PrimeAiSlots.OverrideOrbitClock]++;

            //纯服务端权威下客户端不外推，必须每帧广播权威位置，冲撞等高速段才不会跳帧
            if (!VaultUtils.isClient) {
                npc.netUpdate = true;
            }

            return false;
        }

        private void FindTarget() {
            if (npc.target < 0 || npc.target >= 255 || !targetPlayer.Alives()) {
                npc.TargetClosest();
            }
            targetPlayer = Main.player[npc.target];

            if (TargetInvalid()) {
                npc.TargetClosest();
                targetPlayer = Main.player[npc.target];
            }

            //登场完成前不要判定脱战，防止生成距离过远导致的误离场
            if (!VaultUtils.isClient && npc.ai[PrimeAiSlots.HeadPhase] > PrimePhase.Intro && TargetInvalid()
                && stateMachine?.CurrentState is not PrimeDespawnState and not PrimeDeathState) {
                stateContext.DespawnFromCoinFury = stateMachine?.CurrentState is PrimeCoinGunFuryState;
                stateMachine?.ChangeState(new PrimeDespawnState());
            }
        }

        private bool TargetInvalid() {
            return targetPlayer == null || targetPlayer.dead || !targetPlayer.active
                || Math.Abs(npc.position.X - targetPlayer.position.X) > MaxFindDistance
                || Math.Abs(npc.position.Y - targetPlayer.position.Y) > MaxFindDistance;
        }

        private void UpdateStateContext() {
            stateContext.Npc = npc;
            stateContext.Target = targetPlayer;
            stateContext.Owner = this;
            stateContext.BossRush = CWRRef.GetBossRushActive();
            stateContext.DeathMode = CWRRef.GetDeathMode() || stateContext.BossRush;
            stateContext.MasterMode = Main.masterMode || stateContext.BossRush;

            CheakRam(out bool cannonAlive, out bool viceAlive, out bool sawAlive, out bool laserAlive);
            stateContext.CannonAlive = cannonAlive;
            stateContext.ViceAlive = viceAlive;
            stateContext.SawAlive = sawAlive;
            stateContext.LaserAlive = laserAlive;
        }

        /// <summary>全局转移裁决（服务端驱动，客户端经状态槽同步）；优先级：死亡演出>转阶段>白昼狂暴>金币枪狂怒</summary>
        private void EvaluateGlobalTransitions() {
            if (VaultUtils.isClient || stateMachine?.CurrentState == null) {
                return;
            }

            IVaultState<PrimeStateContext> current = stateMachine.CurrentState;
            int phase = (int)npc.ai[PrimeAiSlots.HeadPhase];

            //死亡演出：正式战斗阶段生命见底
            if (phase > PrimePhase.Intro && npc.life <= DeathTriggerLife
                && !stateContext.DeathPerformanceFinished && current is not PrimeDeathState) {
                stateMachine.ChangeState(new PrimeDeathState());
                return;
            }
            if (current is PrimeDeathState or PrimeDespawnState or PrimeIntroState or PrimePhaseTransitionState) {
                return;
            }

            //转阶段：武装阶段生命 ≤55% 或存活臂 ≤1，必定触发。
            if (phase == PrimePhase.Armed && ShouldPhaseTransition()) {
                stateMachine.ChangeState(new PrimePhaseTransitionState());
                return;
            }

            //白昼狂暴
            if (Main.IsItDay() && current is not PrimeDayEnrageState) {
                stateMachine.ChangeState(new PrimeDayEnrageState());
                return;
            }
            if (current is PrimeDayEnrageState) {
                return;
            }

            //金币枪狂怒
            if (PrimeCoinGunFuryState.IsProvoking(targetPlayer) && current is not PrimeCoinGunFuryState) {
                stateMachine.ChangeState(new PrimeCoinGunFuryState());
            }
        }

        private void LifeRecovery() {
            if (stateContext.CannonAlive) {
                npc.life += 1;
            }
            if (stateContext.LaserAlive) {
                npc.life += 1;
            }
            if (stateContext.SawAlive) {
                npc.life += 1;
            }
            if (stateContext.ViceAlive) {
                npc.life += 1;
            }
        }

        /// <summary>机械热感滤镜：按状态定模式/强度，头部+四肢共用 npc.whoAmI</summary>
        private void UpdateMechThermalVisualState() {
            IVaultState<PrimeStateContext> current = stateMachine?.CurrentState;

            //死亡演出：满强度红色警告脉动
            if (current is PrimeDeathState) {
                MechBossVisualState.Push(npc.whoAmI, MechBossVisualMode.Warning, 1f,
                    0.5f + 0.5f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 18.0));
                return;
            }

            //登场期间不施加滤镜，保持原始演出
            if (current is PrimeIntroState
                || npc.ai[PrimeAiSlots.HeadPhase] <= PrimePhase.Intro) {
                return;
            }

            //冲撞突进：白热高速
            if (current is PrimeSpinDashState or PrimeRageDashState && npc.velocity.LengthSquared() > 12f * 12f) {
                MechBossVisualState.Push(npc.whoAmI, MechBossVisualMode.Dashing, 1f, 1f);
                return;
            }

            //金币枪狂怒：强烈红色警告
            if (current is PrimeCoinGunFuryState) {
                MechBossVisualState.Push(npc.whoAmI, MechBossVisualMode.Warning, 0.95f, 0.7f);
                return;
            }

            //蓄力预警（冲撞蓄势/转阶段过载/环形充能）
            if (stateContext.IsCharging) {
                MechBossVisualState.Push(npc.whoAmI, MechBossVisualMode.Warning, 0.85f, stateContext.ChargeProgress);
                return;
            }

            //狂暴常态：红橙描边稍强
            if (npc.ai[PrimeAiSlots.HeadPhase] >= PrimePhase.Rage) {
                MechBossVisualState.Push(npc.whoAmI, MechBossVisualMode.Idle, 0.8f, 0f);
                return;
            }

            //武装常态：低强度滤镜保夜晚能见度
            MechBossVisualState.Push(npc.whoAmI, MechBossVisualMode.Idle, 0.55f, 0f);
        }
        #endregion

        #region 对外契约与静态工具

        /// <summary>Mechdusa 形态交还原版逻辑</summary>
        internal static bool IsMechdusa(NPC head) {
            return head.ai[PrimeAiSlots.HeadMechQueenFlag] != 0f || NPC.IsMechQueenUp;
        }

        /// <summary>读取头部当前同步状态索引</summary>
        internal static PrimeStateIndex GetStateIndex(NPC head) {
            return (PrimeStateIndex)(int)head.ai[PrimeAiSlots.HeadStateSlot];
        }

        /// <summary>头部是否正在脱战离场</summary>
        internal static bool IsDespawnState(NPC head) {
            return GetStateIndex(head) == PrimeStateIndex.Despawn;
        }

        /// <summary>四肢是否处于收拢/环绕编队（机械臂连接件改用紧凑绘制）</summary>
        internal static bool InCompactFormation(NPC head) {
            return GetStateIndex(head) is PrimeStateIndex.SpinDash or PrimeStateIndex.RageDash
                or PrimeStateIndex.BarrageCommand or PrimeStateIndex.TetherSpin
                or PrimeStateIndex.DayEnrage or PrimeStateIndex.PhaseTransition;
        }

        internal static PrimeCommandKind GetActiveCommand(NPC head) {
            if (head == null || !head.active) {
                return PrimeCommandKind.None;
            }
            return (PrimeCommandKind)(int)head.ai[PrimeAiSlots.HeadCommandSlot];
        }

        private bool ShouldPhaseTransition() {
            int aliveArms = (stateContext.CannonAlive ? 1 : 0) + (stateContext.ViceAlive ? 1 : 0)
                + (stateContext.SawAlive ? 1 : 0) + (stateContext.LaserAlive ? 1 : 0);
            return npc.life <= npc.lifeMax * 0.45f || aliveArms <= 1;
        }

        internal static void FindPlayer(NPC npc) {
            if (npc.target < 0 || npc.target == Main.maxPlayers || Main.player[npc.target].dead || !Main.player[npc.target].active) {
                npc.TargetClosest();
            }
            if (Vector2.Distance(Main.player[npc.target].Center, npc.Center) > 3200f) {
                npc.TargetClosest();
            }
        }

        internal static int SetMultiplier(int num) {
            if (!CWRRef.GetBossRushActive()) {
                if (CWRRef.GetEarlyHardmodeProgressionReworkBool()) {
                    double firstMechMultiplier = 0.9f;
                    double secondMechMultiplier = 0.95f;
                    if (!NPC.downedMechBossAny) {
                        num = (int)(num * firstMechMultiplier);
                    }
                    else if ((!NPC.downedMechBoss1 && !NPC.downedMechBoss2)
                        || (!NPC.downedMechBoss2 && !NPC.downedMechBoss3)
                        || (!NPC.downedMechBoss3 && !NPC.downedMechBoss1)) {
                        num = (int)(num * secondMechMultiplier);
                    }
                }
                if (CWRWorld.Revenge) {
                    num = (int)(num * 0.75f);
                }
            }
            return num;
        }

        internal static void CheakRam(out bool cannonAlive, out bool viceAlive, out bool sawAlive, out bool laserAlive) {
            PrimeLimbStatus status = PrimeFacts.GetLimbStatus();
            cannonAlive = status.CannonAlive;
            viceAlive = status.ViceAlive;
            sawAlive = status.SawAlive;
            laserAlive = status.LaserAlive;
        }

        internal static void SpanFireLerterDustEffect(NPC npc, int modes) {
            Vector2 pos = npc.Center + (npc.rotation + MathHelper.PiOver2).ToRotationVector2() * 30;
            for (int i = 0; i < 4; i++) {
                float rot1 = MathHelper.PiOver2 * i;
                Vector2 vr = rot1.ToRotationVector2();
                for (int j = 0; j < modes; j++) {
                    PRTLoader.NewParticle<PRT_Spark>(pos, vr * (0.1f + j * 0.34f), Color.Red, Main.rand.NextFloat(1.2f, 1.3f)).Configure(false, 13);
                }
            }
        }

        #endregion

        #region 死亡演出对外数据（供钳子 Actor 与运镜层读取）

        /// <summary>是否正处于死亡演出主状态</summary>
        internal bool InDeathPerformance => npc.ai[PrimeAiSlots.HeadPhase] == PrimePhase.DeathShow;
        /// <summary>演出已运行帧数（各端本地推进）</summary>
        internal int DeathTimer => stateContext?.DeathTimer ?? 0;
        /// <summary>当前演出阶段</summary>
        internal PrimeDeathPhase CurrentDeathPhase => stateContext?.DeathPhase ?? PrimeDeathPhase.FakeDeath;
        /// <summary>锁定的被抓玩家索引</summary>
        internal int DeathTargetIndex => stateContext?.DeathTargetIndex ?? -1;
        /// <summary>玩家被举起的目标世界坐标（头部正下方）</summary>
        internal Vector2 DeathLiftPoint => npc.Center + new Vector2(0f, PrimeDeathState.DeathLiftDistance);
        /// <summary>被抓玩家实例，无效时为 null</summary>
        internal Player DeathTargetPlayer =>
            (DeathTargetIndex >= 0 && DeathTargetIndex < Main.maxPlayers) ? Main.player[DeathTargetIndex] : null;

        #endregion

        #region 生成辅助

        internal void SpawnHouengEffect() {
            for (int i = 0; i < 133; i++) {
                PRTLoader.NewParticle<PRT_Light>(npc.Center + VaultUtils.RandVr(0, npc.width), VaultUtils.RandVr(3, 13), Color.Red, Main.rand.Next(1, 3)).Configure(32);
            }
            for (int i = 0; i < 60; i++) {
                Vector2 dustV = VaultUtils.RandVr(3, 33);
                int dust = Dust.NewDust(npc.Center + VaultUtils.RandVr(0, npc.width), 1, 1, DustID.FireworkFountain_Red, dustV.X, dustV.Y);
                Main.dust[dust].scale = Main.rand.NextFloat(1, 6);
            }
        }

        internal void SpawnArm(int limit = 0) {
            if (VaultUtils.isClient) {
                return;
            }

            if (limit == 1 || limit == 0) {
                int primeCannon = NPC.NewNPC(npc.GetSource_FromAI(), (int)npc.Center.X, (int)npc.Center.Y, NPCID.PrimeCannon, npc.whoAmI);
                Main.npc[primeCannon].ai[PrimeAiSlots.ArmSide] = -1f;
                Main.npc[primeCannon].ai[PrimeAiSlots.ArmHeadIndex] = npc.whoAmI;
                Main.npc[primeCannon].target = npc.target;
                Main.npc[primeCannon].netUpdate = true;
            }
            if (limit == 2 || limit == 0) {
                int primeSaw = NPC.NewNPC(npc.GetSource_FromAI(), (int)npc.Center.X, (int)npc.Center.Y, NPCID.PrimeSaw, npc.whoAmI);
                Main.npc[primeSaw].ai[PrimeAiSlots.ArmSide] = 1f;
                Main.npc[primeSaw].ai[PrimeAiSlots.ArmHeadIndex] = npc.whoAmI;
                Main.npc[primeSaw].target = npc.target;
                Main.npc[primeSaw].netUpdate = true;
            }
            if (limit == 3 || limit == 0) {
                int primeVice = NPC.NewNPC(npc.GetSource_FromAI(), (int)npc.Center.X, (int)npc.Center.Y, NPCID.PrimeVice, npc.whoAmI);
                Main.npc[primeVice].ai[PrimeAiSlots.ArmSide] = -1f;
                Main.npc[primeVice].ai[PrimeAiSlots.ArmHeadIndex] = npc.whoAmI;
                Main.npc[primeVice].target = npc.target;
                Main.npc[primeVice].ai[PrimeAiSlots.ArmChargeTimer] = 150f;
                Main.npc[primeVice].netUpdate = true;
            }
            if (limit == 4 || limit == 0) {
                int primeLaser = NPC.NewNPC(npc.GetSource_FromAI(), (int)npc.Center.X, (int)npc.Center.Y, NPCID.PrimeLaser, npc.whoAmI);
                Main.npc[primeLaser].ai[PrimeAiSlots.ArmSide] = 1f;
                Main.npc[primeLaser].ai[PrimeAiSlots.ArmHeadIndex] = npc.whoAmI;
                Main.npc[primeLaser].target = npc.target;
                Main.npc[primeLaser].ai[PrimeAiSlots.ArmChargeTimer] = 150f;
                Main.npc[primeLaser].netUpdate = true;
            }
        }

        #endregion

        #region 死亡与掉落

        /// <summary>演出未完锁血拦截；演出完毕放行掉落；秒杀也先锁血切死亡演出</summary>
        public override bool? CheckDead() {
            int phase = (int)npc.ai[PrimeAiSlots.HeadPhase];

            //初始化/登场阶段锁血
            if (phase == PrimePhase.Uninit || phase == PrimePhase.Intro) {
                npc.dontTakeDamage = true;
                npc.life = 1;
                return false;
            }

            //演出已播完：放行真正死亡
            if (stateContext != null && stateContext.DeathPerformanceFinished) {
                return true;
            }

            //正式战斗阶段即便被秒杀也先锁血，由死亡演出接管
            npc.dontTakeDamage = true;
            npc.life = 1;
            if (!VaultUtils.isClient && stateMachine != null && stateMachine.CurrentState is not PrimeDeathState) {
                stateMachine.ChangeState(new PrimeDeathState());
            }
            return false;
        }

        public override bool? On_PreKill() {
            if (Main.zenithWorld) {
                NPC.downedMechBoss1 = NPC.downedMechBoss2 = NPC.downedMechBoss3 = true;
                if (Main.dedServ) {
                    NetMessage.SendData(MessageID.WorldData);
                }
            }
            return base.On_PreKill();
        }

        public override void ModifyNPCLoot(NPC thisNPC, NPCLoot npcLoot) {
            LeadingConditionRule rule = new LeadingConditionRule(new DropInDeathMode());
            rule.OnSuccess(ItemDropRule.Common(ModContent.ItemType<CommandersChainsaw>(), 4));
            rule.OnSuccess(ItemDropRule.Common(ModContent.ItemType<HyperionBarrage>(), 4));
            rule.OnSuccess(ItemDropRule.Common(ModContent.ItemType<CommandersStaff>(), 4));
            rule.OnSuccess(ItemDropRule.Common(ModContent.ItemType<CommandersClaw>(), 4));
            rule.OnSuccess(ItemDropRule.Common(ModContent.ItemType<RaiderGun>(), 4));
            npcLoot.Add(rule);
        }

        public override void BossHeadSlot(ref int index) {
            index = iconIndex;
        }

        #endregion

        #region 绘制

        public override bool FindFrame(int frameHeight) {
            if (IsMechdusa(npc)) {
                return true;
            }
            if (++frameCount <= 10) {
                return false;
            }
            frameCount = 0;

            switch (stateContext?.FrameMode ?? 0) {
                case 1://冲撞
                    if (++frame > 7 || frame < 4) {
                        frame = 4;
                    }
                    break;
                case 2://狂暴
                    if (++frame > 11 || frame < 8) {
                        frame = 8;
                    }
                    break;
                default:
                    if (++frame > 3) {
                        frame = 0;
                    }
                    break;
            }
            return false;
        }

        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            return IsMechdusa(npc);
        }

        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (IsMechdusa(npc)) {
                return true;
            }

            //金币枪狂怒的怒红
            if (stateMachine?.CurrentState is PrimeCoinGunFuryState) {
                drawColor = Color.Red;
            }

            Texture2D mainValue = HandAsset.Value;
            Texture2D glowValue = HandAssetGlow.Value;
            Rectangle rectangle = mainValue.GetRectangle(frame, 12);
            Vector2 orig = rectangle.Size() / 2;

            //充能漩涡（过载/环形充能）：画在残影与本体之下
            DrawChargeVortex(spriteBatch);

            //速度门控热残影
            DrawAfterimages(spriteBatch, mainValue, rectangle, orig);

            //外圈8向描边光环，远距可读
            Vector2 mainPos = npc.Center - Main.screenPosition;
            MechBossThermalRenderer.DrawOutlineHaloByController(spriteBatch, mainValue, mainPos, rectangle,
                npc.rotation, orig, npc.scale, SpriteEffects.None, npc.whoAmI);

            //本体套机械热感着色器
            bool shaderApplied = MechBossThermalRenderer.BeginThermalShaderByController(
                spriteBatch, mainValue, rectangle, npc.whoAmI, seed: 0f);
            spriteBatch.Draw(mainValue, mainPos, rectangle, drawColor,
                npc.rotation, orig, npc.scale, SpriteEffects.None, 0);
            if (shaderApplied) {
                MechBossThermalRenderer.EndThermalShader(spriteBatch);
            }

            //发光层独立绘制，自发光不受滤镜
            Main.EntitySpriteDraw(glowValue, mainPos, rectangle
                , Color.White, npc.rotation, orig, npc.scale, SpriteEffects.None, 0);

            return false;
        }

        /// <summary>ChargeType 2/3 充能漩涡；ChargeType 1 冲撞蓄力仅走热感滤镜</summary>
        private void DrawChargeVortex(SpriteBatch spriteBatch) {
            if (stateContext == null || !stateContext.IsCharging
                || stateContext.ChargeProgress <= 0.01f || stateContext.ChargeType < 2) {
                return;
            }
            Effect shader = EffectLoader.PrimeChargeVortex?.Value;
            if (shader == null) {
                return;
            }

            Color main = stateContext.ChargeType == 2 ? new Color(255, 150, 40) : new Color(255, 70, 22);
            float progress = MathHelper.Clamp(stateContext.ChargeProgress, 0f, 1f);
            shader.Parameters["uColor"]?.SetValue(main.ToVector3());
            shader.Parameters["uSecondaryColor"]?.SetValue(Color.Lerp(main, Color.White, 0.55f).ToVector3());
            shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            shader.Parameters["uProgress"]?.SetValue(progress);
            shader.Parameters["uIntensity"]?.SetValue(0.45f + progress * 0.75f);
            shader.Parameters["uOpacity"]?.SetValue(1f);
            shader.Parameters["uImage1"]?.SetValue(CWRAsset.Extra_193.Value);

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            shader.CurrentTechnique.Passes[0].Apply();

            Texture2D quad = CWRAsset.Placeholder_White.Value;
            float size = 360f + 220f * progress;
            spriteBatch.Draw(quad, npc.Center - Main.screenPosition, null, Color.White, 0f,
                quad.Size() / 2f, new Vector2(size / quad.Width, size / quad.Height), SpriteEffects.None, 0f);

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>高速 oldPos 链经 PrimeAfterimage 着色器绘残影；缺失时平涂回退</summary>
        private void DrawAfterimages(SpriteBatch spriteBatch, Texture2D mainValue, Rectangle rectangle, Vector2 orig) {
            float speed = npc.velocity.Length();
            bool ragePhase = npc.ai[PrimeAiSlots.HeadPhase] >= PrimePhase.Rage;
            float heat = MathHelper.Clamp((speed - 5f) / 16f, 0f, 1f);
            if (ragePhase) {
                heat = Math.Max(heat, 0.35f);
            }
            if (heat <= 0.05f) {
                return;
            }

            Effect shader = EffectLoader.PrimeAfterimage?.Value;
            if (shader == null) {
                DrawAfterimagesFallback(mainValue, rectangle, orig, ragePhase, heat);
                return;
            }

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            int count = npc.oldPos.Length;
            for (int i = 1; i < count; i++) {
                shader.Parameters["uFade"]?.SetValue(i / (float)count);
                shader.Parameters["uHeat"]?.SetValue(heat);
                shader.Parameters["uSeed"]?.SetValue(npc.whoAmI % 8 / 8f + i * 0.13f);
                shader.CurrentTechnique.Passes[0].Apply();
                Vector2 drawOldPos = npc.oldPos[i] + npc.Size / 2 - Main.screenPosition;
                spriteBatch.Draw(mainValue, drawOldPos, rectangle, Color.White,
                    npc.rotation, orig, npc.scale, SpriteEffects.None, 0);
            }

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }

        private void DrawAfterimagesFallback(Texture2D mainValue, Rectangle rectangle, Vector2 orig, bool ragePhase, float heat) {
            float sengs = 0.2f + heat * 0.3f;
            for (int i = 0; i < npc.oldPos.Length; i++) {
                Vector2 drawOldPos = npc.oldPos[i] + npc.Size / 2 - Main.screenPosition;
                Color trailColor = ragePhase
                    ? Color.Lerp(Color.White, new Color(255, 120, 60), heat) * sengs
                    : Color.White * sengs;
                Main.EntitySpriteDraw(mainValue, drawOldPos, rectangle, trailColor,
                    npc.rotation, orig, npc.scale, SpriteEffects.None, 0);
                sengs *= 0.8f;
            }
        }

        /// <summary>机械臂连接件：常态两段连杆，编队收拢时紧凑贴图</summary>
        internal static void DrawArm(SpriteBatch spriteBatch, NPC rCurrentNPC, Vector2 screenPos) {
            NPC head = Main.npc[(int)rCurrentNPC.ai[PrimeAiSlots.ArmHeadIndex]];

            if (InCompactFormation(head)) {
                float rCurrentNPCRotation = rCurrentNPC.rotation;
                Vector2 drawPos = rCurrentNPC.Center + (rCurrentNPCRotation + MathHelper.PiOver2).ToRotationVector2() * -120;
                Rectangle drawRec = BSPRAM.Value.GetRectangle();
                Vector2 drawOrig = drawRec.Size() / 2;
                Color color7 = Lighting.GetColor((int)drawPos.X / 16, (int)(drawPos.Y / 16f));
                drawPos -= Main.screenPosition;
                spriteBatch.Draw(BSPRAM.Value, drawPos, drawRec, color7, rCurrentNPCRotation, drawOrig, 1f, SpriteEffects.None, 0f);
                spriteBatch.Draw(BSPRAMGlow.Value, drawPos, drawRec, Color.White, rCurrentNPCRotation, drawOrig, 1f, SpriteEffects.None, 0f);
                return;
            }

            Vector2 vector7 = new Vector2(rCurrentNPC.position.X + rCurrentNPC.width * 0.5f - 5f * rCurrentNPC.ai[0], rCurrentNPC.position.Y + 20f);
            for (int k = 0; k < 2; k++) {
                float num21 = head.position.X + head.width / 2 - vector7.X;
                float num22 = head.position.Y + head.height / 2 - vector7.Y;
                float num23;

                if (k == 0) {
                    num21 -= 200f * rCurrentNPC.ai[0];
                    num22 += 130f;
                    num23 = (float)Math.Sqrt(num21 * num21 + num22 * num22);
                    num23 = 92f / num23;
                    vector7.X += num21 * num23;
                    vector7.Y += num22 * num23;
                }
                else {
                    num21 -= 50f * rCurrentNPC.ai[0];
                    num22 += 80f;
                    num23 = (float)Math.Sqrt(num21 * num21 + num22 * num22);
                    num23 = 60f / num23;
                    vector7.X += num21 * num23;
                    vector7.Y += num22 * num23;
                }

                float rotation7 = (float)Math.Atan2(num22, num21) - 1.57f;
                Color color7 = Lighting.GetColor((int)vector7.X / 16, (int)(vector7.Y / 16f));

                Texture2D value = BSPRAM.Value;
                Texture2D glow = BSPRAMGlow.Value;
                if (k == 0) {
                    value = BSPRAM_Forearm.Value;
                    glow = BSPRAM_ForearmGlow.Value;
                }

                Vector2 drawPos = new Vector2(vector7.X - screenPos.X, vector7.Y - screenPos.Y);
                Vector2 drawOrig = new Vector2(TextureAssets.BoneArm.Width() * 0.5f, TextureAssets.BoneArm.Height() * 0.5f);
                Rectangle drawRec = new Rectangle(0, 0, TextureAssets.BoneArm.Width(), TextureAssets.BoneArm.Height());
                SpriteEffects spriteEffects = k == 1 ? SpriteEffects.None : SpriteEffects.FlipVertically;

                spriteBatch.Draw(value, drawPos, drawRec, color7, rotation7, drawOrig, 1f, spriteEffects, 0f);
                spriteBatch.Draw(glow, drawPos, drawRec, Color.White, rotation7, drawOrig, 1f, spriteEffects, 0f);

                if (k == 0) {
                    vector7.X += num21 * num23 / 2f;
                    vector7.Y += num22 * num23 / 2f;
                }
                else if (Main.instance.IsActive) {
                    vector7.X += num21 * num23 - 16f;
                    vector7.Y += num22 * num23 - 6f;
                    int num24 = Dust.NewDust(new Vector2(vector7.X, vector7.Y), 30, 10
                        , DustID.FireworkFountain_Red, num21 * 0.02f, num22 * 0.02f, 0, Color.Gold, 0.5f);
                    Main.dust[num24].noGravity = true;
                }
            }
        }

        #endregion
    }
}
