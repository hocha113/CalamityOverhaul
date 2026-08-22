using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Rendering;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.States;
using InnoVault.StateMachines;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord
{
    /// <summary>月球领主核心主控：状态机指挥、部件生成回收、天体演出数据源</summary>
    internal class MoonLordCoreAI : CWRNPCOverride, ICWRLoader
    {
        #region 数据
        public override int TargetID => NPCID.MoonLordCore;

        /// <summary>死亡演出中的核心 whoAmI，无则 -1（运镜/玩家侧查询）</summary>
        internal static int ActivePerformanceCore = -1;

        private VaultStateMachine<MLordContext> stateMachine;
        private MLordContext stateContext;
        private Player targetPlayer;
        /// <summary>远距滞留帧，达上限触发日蚀回归瞬移</summary>
        private int farTimer;
        /// <summary>心跳帧（心脏裸露动画）</summary>
        private int heartFrameTick;
        private int heartFrame;
        /// <summary>逐部件破坏入账（服务端事件检测与归因基线）：槽0~3四手，槽4头</summary>
        private readonly bool[] countedBroken = new bool[MLordPartsStatus.HandSlots + 1];
        #endregion

        #region 加载与初始化
        void ICWRLoader.UnLoadData() {
            ActivePerformanceCore = -1;
            MLordScreenEffects.Clear();
            MLordEclipseSky.ResetDrive();
            MLordArmIK.Reset();
            MLordUltArms.Reset();
            MLordBlackFlashFX.Clear();
        }

        public override bool? CanCWROverride() {
            return null;
        }

        public override void SetProperty() {
            npc.aiStyle = -1;
            npc.knockBackResist = 0f;
            for (int i = 0; i < ai.Length; i++) {
                ai[i] = 0;
            }
            int newMaxLife = (int)(npc.lifeMax * MLordDirector.CoreLifeFactor);
            npc.life = npc.lifeMax = newMaxLife;
            InitializeStateContext();
        }

        private void InitializeStateContext() {
            stateContext = new MLordContext {
                Npc = npc,
                Owner = this,
                DeathMode = CWRRef.GetDeathMode() || CWRRef.GetBossRushActive()
            };
            stateMachine = new NpcStateMachine<MLordContext>(stateContext, aiSlot: MLordAiSlots.CoreStateSlot);

            //客户端从 ai[2] 恢复状态
            if (VaultUtils.isClient) {
                int serverStateIndex = (int)npc.ai[MLordAiSlots.CoreStateSlot];
                IVaultState<MLordContext> syncedState = VaultStateRegistry<MLordContext>.Create(serverStateIndex);
                stateMachine.SetInitialState(syncedState ?? new MLordIntroState());
            }
            else {
                stateMachine.SetInitialState(new MLordIntroState());
            }
        }
        #endregion

        #region 主AI
        public override bool AI() {
            if (stateContext == null || stateMachine == null) {
                InitializeStateContext();
            }

            npc.aiStyle = -1;
            npc.knockBackResist = 0f;
            npc.netOffset = Vector2.Zero;
            npc.damage = 0;
            npc.defense = npc.defDefense;

            //默认锁伤：三相拱卫期核心无敌，裸露/演出期由状态重申
            npc.dontTakeDamage = ShouldArmorLock();

            //全体免疫 debuff
            for (int i = 0; i < npc.buffImmune.Length; i++) {
                npc.buffImmune[i] = true;
            }

            FindTarget();
            UpdateStateContext();
            EvaluateGlobalTransitions();

            //表现驱动量每帧重申，未声明自然衰减
            stateContext.EclipseDrive = Math.Max(0f, stateContext.EclipseDrive - 0.012f);
            stateContext.HeartExposure = stateContext.CoreExposed ? 1f : Math.Max(0f, stateContext.HeartExposure - 0.02f);
            stateContext.HoldAllParts = false;
            stateContext.StaggerVulnerable = false;

            stateMachine.Update();

            UpdateFarReturnValve();
            UpdateHeartFrames();
            PushAmbience();

            //编队时钟
            ai[MLordAiSlots.OvFormationClock]++;

            if (!VaultUtils.isClient && Main.GameUpdateCount % 10 == 0) {
                npc.netUpdate = true;
            }
            return false;
        }

        /// <summary>三相拱卫期常驻锁伤；裸露、死亡演出（内部自锁）与哨兵期放开</summary>
        private bool ShouldArmorLock() {
            int phase = (int)npc.ai[MLordAiSlots.CorePhase];
            return phase is MLordPhase.Uninit or MLordPhase.Intro or MLordPhase.Trinity or MLordPhase.Leaving;
        }

        private void FindTarget() {
            if (npc.target < 0 || npc.target >= 255 || !targetPlayer.Alives()) {
                npc.TargetClosest();
            }
            targetPlayer = Main.player[npc.target];

            if (!targetPlayer.Alives()) {
                npc.TargetClosest();
                targetPlayer = Main.player[npc.target];
            }

            //登场后无有效目标进月退
            if (!VaultUtils.isClient && npc.ai[MLordAiSlots.CorePhase] > MLordPhase.Intro
                && !targetPlayer.Alives()
                && stateMachine?.CurrentState is not MLordDespawnState and not MLordDeathState) {
                stateMachine?.ChangeState(new MLordDespawnState());
            }
        }

        private void UpdateStateContext() {
            stateContext.Npc = npc;
            stateContext.Target = targetPlayer;
            stateContext.Owner = this;
            stateContext.BossRush = CWRRef.GetBossRushActive();
            stateContext.DeathMode = CWRRef.GetDeathMode() || stateContext.BossRush;
            stateContext.MasterMode = Main.masterMode || stateContext.BossRush;
            stateContext.Parts = MLordFacts.ScanParts(npc);
            stateContext.CoreExposed = (int)npc.ai[MLordAiSlots.CorePhase] == MLordPhase.CoreExposed;
        }

        /// <summary>全局转移，服务端驱动。优先级：死亡 > 部件破坏 > 大招</summary>
        private void EvaluateGlobalTransitions() {
            if (VaultUtils.isClient || stateMachine?.CurrentState == null) {
                return;
            }

            IVaultState<MLordContext> current = stateMachine.CurrentState;
            int phase = (int)npc.ai[MLordAiSlots.CorePhase];

            //死亡演出：低血阈值，或被巨伤一击触发原版 checkDead 的 398 特判
            //（该特判先于 tML 钩子执行，会把 ai[0] 写成 2 并回满生命，视作死亡信号接管）
            bool vanillaDeathConverted = phase == MLordPhase.VanillaDeathSentinel
                && !stateContext.DeathPerformanceFinished;
            if ((vanillaDeathConverted
                || (phase > MLordPhase.Intro && npc.life <= MLordDirector.DeathTriggerLife))
                && !stateContext.DeathPerformanceFinished && current is not MLordDeathState) {
                stateMachine.ChangeState(new MLordDeathState());
                return;
            }
            //掌中处刑不可被部件破坏/大招打断（死亡在上方仍可抢占）；
            //期间的新破坏此处不入账，处刑结束后首帧照常检测补演
            if (current is MLordDeathState or MLordDespawnState or MLordIntroState
                or MLordPartBreakState or MLordCoreExposureState or MLordPalmExecutionState) {
                return;
            }

            //部件破坏事件检测（逐部件归因，含同帧多破排队）
            int newBreaks = CountNewBreaks();
            if (newBreaks > 0 && phase == MLordPhase.Trinity) {
                stateContext.PendingBreakEvents += newBreaks - 1;
                stateMachine.ChangeState(new MLordPartBreakState());
                return;
            }

            //终局黑闪（一场一次，比虚空撕裂更迟解锁；不打断进行中的另一大招）
            if (stateContext.CoreExposed && ai[MLordAiSlots.OvBlackFlashUsed] == 0f
                && npc.life < npc.lifeMax * MLordDirector.BlackFlashLifeRatio
                && current is not MLordBlackFlashState and not MLordVoidRuptureState) {
                stateMachine.ChangeState(new MLordBlackFlashState());
                return;
            }

            //低血大招（一场一次，裸露期解锁）
            if (stateContext.CoreExposed && ai[MLordAiSlots.OvUltUsed] == 0f
                && npc.life < npc.lifeMax * MLordDirector.UltLifeRatio
                && current is not MLordVoidRuptureState and not MLordBlackFlashState) {
                stateMachine.ChangeState(new MLordVoidRuptureState());
            }
        }

        /// <summary>入账新破坏并写最近破坏部件归因槽，返回新增破坏数。
        /// 归因码：1上左/2上右/3头/4下左/5下右（手槽 slot → slot&lt;2 ? slot+1 : slot+2）；
        /// 同帧多破时首个写槽、其余归因入队，随排队事件逐个换写</summary>
        private int CountNewBreaks() {
            MLordPartsStatus parts = stateContext.Parts;
            int newBreaks = 0;

            for (int slot = 0; slot < MLordPartsStatus.HandSlots; slot++) {
                bool broken = parts.HandIndex(slot) >= 0 && !parts.HandAlive(slot);
                if (broken && !countedBroken[slot]) {
                    RecordBreak(ref newBreaks, slot < 2 ? slot + 1 : slot + 2);
                }
                countedBroken[slot] = broken;
            }

            bool headBroken = parts.Head >= 0 && !parts.HeadAlive;
            if (headBroken && !countedBroken[MLordPartsStatus.HandSlots]) {
                RecordBreak(ref newBreaks, 3);
            }
            countedBroken[MLordPartsStatus.HandSlots] = headBroken;

            if (newBreaks > 0) {
                npc.netUpdate = true;
            }
            return newBreaks;
        }

        /// <summary>首个破坏立即写归因槽，同帧其余入队待排队事件消费</summary>
        private void RecordBreak(ref int newBreaks, int code) {
            if (newBreaks == 0) {
                ai[MLordAiSlots.OvLastBrokenPart] = code;
            }
            else {
                stateContext.PendingBreakCodes.Add(code);
            }
            newBreaks++;
        }

        /// <summary>远距日蚀回归：整套阵形相对平移，保持拼装关系</summary>
        private void UpdateFarReturnValve() {
            if (VaultUtils.isClient || !targetPlayer.Alives()) {
                farTimer = 0;
                return;
            }
            if (stateMachine?.CurrentState is MLordIntroState or MLordDeathState or MLordDespawnState) {
                farTimer = 0;
                return;
            }
            if (npc.Distance(targetPlayer.Center) <= MLordDirector.FarSnapDistance) {
                farTimer = 0;
                return;
            }
            if (++farTimer < 30) {
                return;
            }
            farTimer = 0;

            //相对位移全家桶（原版 -2 段的搬迁逻辑）
            Vector2 shift = targetPlayer.Center + new Vector2(0f, -150f) - npc.Center;
            npc.position += shift;
            npc.netUpdate = true;
            foreach (NPC other in Main.ActiveNPCs) {
                if (!IsMyServant(other)) {
                    continue;
                }
                other.position += shift;
                other.netUpdate = true;
            }
        }

        /// <summary>是否本核心的从属实体（手/头/真眼按 ai[3] 归属，凝滴一律算）</summary>
        private bool IsMyServant(NPC other) {
            if (other.type == NPCID.MoonLordLeechBlob) {
                return true;
            }
            if (other.type != NPCID.MoonLordHand && other.type != NPCID.MoonLordHead
                && other.type != NPCID.MoonLordFreeEye) {
                return false;
            }
            return (int)other.ai[MLordAiSlots.PartCoreIndex] == npc.whoAmI;
        }

        /// <summary>心脏帧：未裸露定格护甲；裸露后循环搏动</summary>
        private void UpdateHeartFrames() {
            if (stateContext.HeartExposure < 0.5f) {
                heartFrame = 0;
                return;
            }
            if (++heartFrameTick >= 6) {
                heartFrameTick = 0;
                if (++heartFrame > 4 || heartFrame < 1) {
                    heartFrame = 1;
                }
            }
        }

        /// <summary>向天幕/滤镜推送氛围（客户端观察状态驱动，不新增网络包）</summary>
        private void PushAmbience() {
            if (VaultUtils.isServer) {
                return;
            }
            MLordEclipseSky.ReportBossDrive(npc.whoAmI, stateContext.EclipseDrive,
                stateContext.IsCharging ? stateContext.ChargeProgress : 0f);
            Lighting.AddLight(npc.Center, MLordDirector.DeepViolet.ToVector3() * 0.6f);
            if (stateContext.HeartExposure > 0.5f) {
                Lighting.AddLight(npc.Center, MLordDirector.Phantasmal.ToVector3() * stateContext.HeartExposure);
            }
        }
        #endregion

        #region 部件生成与回收

        /// <summary>登场拼装第一拍：上对双手+头（服务端）</summary>
        internal void SpawnUpperAssembly() {
            if (VaultUtils.isClient) {
                return;
            }
            SpawnHandPair(row: 0);
            int head = NPC.NewNPC(npc.GetSource_FromAI(), (int)npc.Center.X, (int)npc.Center.Y - 200,
                NPCID.MoonLordHead, npc.whoAmI);
            if (head < Main.maxNPCs) {
                Main.npc[head].ai[MLordAiSlots.PartCoreIndex] = npc.whoAmI;
                Main.npc[head].target = npc.target;
                Main.npc[head].netUpdate = true;
            }
        }

        /// <summary>登场拼装第二拍：下对双手（服务端）</summary>
        internal void SpawnLowerPair() {
            if (VaultUtils.isClient) {
                return;
            }
            SpawnHandPair(row: 1);
        }

        /// <summary>生成一对手：行位写 ai[1]，边位写 ai[2]（原版 checkDead 逐实例生效，追加手同样转破脱真眼）</summary>
        private void SpawnHandPair(int row) {
            int spawnY = (int)npc.Center.Y + (int)(row == 0
                ? MLordDirector.ShoulderOffset.Y : MLordDirector.LowerShoulderOffset.Y);
            for (int side = 0; side < 2; side++) {
                int hand = NPC.NewNPC(npc.GetSource_FromAI(), (int)npc.Center.X + (side * 2 - 1) * (120 + row * 60),
                    spawnY, NPCID.MoonLordHand, npc.whoAmI);
                if (hand < Main.maxNPCs) {
                    Main.npc[hand].ai[MLordAiSlots.HandRow] = row;
                    Main.npc[hand].ai[MLordAiSlots.HandSide] = side;
                    Main.npc[hand].ai[MLordAiSlots.PartCoreIndex] = npc.whoAmI;
                    Main.npc[hand].target = npc.target;
                    Main.npc[hand].netUpdate = true;
                }
            }
        }

        /// <summary>死亡演出：按序吞回一名从属（真眼×5→四手残口→头残口）</summary>
        internal void ConsumeOneServant() {
            if (VaultUtils.isClient) {
                return;
            }
            int[] eyeBuffer = new int[MLordFacts.MaxFreeEyes];
            int eyeCount = MLordFacts.ScanFreeEyes(npc, eyeBuffer);
            if (eyeCount > 0) {
                KillServant(Main.npc[eyeBuffer[0]]);
                return;
            }
            MLordPartsStatus parts = MLordFacts.ScanParts(npc);
            for (int slot = 0; slot < MLordPartsStatus.HandSlots; slot++) {
                if (parts.HandIndex(slot) >= 0) {
                    KillServant(Main.npc[parts.HandIndex(slot)]);
                    return;
                }
            }
            if (parts.Head >= 0) {
                KillServant(Main.npc[parts.Head]);
            }
        }

        /// <summary>清除全部从属（月退/收尾），服务端</summary>
        internal void RemoveAllServants(bool despawnEffect) {
            if (VaultUtils.isClient) {
                return;
            }
            foreach (NPC other in Main.ActiveNPCs) {
                if (!IsMyServant(other)) {
                    continue;
                }
                if (despawnEffect) {
                    other.life = 0;
                    other.HitEffect();
                }
                other.active = false;
                BroadcastServantRemoval(other);
            }
        }

        private static void KillServant(NPC servant) {
            servant.life = 0;
            servant.HitEffect();
            servant.active = false;
            BroadcastServantRemoval(servant);
        }

        /// <summary>
        /// 显式广播从属失活。被灭者的 UpdateNPC 因 !active 直接返回，
        /// 挂 netUpdate 永远不会被冲刷（原版清场同样手动 SendData 23）
        /// </summary>
        private static void BroadcastServantRemoval(NPC servant) {
            if (VaultUtils.isServer) {
                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, servant.whoAmI);
            }
        }

        /// <summary>胸甲炸开：原版四块装甲 Gore（客户端视觉）</summary>
        internal void PopChestPlates() {
            if (VaultUtils.isServer) {
                return;
            }
            Gore.NewGore(npc.GetSource_FromAI(), npc.position + new Vector2(-10f, -15f), npc.velocity, 619);
            Gore.NewGore(npc.GetSource_FromAI(), npc.position + new Vector2(10f, -15f), npc.velocity, 620);
            Gore.NewGore(npc.GetSource_FromAI(), npc.position + new Vector2(-10f, 15f), npc.velocity, 621);
            Gore.NewGore(npc.GetSource_FromAI(), npc.position + new Vector2(10f, 15f), npc.velocity, 622);
        }

        #endregion

        #region 死亡与受击

        /// <summary>演出未完锁血；播完挂哨兵放行原版真死（掉落/进度走原版）</summary>
        public override bool? CheckDead() {
            if (stateContext == null) {
                return true;
            }
            if (stateContext.DeathPerformanceFinished) {
                return true;
            }

            npc.life = 1;
            npc.dontTakeDamage = true;

            if (!VaultUtils.isClient && stateMachine != null && stateMachine.CurrentState is not MLordDeathState) {
                stateMachine.ChangeState(new MLordDeathState());
            }
            return false;
        }

        public override bool CheckActive() => false;

        /// <summary>大招硬直惩罚窗受击加伤</summary>
        public override void ModifyHitByItem(Player player, Item item, ref NPC.HitModifiers modifiers) {
            if (stateContext != null && stateContext.StaggerVulnerable) {
                modifiers.FinalDamage *= 1.3f;
            }
        }

        public override void ModifyHitByProjectile(Projectile projectile, ref NPC.HitModifiers modifiers) {
            if (stateContext != null && stateContext.StaggerVulnerable) {
                modifiers.FinalDamage *= 1.3f;
            }
        }

        #endregion

        #region 演出对外数据（运镜/玩家侧读取）

        /// <summary>是否处于死亡演出</summary>
        internal bool InDeathPerformance => npc.ai[MLordAiSlots.CorePhase] == MLordPhase.DeathShow;
        /// <summary>演出计时（本地推进）</summary>
        internal int DeathTimer => stateContext?.DeathTimer ?? 0;
        /// <summary>死亡演出阶段</summary>
        internal MLordDeathPhase CurrentDeathPhase => stateContext?.DeathPhase ?? MLordDeathPhase.Collapse;
        /// <summary>当前状态计时（部件侧姿态推导）</summary>
        internal int StateTimer => (stateMachine?.CurrentState as MLordStateBase)?.Timer ?? 0;
        /// <summary>状态上下文只读暴露（部件/渲染读取表现量）</summary>
        internal MLordContext Context => stateContext;

        #endregion

        #region 绘制

        public override bool FindFrame(int frameHeight) {
            //专用服务器纹理未加载（Value 为 null）且钩子照常被调（frameHeight=1），帧矩形只有绘制端消费
            if (VaultUtils.isServer) {
                return false;
            }
            //完整重建帧矩形：接管后原版不再初始化 frame 宽高
            int width = TextureAssets.Npc[npc.type].Value.Width;
            npc.frame = new Rectangle(0, heartFrame * frameHeight, width, frameHeight);
            return false;
        }

        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (stateContext == null) {
                return true;
            }
            MLordDrawHelper.DrawCoreAssembly(spriteBatch, npc, screenPos, stateContext);
            //黑闪四臂：仅大招期激活，模块自带渐出
            MLordUltArms.Draw(spriteBatch, npc, screenPos);
            return false;
        }

        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            return false;
        }

        #endregion
    }
}
