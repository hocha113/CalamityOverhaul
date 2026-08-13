using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu.Rendering;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu.States;
using InnoVault.StateMachines;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu
{
    /// <summary>克苏鲁之眼 AI 主控：血雾迷场与假动作冲刺，状态机驱动</summary>
    internal class EyeOfCthulhuAI : CWRNPCOverride, ICWRLoader, ILocalizedModType
    {
        #region 数据
        public string LocalizationCategory => "BrutalNPCs";

        public override int TargetID => NPCID.EyeofCthulhu;

        /// <summary>撕皮转阶段播报</summary>
        public static LocalizedText SkinTear_Text { get; private set; }
        /// <summary>血漩涡大招播报</summary>
        public static LocalizedText Maelstrom_Text { get; private set; }

        /// <summary>life 低于此值进死亡演出</summary>
        internal const int DeathPerformanceTriggerLife = 12;

        private VaultStateMachine<EocStateContext> stateMachine;
        private EocStateContext stateContext;
        private Player targetPlayer;
        /// <summary>远距滞留帧，达上限触发雾步回归</summary>
        private int farTimer;
        #endregion

        #region 加载与初始化
        void ICWRLoader.UnLoadData() {
            EocRenderHelper.Unload();
            EocScreenFX.Clear();
        }

        public override void SetStaticDefaults() {
            SkinTear_Text = this.GetLocalization(nameof(SkinTear_Text),
                () => "表皮裂开了——里面是一直没合上的口器。");
            Maelstrom_Text = this.GetLocalization(nameof(Maelstrom_Text),
                () => "血雾在收拢——漩涡要睁眼了。");
        }

        public override bool? CanCWROverride() {
            return null;
        }

        public override void SetProperty() {
            //残影与血带需要位置+旋转缓存
            NPCID.Sets.TrailingMode[npc.type] = 3;
            NPCID.Sets.TrailCacheLength[npc.type] = 24;

            //编舞战更长，血量上调
            npc.lifeMax = (int)(npc.lifeMax * 1.35f);
            npc.life = npc.lifeMax;
            npc.knockBackResist = 0f;

            InitializeStateContext();
        }

        private void InitializeStateContext() {
            stateContext = new EocStateContext {
                Npc = npc,
                IsDeathMode = CWRRef.GetDeathMode() || CWRRef.GetBossRushActive(),
            };
            stateMachine = new NpcStateMachine<EocStateContext>(stateContext, aiSlot: 2);

            //客户端从 ai[2] 恢复状态
            if (VaultUtils.isClient) {
                int serverStateIndex = (int)npc.ai[2];
                IVaultState<EocStateContext> syncedState = VaultStateRegistry<EocStateContext>.Create(serverStateIndex);
                stateMachine.SetInitialState(syncedState ?? new EocIntroState());
            }
            else {
                stateMachine.SetInitialState(new EocIntroState());
            }
        }
        #endregion

        #region 主AI
        public override bool AI() {
            //延迟初始化
            if (stateContext == null || stateMachine == null) {
                InitializeStateContext();
            }

            FindTarget();
            UpdateStateContext();
            CheckDayFlee();
            CheckPhaseTransition();
            CheckDeathPerformanceTrigger();

            //接触伤默认关，状态内按窗口开
            npc.damage = 0;

            stateMachine?.Update();

            UpdateFogStepValve();
            UpdateAnimation();
            ForcedNetUpdating(npc);

            Lighting.AddLight(npc.Center, EocMotion.Arterial.ToVector3() * 0.6f);

            if (!VaultUtils.isClient && Main.GameUpdateCount % 10 == 0) {
                npc.netUpdate = true;
            }

            return false;
        }

        private void FindTarget() {
            if (npc.target < 0 || npc.target >= 255 || !targetPlayer.Alives()) {
                npc.TargetClosest();
            }
            if (npc.target >= 0 && npc.target < 255) {
                targetPlayer = Main.player[npc.target];
            }

            if (!targetPlayer.Alives()) {
                if (!VaultUtils.isClient
                    && stateMachine?.CurrentState is not EocDespawnState and not EocDeathState) {
                    stateMachine?.ChangeState(new EocDespawnState());
                }
            }
        }

        private void UpdateStateContext() {
            stateContext.Npc = npc;
            stateContext.Target = targetPlayer ?? Main.player[Main.myPlayer];
            stateContext.IsDeathMode = CWRRef.GetDeathMode() || CWRRef.GetBossRushActive();
            stateContext.IsLowPhase = npc.life < npc.lifeMax * stateContext.LowPhaseRatio;

            //阶段旗随 ai[0] 同步，晚入场客户端由此恢复口器形态
            if (npc.ai[0] >= 2f) {
                stateContext.IsSecondPhase = true;
            }

            if (!VaultUtils.isClient && stateContext.MaelstromCooldown > 0) {
                stateContext.MaelstromCooldown--;
            }

            stateContext.DecayVisuals();
        }

        /// <summary>白昼撤离（Boss Rush 除外）</summary>
        private void CheckDayFlee() {
            if (VaultUtils.isClient || !Main.dayTime || CWRRef.GetBossRushActive()) {
                return;
            }
            if (stateMachine?.CurrentState is EocDespawnState or EocDeathState) {
                return;
            }
            stateMachine?.ChangeState(new EocDespawnState());
        }

        /// <summary>血量过阈切撕皮演出，权威端</summary>
        private void CheckPhaseTransition() {
            if (VaultUtils.isClient || stateContext == null || stateMachine == null) {
                return;
            }
            if (stateContext.IsSecondPhase || stateContext.IsInPhaseTransition) {
                return;
            }
            if (stateMachine.CurrentState is EocIntroState or EocDespawnState or EocDeathState) {
                return;
            }
            if (npc.life < npc.lifeMax * stateContext.Phase2Ratio) {
                stateMachine.ChangeState(new EocPhaseTransitionState());
            }
        }

        /// <summary>濒死切死亡演出，权威端</summary>
        private void CheckDeathPerformanceTrigger() {
            if (VaultUtils.isClient || stateContext == null || stateMachine == null) {
                return;
            }
            if (stateContext.DeathPerformanceFinished) {
                return;
            }
            if (stateMachine.CurrentState is EocDeathState or EocDespawnState) {
                return;
            }
            if (npc.life <= DeathPerformanceTriggerLife) {
                stateMachine.ChangeState(new EocDeathState());
            }
        }

        /// <summary>远距雾步回归阀：太远滞留则化雾折返，权威端</summary>
        private void UpdateFogStepValve() {
            if (VaultUtils.isClient) {
                return;
            }
            if (stateMachine?.CurrentState is not EocStateBase state || !state.AllowFogStep) {
                farTimer = 0;
                return;
            }
            if (!targetPlayer.Alives()) {
                farTimer = 0;
                return;
            }

            float dist = npc.Distance(targetPlayer.Center);
            if (dist <= 2400f) {
                farTimer = 0;
                return;
            }

            if (++farTimer < 30) {
                return;
            }
            farTimer = 0;
            EocMotion.FogStep(npc, targetPlayer);
        }

        private void UpdateAnimation() {
            stateContext.FrameCounter++;
            if (stateContext.FrameCounter >= stateContext.FrameRate) {
                stateContext.FrameCounter = 0;
                stateContext.FrameIndex = (stateContext.FrameIndex + 1) % 3;
            }
        }

        /// <summary>远端玩家周期性全量刷新，防长战漂移</summary>
        internal static void ForcedNetUpdating(NPC npc) {
            if (!VaultUtils.isServer || !npc.active || Main.GameUpdateCount % 80 != 0) {
                return;
            }
            foreach (var findPlayer in Main.ActivePlayers) {
                if (findPlayer.Distance(npc.position) < 1440) {
                    continue;
                }
                npc.SendNPCbasicData(findPlayer.whoAmI);
            }
        }
        #endregion

        #region 死亡与激活
        public override bool CheckActive() => false;

        /// <summary>演出中锁血，完后放行；秒杀也先切演出</summary>
        public override bool? CheckDead() {
            if (stateContext == null || stateContext.DeathPerformanceFinished) {
                npc.dontTakeDamage = false;
                return true;
            }

            npc.life = 1;
            npc.dontTakeDamage = true;

            if (!VaultUtils.isClient && stateMachine != null && stateMachine.CurrentState is not EocDeathState) {
                stateMachine.ChangeState(new EocDeathState());
            }

            return false;
        }
        #endregion

        #region 绘制
        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (stateContext == null) {
                return true;
            }

            //预警车道先铺底
            EocRenderHelper.DrawTelegraphLane(spriteBatch, stateContext);
            EocRenderHelper.DrawBody(spriteBatch, npc, stateContext, screenPos, drawColor);
            return false;
        }

        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            return false;
        }
        #endregion
    }
}
