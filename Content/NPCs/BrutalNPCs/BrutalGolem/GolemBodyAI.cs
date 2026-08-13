using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.Rendering;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.States;
using InnoVault.StateMachines;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem
{
    /// <summary>躯干主控 NPCOverride，States 驱动，契约见 GolemPhase、npc.ai[2]</summary>
    internal class GolemBodyAI : CWRNPCOverride, ICWRLoader
    {
        #region 数据
        public override int TargetID => NPCID.Golem;

        /// <summary>死亡演出中的躯干 whoAmI，无则 -1</summary>
        internal static int ActivePerformanceBody = -1;

        private VaultStateMachine<GolemStateContext> stateMachine;
        private GolemStateContext stateContext;
        private Player targetPlayer;
        private int frame;
        private int frameCount;

        /// <summary>状态上下文只读入口（渲染/部件观察用）</summary>
        internal GolemStateContext Context => stateContext;
        #endregion

        #region 加载
        void ICWRLoader.UnLoadData() {
            ActivePerformanceBody = -1;
            GolemScreenEffects.Clear();
        }

        public override bool? CanCWROverride() {
            return null;
        }
        #endregion

        #region 初始化
        public override void SetProperty() {
            npc.aiStyle = -1;
            npc.knockBackResist = 0;
            for (int i = 0; i < ai.Length; i++) {
                ai[i] = 0;
            }
            InitializeStateContext();
        }

        private void InitializeStateContext() {
            stateContext = new GolemStateContext {
                Npc = npc,
                Owner = this,
                //先给出兜底目标，防中途加入的客户端恢复状态时 OnEnter 解引用空目标
                Target = Main.player[npc.target >= 0 && npc.target < Main.maxPlayers ? npc.target : 0]
            };
            stateMachine = new NpcStateMachine<GolemStateContext>(stateContext, aiSlot: GolemAiSlots.BodyStateSlot);

            //中途加入从 ai[2] 恢复状态
            if (VaultUtils.isClient) {
                int serverStateIndex = (int)npc.ai[GolemAiSlots.BodyStateSlot];
                IVaultState<GolemStateContext> syncedState = VaultStateRegistry<GolemStateContext>.Create(serverStateIndex);
                stateMachine.SetInitialState(syncedState ?? new GolemIntroState());
            }
            else {
                stateMachine.SetInitialState(new GolemIntroState());
            }
        }
        #endregion

        #region 主AI
        public override bool AI() {
            //延迟初始化
            if (stateContext == null || stateMachine == null) {
                InitializeStateContext();
            }

            //维持原版注册表，供其它模组查询
            NPC.golemBoss = npc.whoAmI;

            npc.aiStyle = -1;
            npc.knockBackResist = 0;
            npc.netOffset = Vector2.Zero;
            npc.dontTakeDamage = false;
            //默认无接触伤害，仅冲撞状态开启（防站桩贴脸白吃伤害）
            npc.damage = 0;

            FindTarget();
            UpdateStateContext();
            EvaluateGlobalTransitions();

            //节拍广播默认归零，状态内自行写入（部件表现读取）
            npc.ai[GolemAiSlots.BodyBeat] = 0f;

            stateMachine.Update();

            //中途加入兜底：alpha 不走原版网络包，新客户端本地出生值 255；
            //过了 Intro 后由各端本地降（Intro 自己管淡入，Despawn 沉地淡出不干扰）
            GolemStateIndex visState = GolemFacts.GetStateIndex(npc);
            if (npc.alpha > 0 && visState is not GolemStateIndex.Intro and not GolemStateIndex.Despawn) {
                npc.alpha = Math.Max(npc.alpha - 12, 0);
            }

            //岩浆脉络余温衰减
            stateContext.VeinGlow = Math.Max(stateContext.VeinGlow - 0.012f, BaseVeinGlow());

            //演出通用时钟（仅表现，本地推进）
            ai[GolemAiSlots.OverrideShowClock]++;

            if (!VaultUtils.isClient && Main.GameUpdateCount % 10 == 0) {
                npc.netUpdate = true;
            }

            return false;
        }

        /// <summary>阶段基础脉络亮度</summary>
        private float BaseVeinGlow() {
            int phase = (int)npc.ai[GolemAiSlots.BodyPhase];
            if (phase >= GolemPhase.DeathShow) {
                return 1f;
            }
            if (phase >= GolemPhase.Sundered) {
                return stateContext.PostUltRage ? 0.72f : 0.45f;
            }
            //登场点火前石壳全暗
            return phase >= GolemPhase.Armed ? 0.18f : 0f;
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

            //登场前不脱战
            if (!VaultUtils.isClient && npc.ai[GolemAiSlots.BodyPhase] > GolemPhase.Intro && TargetInvalid()
                && stateMachine?.CurrentState is not GolemDespawnState and not GolemDeathState) {
                stateMachine?.ChangeState(new GolemDespawnState());
            }
        }

        private bool TargetInvalid() {
            return targetPlayer == null || targetPlayer.dead || !targetPlayer.active
                || Math.Abs(npc.position.X - targetPlayer.position.X) > GolemDirector.MaxFindDistance
                || Math.Abs(npc.position.Y - targetPlayer.position.Y) > GolemDirector.MaxFindDistance;
        }

        private void UpdateStateContext() {
            stateContext.Npc = npc;
            stateContext.Target = targetPlayer;
            stateContext.Owner = this;
            stateContext.BossRush = CWRRef.GetBossRushActive();
            stateContext.DeathMode = CWRRef.GetDeathMode() || stateContext.BossRush;
            stateContext.MasterMode = Main.masterMode || stateContext.BossRush;
            stateContext.Enraged = ComputeEnrage(targetPlayer, stateContext.BossRush);
            stateContext.Limbs = GolemFacts.ScanLimbs(npc.whoAmI);
        }

        /// <summary>神庙外激怒：目标在地表上，或身后墙不是神庙砖墙</summary>
        internal static bool ComputeEnrage(Player target, bool bossRush) {
            if (bossRush || target == null || !target.active) {
                return false;
            }
            if (target.Center.Y < Main.worldSurface * 16.0) {
                return true;
            }
            Tile tile = Framing.GetTileSafely((int)target.Center.X / 16, (int)target.Center.Y / 16);
            return tile.WallType != WallID.LihzahrdBrickUnsafe;
        }

        /// <summary>全局转移，服务端驱动，优先级 死亡>转阶段>大招</summary>
        private void EvaluateGlobalTransitions() {
            if (VaultUtils.isClient || stateMachine?.CurrentState == null) {
                return;
            }

            IVaultState<GolemStateContext> current = stateMachine.CurrentState;
            int phase = (int)npc.ai[GolemAiSlots.BodyPhase];

            //死亡演出
            if (phase > GolemPhase.Intro && npc.life <= GolemDirector.DeathTriggerLife
                && !stateContext.DeathPerformanceFinished && current is not GolemDeathState) {
                stateMachine.ChangeState(new GolemDeathState());
                return;
            }
            //不可打断的演出/仪式状态
            if (current is GolemDeathState or GolemDespawnState or GolemIntroState
                or GolemHeadDetachState or GolemSolarOverdriveState or GolemMeteorLeapState) {
                return;
            }

            //二阶段：头部分离仪式
            if (phase == GolemPhase.Armed && npc.life <= npc.lifeMax * GolemDirector.SunderLifeRatio) {
                stateMachine.ChangeState(new GolemHeadDetachState());
                return;
            }

            //低血大招（一场一次）
            if (phase == GolemPhase.Sundered && !stateContext.UltFired
                && npc.life <= npc.lifeMax * GolemDirector.UltLifeRatio) {
                stateMachine.ChangeState(new GolemSolarOverdriveState());
            }
        }
        #endregion

        #region 部件生成与拳指令
        /// <summary>生成双拳（服务端）</summary>
        internal void SpawnFists() {
            if (VaultUtils.isClient) {
                return;
            }
            SpawnOnePart(NPCID.GolemFistLeft, new Vector2(-84f, -9f));
            SpawnOnePart(NPCID.GolemFistRight, new Vector2(78f, -9f));
        }

        /// <summary>生成附着头（服务端）</summary>
        internal void SpawnAttachedHead() {
            if (VaultUtils.isClient) {
                return;
            }
            SpawnOnePart(NPCID.GolemHead, new Vector2(-3f, -57f));
        }

        /// <summary>生成分离飞头（服务端，转阶段仪式）</summary>
        internal void SpawnFreeHead() {
            if (VaultUtils.isClient) {
                return;
            }
            SpawnOnePart(NPCID.GolemHeadFree, new Vector2(-3f, -57f));
        }

        private void SpawnOnePart(int type, Vector2 offset) {
            int index = NPC.NewNPC(npc.GetSource_FromAI(),
                (int)(npc.Center.X + offset.X), (int)(npc.Center.Y + offset.Y), type);
            if (index < 0 || index >= Main.maxNPCs) {
                return;
            }
            NPC part = Main.npc[index];
            part.ai[GolemAiSlots.PartBodyIndex] = npc.whoAmI;
            part.target = npc.target;
            part.netUpdate = true;
        }

        /// <summary>向拳下达指令（服务端）：写 Override.ai 后随 SyncNPC 原子到达</summary>
        internal static void CommandFist(int fistIndex, GolemFistCommand kind, Vector2 point,
            int windup, float speed, int bounce, float sweepStartX = 0f) {
            if (VaultUtils.isClient || fistIndex < 0 || fistIndex >= Main.maxNPCs) {
                return;
            }
            NPC fist = Main.npc[fistIndex];
            if (!fist.active) {
                return;
            }
            //拳注册的是左右子类，禁用精确类型索引的 GetOverride（基类键不存在会抛出）
            GolemFistAI fistOverride = GolemFacts.FindOverride<GolemFistAI>(fist);
            if (fistOverride == null) {
                return;
            }
            fistOverride.ai[GolemAiSlots.FistCmdSeq] += 1f;
            fistOverride.ai[GolemAiSlots.FistCmdKind] = (int)kind;
            fistOverride.ai[GolemAiSlots.FistCmdX] = point.X;
            fistOverride.ai[GolemAiSlots.FistCmdY] = point.Y;
            fistOverride.ai[GolemAiSlots.FistBounce] = bounce;
            fistOverride.ai[GolemAiSlots.FistWindup] = windup;
            fistOverride.ai[GolemAiSlots.FistSpeed] = speed;
            fistOverride.ai[GolemAiSlots.FistSweepStartX] = sweepStartX;
            fist.netUpdate = true;
        }

        #endregion

        #region 死亡与掉落
        /// <summary>演出未完锁血，播完放行，秒杀也先切死亡演出</summary>
        public override bool? CheckDead() {
            int phase = (int)npc.ai[GolemAiSlots.BodyPhase];

            //登场锁血
            if (phase == GolemPhase.Uninit || phase == GolemPhase.Intro) {
                npc.dontTakeDamage = true;
                npc.life = 1;
                return false;
            }

            //演出完放行真死
            if (stateContext != null && stateContext.DeathPerformanceFinished) {
                return true;
            }

            //秒杀也锁血切死亡演出
            npc.dontTakeDamage = true;
            npc.life = 1;
            if (!VaultUtils.isClient && stateMachine != null && stateMachine.CurrentState is not GolemDeathState) {
                stateMachine.ChangeState(new GolemDeathState());
            }
            return false;
        }
        #endregion

        #region 绘制
        public override bool FindFrame(int frameHeight) {
            //帧组 0待机/1蹲伏/2跃空
            int mode = stateContext?.FrameMode ?? 0;
            int total = Math.Max(Main.npcFrameCount[NPCID.Golem], 1);

            if (++frameCount > 6) {
                frameCount = 0;
                switch (mode) {
                    case 1://蹲伏蓄势：帧逐级下压
                        if (frame < 3) {
                            frame++;
                        }
                        break;
                    case 2://跃空
                        frame = Math.Min(4, total - 1);
                        break;
                    default:
                        frame = 0;
                        break;
                }
            }
            if (mode == 0) {
                frame = 0;
            }

            frame = Math.Min(frame, total - 1);
            npc.frame.Y = frame * frameHeight;
            return false;
        }

        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            //死亡演出接管绘制：崩解侵蚀
            if (stateMachine?.CurrentState is GolemDeathState) {
                GolemRenderHelper.DrawBodyCrumble(spriteBatch, npc, stateContext, screenPos, drawColor);
                return false;
            }
            return null;
        }

        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (stateMachine?.CurrentState is GolemDeathState) {
                return false;
            }

            //岩浆脉络覆盖层
            GolemRenderHelper.DrawMagmaVeins(spriteBatch, npc, stateContext);

            //蓄力充能漩涡（太阳宝石位置）
            if (stateContext != null && stateContext.IsCharging && stateContext.ChargeProgress > 0.01f) {
                GolemRenderHelper.DrawGemCharge(spriteBatch, npc, stateContext);
            }
            return false;
        }
        #endregion

        #region 对外契约
        /// <summary>死亡演出计时（供运镜层读取）</summary>
        internal int DeathTimer => stateContext?.DeathTimer ?? 0;
        /// <summary>当前死亡演出阶段</summary>
        internal GolemDeathPhase CurrentDeathPhase => stateContext?.DeathPhase ?? GolemDeathPhase.Stagger;
        /// <summary>是否正处于死亡演出</summary>
        internal bool InDeathPerformance => npc.ai[GolemAiSlots.BodyPhase] == GolemPhase.DeathShow;
        #endregion
    }
}
