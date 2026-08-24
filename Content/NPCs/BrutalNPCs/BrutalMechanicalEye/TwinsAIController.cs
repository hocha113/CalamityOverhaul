using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Magic;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Items.Modifys.ModifyBag;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Rendering;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Retinazer;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Spazmatism;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.Common;
using InnoVault.GameSystem;
using InnoVault.StateMachines;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Reflection;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.ItemDropRules;
using Terraria.GameContent.UI.BigProgressBar;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye
{
    /// <summary>双子魔眼 AI 控制器，状态机驱动战斗</summary>
    internal class TwinsAIController : BrutalNPCOverride, ICWRLoader, ILocalizedModType
    {
        public string LocalizationCategory => "BrutalNPCs";

        #region 常量与枚举

        private enum PrimaryAIState
        {
            Initialization = 0,
            Debut = 1,
            Battle = 2,
            EnragedBattle = 3,
            Flee = 4
        }

        /// <summary>死亡演出血量阈值，同毁灭者</summary>
        private const int DeathPerformanceTriggerLife = 10;

        #endregion

        #region 字段与属性

        private delegate void TwinsBigProgressBarDrawDelegate(
            TwinsBigProgressBar inds,
            ref BigProgressBarInfo info,
            SpriteBatch spriteBatch
        );

        public override int TargetID => NPCID.Spazmatism;

        protected VaultStateMachine<TwinsStateContext> stateMachine;

        protected TwinsStateContext stateContext;

        protected Player player;

        public static Color TextColor1 => new(155, 215, 215);
        public static Color TextColor2 => new(200, 54, 91);

        #endregion

        #region 资源加载

        [VaultLoaden(CWRConstant.NPC + "BEYE/Spazmatism")]
        internal static Asset<Texture2D> SpazmatismAsset = null;

        [VaultLoaden(CWRConstant.NPC + "BEYE/SpazmatismAlt")]
        internal static Asset<Texture2D> SpazmatismAltAsset = null;

        [VaultLoaden(CWRConstant.NPC + "BEYE/Retinazer")]
        internal static Asset<Texture2D> RetinazerAsset = null;

        [VaultLoaden(CWRConstant.NPC + "BEYE/RetinazerAlt")]
        internal static Asset<Texture2D> RetinazerAltAsset = null;

        private static int spazmatismIconIndex;
        private static int retinazerIconIndex;
        private static int spazmatismAltIconIndex;
        private static int retinazerAltIconIndex;
        private FieldInfo _cacheField;
        private FieldInfo _headIndexField;

        void ICWRLoader.LoadData() {
            CWRMod.Instance.AddBossHeadTexture(CWRConstant.NPC + "BEYE/Spazmatism_Head", -1);
            spazmatismIconIndex = ModContent.GetModBossHeadSlot(CWRConstant.NPC + "BEYE/Spazmatism_Head");
            CWRMod.Instance.AddBossHeadTexture(CWRConstant.NPC + "BEYE/Retinazer_Head", -1);
            retinazerIconIndex = ModContent.GetModBossHeadSlot(CWRConstant.NPC + "BEYE/Retinazer_Head");

            CWRMod.Instance.AddBossHeadTexture(CWRConstant.NPC + "BEYE/SpazmatismAlt_Head", -1);
            spazmatismAltIconIndex = ModContent.GetModBossHeadSlot(CWRConstant.NPC + "BEYE/SpazmatismAlt_Head");
            CWRMod.Instance.AddBossHeadTexture(CWRConstant.NPC + "BEYE/RetinazerAlt_Head", -1);
            retinazerAltIconIndex = ModContent.GetModBossHeadSlot(CWRConstant.NPC + "BEYE/RetinazerAlt_Head");

            MethodInfo methodInfo = typeof(TwinsBigProgressBar).GetMethod("Draw", BindingFlags.Public | BindingFlags.Instance);
            VaultHook.Add(methodInfo, OnTwinsBigProgressBarDrawHook);
            _cacheField = typeof(TwinsBigProgressBar).GetField("_cache", BindingFlags.NonPublic | BindingFlags.Instance);
            _headIndexField = typeof(TwinsBigProgressBar).GetField("_headIndex", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        void ICWRLoader.UnLoadData() {
            _cacheField = null;
            _headIndexField = null;
        }

        #endregion

        #region 初始化

        public override bool? CanBrutalOverride() {
            return null;
        }

        public override void SetProperty() {
            npc.realLife = -1;

            for (int i = 0; i < ai.Length; i++) {
                ai[i] = 0;
            }

            //魔焰生成时重置同步
            if (npc.type == NPCID.Spazmatism) {
                TwinsStateContext.ResetSyncData();
            }

            InitializeStateContext();
        }

        private void InitializeStateContext() {
            stateContext = new TwinsStateContext {
                Npc = npc,
                Ai = ai,
                IsDeathMode = CWRRef.GetDeathMode() || CWRRef.GetBossRushActive(),
                IsSpazmatism = npc.type == NPCID.Spazmatism
            };

            stateMachine = new NpcStateMachine<TwinsStateContext>(stateContext, aiSlot: 1);
        }

        #endregion

        #region Boss头像

        public override void BossHeadSlot(ref int index) {
            if (npc.type == NPCID.Spazmatism) {
                index = IsSecondPhase() ? spazmatismAltIconIndex : spazmatismIconIndex;
            }
            else {
                index = IsSecondPhase() ? retinazerAltIconIndex : retinazerIconIndex;
            }
        }

        private void OnTwinsBigProgressBarDrawHook(
            TwinsBigProgressBarDrawDelegate orig,
            TwinsBigProgressBar inds,
            ref BigProgressBarInfo info,
            SpriteBatch spriteBatch
        ) {
            int headIndex = (int)_headIndexField.GetValue(inds);
            if (headIndex < 0 || headIndex >= TextureAssets.NpcHeadBoss.Length) {
                return;
            }

            Texture2D value = TextureAssets.NpcHeadBoss[headIndex].Value;
            Rectangle barIconFrame = value.Frame();
            BigProgressBarCache _cache = (BigProgressBarCache)_cacheField.GetValue(inds);
            BigProgressBarHelper.DrawFancyBar(spriteBatch, _cache.LifeCurrent, _cache.LifeMax, value, barIconFrame);
        }

        #endregion

        #region 掉落物

        public override void ModifyNPCLoot(NPC thisNPC, NPCLoot npcLoot) {
            if (thisNPC.type != NPCID.Spazmatism) {
                return;
            }
            IItemDropRuleCondition condition = new DropInDeathMode();
            LeadingConditionRule rule = new LeadingConditionRule(condition);
            rule.SimpleAdd(ModContent.ItemType<FocusingGrimoire>(), 4);
            rule.SimpleAdd(ModContent.ItemType<GeminisTribute>(), 4);
            rule.SimpleAdd(ModContent.ItemType<Dicoria>(), 4);
            npcLoot.Add(rule);
        }

        #endregion

        #region 工具方法

        private void FindPlayer() {
            if (player != null && player.Alives()) {
                return;
            }
            npc.TargetClosest(true);
            player = Main.player[npc.target];
        }

        internal bool IsSecondPhase() {
            //登场中不进二阶段
            bool isInDebut = (PrimaryAIState)ai[0] == PrimaryAIState.Debut || (PrimaryAIState)ai[0] == PrimaryAIState.Initialization;
            if (isInDebut) {
                return false;
            }

            if (TwinsStateContext.Phase2Triggered) {
                return true;
            }

            //检查自身血量(只有在非登场状态下才检查)
            bool selfLowHealth = (npc.life / (float)npc.lifeMax) < 0.6f;

            NPC partner = TwinsStateContext.GetPartnerNpc(npc.type);
            if (partner != null && partner.active) {
                //查搭档是否登场中
                var partnerOverride = partner.GetGlobalNPC<CWRNpc>();
                bool partnerInDebut = false;

                //ai[0]查搭档登场
                //ai[0] 0初始化 1登场
                if (partner.ai[0] <= 1) {
                    partnerInDebut = true;
                }

                //搭档非登场才查血量
                if (!partnerInDebut) {
                    bool partnerLowHealth = (partner.life / (float)partner.lifeMax) < 0.6f;
                    if (partnerLowHealth || selfLowHealth) {
                        //任一眼低血→同步二阶段
                        TwinsStateContext.TriggerPhase2(npc.type);
                        return true;
                    }
                }
            }

            //仅自身低血触发
            if (selfLowHealth) {
                TwinsStateContext.TriggerPhase2(npc.type);
                return true;
            }

            return false;
        }

        #endregion

        #region AI核心

        public override bool AI() {
            // 天顶世界 Mechdusa 模式：交还原版 AI 以维持三王合体行为
            if (NPC.IsMechQueenUp) {
                return true;
            }

            UpdateAnimation();

            npc.spriteDirection = Math.Sign((npc.rotation + MathHelper.PiOver2).ToRotationVector2().X);

            FindPlayer();
            //死亡演出中不切逃跑
            if ((player == null || !player.active || player.dead)
                && stateMachine?.CurrentState is not TwinsDeathState) {
                if (ai[0] != (int)PrimaryAIState.Flee) {
                    ai[1] = 0;
                }
                ai[0] = (int)PrimaryAIState.Flee;
                npc.netUpdate = true;  //强制更新NPC
            }

            UpdateStateContext();

            CheckDeathPerformanceTrigger();

            return ProtogenesisAI();
        }

        private void UpdateAnimation() {
            stateContext.FrameCount++;
            if (stateContext.FrameCount > 5) {
                stateContext.FrameIndex = (stateContext.FrameIndex + 1) % 4;
                stateContext.FrameCount = 0;
            }
        }

        private void UpdateStateContext() {
            stateContext.Npc = npc;
            stateContext.Target = player;
            stateContext.IsSecondPhase = IsSecondPhase();
            stateContext.IsDeathMode = CWRRef.GetDeathMode() || CWRRef.GetBossRushActive();

            //冲刺视觉每帧衰减
            stateContext.DecayDashVisuals();

            //检测独眼狂暴模式(另一只眼睛死亡)
            CheckSoloRageMode();
        }

        private void CheckSoloRageMode() {
            //二阶段才检独眼
            if (!stateContext.IsSecondPhase || stateContext.IsSoloRageMode) {
                return;
            }

            //检查另一只眼睛是否存活
            NPC partner = TwinsStateContext.GetPartnerNpc(npc.type);
            bool partnerDead = !partner.Alives();

            //死亡模式允许搭档濒死即提前狂暴，但名额只有一个，避免双眼同时进无限狂暴
            if (!partnerDead && stateContext.IsDeathMode
                && (partner.life / (float)partner.lifeMax) < 0.15f) {
                partnerDead = TwinsStateContext.TryClaimEarlyRage(npc.type);
            }

            if (partnerDead) {
                stateContext.IsSoloRageMode = true;
                stateContext.SoloRageJustTriggered = true;

                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.3f, Volume = 1.5f }, npc.Center);
                }

                if (!VaultUtils.isServer) {
                    Color effectColor = stateContext.IsSpazmatism ? Color.OrangeRed : Color.BlueViolet;
                    for (int i = 0; i < 30; i++) {
                        float angle = MathHelper.TwoPi / 30f * i;
                        Vector2 vel = angle.ToRotationVector2() * 8f;
                        Dust dust = Dust.NewDustDirect(npc.Center, 1, 1,
                            stateContext.IsSpazmatism ? DustID.SolarFlare : DustID.Vortex,
                            vel.X, vel.Y, 0, default, 2.5f);
                        dust.noGravity = true;
                    }
                }
            }
        }

        #endregion

        #region 原生AI(独立战斗模式)

        /// <summary>濒死切死亡演出；服务端/单人；登场逃跑转阶不触发</summary>
        private void CheckDeathPerformanceTrigger() {
            if (VaultUtils.isClient) {
                return;
            }
            if (stateMachine == null || stateContext == null) {
                return;
            }
            if (stateContext.DeathPerformanceFinished) {
                return;
            }
            if (stateMachine.CurrentState is TwinsDeathState) {
                return;
            }
            //仅战斗阶段触发
            if (ai[0] != (int)PrimaryAIState.Battle && ai[0] != (int)PrimaryAIState.EnragedBattle) {
                return;
            }
            //转阶段无敌期不触发
            if (stateContext.IsInPhaseTransition || npc.dontTakeDamage) {
                return;
            }
            if (npc.life <= DeathPerformanceTriggerLife) {
                stateMachine.ChangeState(new TwinsDeathState());
            }
        }

        private bool ProtogenesisAI() {
            //死亡演出接管
            if (stateMachine?.CurrentState is TwinsDeathState) {
                stateMachine.Update();
                return false;
            }

            if (ai[0] == (int)PrimaryAIState.Flee) {
                npc.velocity.Y -= 0.5f;
                npc.EncourageDespawn(10);
                if (++ai[1] > 180) {
                    npc.active = false;
                    npc.netUpdate = true;
                }
                return false;
            }

            if (ai[0] == (int)PrimaryAIState.Initialization) {
                ai[0] = (int)PrimaryAIState.Debut;
                ai[1] = 0;
                npc.netUpdate = true;  //强制更新NPC
            }

            if (ai[0] == (int)PrimaryAIState.Debut) {
                if (!ExecuteDebutSequence()) {
                    ai[0] = (int)PrimaryAIState.Battle;
                    ai[1] = 0;
                    ai[2] = 0;
                    ai[3] = 0;

                    InitializeStateMachine();
                    npc.netUpdate = true;  //强制更新NPC
                }
                return false;
            }

            npc.dontTakeDamage = false;

            CheckPhaseTransition();

            //魔焰默认关接触伤
            //激光始终关接触伤
            if (npc.type == NPCID.Retinazer) {
                npc.damage = 0;
            }

            stateMachine?.Update();

            return false;
        }

        private void InitializeStateMachine() {
            IVaultState<TwinsStateContext> initialState;

            //客户端跟 ai[1] 同步态
            if (VaultUtils.isClient) {
                int serverStateIndex = (int)npc.ai[1];
                initialState = VaultStateRegistry<TwinsStateContext>.Create(serverStateIndex);
            }
            else {
                initialState = null;
            }

            if (initialState == null) {
                if (stateContext.IsSpazmatism) {
                    initialState = stateContext.IsSecondPhase
                        ? new SpazmatismFlameChaseState()
                        : new SpazmatismHoverShootState();
                }
                else {
                    initialState = stateContext.IsSecondPhase
                        ? new RetinazerVerticalBarrageState()
                        : new RetinazerHoverShootState();
                }
            }

            stateMachine.SetInitialState(initialState);

            //初始化关碰撞伤
            //魔焰仅冲刺态开伤
            //激光无碰撞伤
            npc.damage = 0;
        }

        private void CheckPhaseTransition() {
            bool secondPhase = IsSecondPhase();

            if (secondPhase && ai[0] != (int)PrimaryAIState.EnragedBattle) {
                ai[0] = (int)PrimaryAIState.EnragedBattle;
                ai[1] = 0;
                ai[2] = 0;
                ai[3] = 0;

                //原版二阶段 ai[0]==4
                npc.ai[0] = 4f;

                for (int i = 0; i < npc.buffType.Length; i++) {
                    npc.buffTime[i] = 0;
                }

                //切转阶段态，非直接二阶段
                TwinsPhaseTransitionState transitionState = new TwinsPhaseTransitionState();
                stateMachine.ChangeState(transitionState);
            }
        }

        private bool ExecuteDebutSequence() {
            if (ai[1] == 0) {
                npc.life = 1;
                npc.Center = player.Center;
                npc.Center += npc.type == NPCID.Spazmatism ? new Vector2(-1200, 1000) : new Vector2(1200, 1000);
            }

            npc.damage = 0;
            npc.dontTakeDamage = true;

            Vector2 toTarget = npc.Center.To(player.Center);
            npc.rotation = toTarget.ToRotation() - MathHelper.PiOver2;
            npc.velocity = Vector2.Zero;
            npc.position += player.velocity;
            Vector2 toPoint = player.Center;

            if (ai[1] < 60) {
                toPoint = player.Center + new Vector2(npc.type == NPCID.Spazmatism ? 500 : -500, 500);
            }
            else {
                toPoint = player.Center + new Vector2(npc.type == NPCID.Spazmatism ? -500 : 500, -500);

                if (ai[1] == 90 && !VaultUtils.isServer) {
                    SoundEngine.PlaySound(CWRSound.MechanicalFullBloodFlow, Main.LocalPlayer.Center);
                }

                if (ai[1] > 90) {
                    int addNum = (int)(npc.lifeMax / 80f);
                    if (npc.life >= npc.lifeMax) {
                        npc.life = npc.lifeMax;
                    }
                    else {
                        Lighting.AddLight(npc.Center, (npc.type == NPCID.Spazmatism ? Color.OrangeRed : Color.BlueViolet).ToVector3());
                        npc.life += addNum;
                        CombatText.NewText(npc.Hitbox, CombatText.HealLife, addNum);
                    }
                }
            }

            if (ai[1] > 180) {
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(CWRSound.SpawnArmMgs, Main.LocalPlayer.Center);
                }
                npc.dontTakeDamage = false;
                npc.damage = npc.defDamage;
                ai[0] = 2;
                ai[1] = 0;
                npc.netUpdate = true;  //强制更新NPC
                return false;
            }

            npc.Center = Vector2.Lerp(npc.Center, toPoint, 0.065f);

            ai[1]++;

            return true;
        }

        #endregion

        #region 其他覆写

        public override bool? CheckDead() {
            // 天顶世界 Mechdusa 模式由原版负责死亡
            if (NPC.IsMechQueenUp) {
                npc.dontTakeDamage = false;
                return true;
            }
            //无上下文→原版死亡
            if (stateContext == null) {
                npc.dontTakeDamage = false;
                return true;
            }
            //演出完放行真死
            if (stateContext.DeathPerformanceFinished) {
                npc.dontTakeDamage = false;
                return true;
            }
            //锁血进死亡演出
            npc.life = 1;
            npc.dontTakeDamage = true;
            if (!VaultUtils.isClient && stateMachine != null && stateMachine.CurrentState is not TwinsDeathState) {
                stateMachine.ChangeState(new TwinsDeathState());
            }
            return false;
        }

        public override bool CheckActive() => false;  //不要自动消失

        #endregion

        #region 绘制

        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            //Draw 前推热感，双眼独立
            PushThermalVisualState();

            Texture2D mainTexture = GetCurrentTexture();

            TwinsRenderHelper.DrawChargeEffect(spriteBatch, stateContext);

            //绘制本体+拉伸残影
            TwinsRenderHelper.DrawNpcBody(
                spriteBatch,
                npc,
                mainTexture,
                stateContext.FrameIndex,
                npc.rotation,
                stateContext
            );

            return false;
        }

        /// <summary>热感模式，冲刺Dashing/蓄力Warning/独眼Idle加强</summary>
        private void PushThermalVisualState() {
            if (stateContext == null) {
                return;
            }

            //死亡演出热感
            if (stateMachine?.CurrentState is TwinsDeathState) {
                MechBossVisualState.Push(npc.whoAmI, MechBossVisualMode.Warning, 1f,
                    0.5f + 0.5f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 18f));
                return;
            }

            //冲刺态优先级最高(具体冲刺状态或
            if (stateMachine?.CurrentState is SpazmatismDashingState
                || stateMachine?.CurrentState is SpazmatismPhase2DashingState
                || stateMachine?.CurrentState is SpazmatismShadowDashState
                || stateContext.DashStretch > 0.6f) {
                MechBossVisualState.Push(npc.whoAmI, MechBossVisualMode.Dashing, 1f, 1f);
                return;
            }

            //蓄力→Warning
            if (stateContext.IsCharging && stateContext.ChargeProgress > 0f) {
                //冲刺蓄力 type1/8/11/14 警告更强
                bool isDashCharge = stateContext.ChargeType == 1
                    || stateContext.ChargeType == 8
                    || stateContext.ChargeType == 11
                    || stateContext.ChargeType == 14;
                float intensity = isDashCharge ? 0.95f : 0.8f;
                float progress = stateContext.ChargeProgress;
                MechBossVisualState.Push(npc.whoAmI, MechBossVisualMode.Warning, intensity, progress);
                return;
            }

            //独眼狂暴
            if (stateContext.IsSoloRageMode) {
                MechBossVisualState.Push(npc.whoAmI, MechBossVisualMode.Idle, 0.55f, 0f);
                return;
            }

            //转阶段，保持警告色
            if (stateContext.IsInPhaseTransition) {
                MechBossVisualState.Push(npc.whoAmI, MechBossVisualMode.Warning, 0.7f, 0.5f);
                return;
            }

            //常态不推送
        }

        private Texture2D GetCurrentTexture() {
            if (npc.type == NPCID.Spazmatism) {
                return IsSecondPhase() ? SpazmatismAltAsset.Value : SpazmatismAsset.Value;
            }
            else {
                return IsSecondPhase() ? RetinazerAltAsset.Value : RetinazerAsset.Value;
            }
        }

        #endregion
    }
}
