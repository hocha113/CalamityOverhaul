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
    internal class TwinsAIController : CWRNPCOverride, ICWRLoader, ILocalizedModType
    {
        public string LocalizationCategory => "BrutalNPCs";

        #region 常量与枚举

        /// <summary>AI主状态</summary>
        private enum PrimaryAIState
        {
            /// <summary>初始化</summary>
            Initialization = 0,
            /// <summary>登场演出</summary>
            Debut = 1,
            /// <summary>常规战斗</summary>
            Battle = 2,
            /// <summary>狂暴战斗(二阶段)</summary>
            EnragedBattle = 3,
            /// <summary>逃跑退场</summary>
            Flee = 4
        }

        /// <summary>进入死亡演出的血量阈值（与毁灭者一致，濒死时触发独立殉爆演出）</summary>
        private const int DeathPerformanceTriggerLife = 10;

        #endregion

        #region 字段与属性

        private delegate void TwinsBigProgressBarDrawDelegate(
            TwinsBigProgressBar inds,
            ref BigProgressBarInfo info,
            SpriteBatch spriteBatch
        );

        public override int TargetID => NPCID.Spazmatism;

        /// <summary>状态机实例</summary>
        protected VaultStateMachine<TwinsStateContext> stateMachine;

        /// <summary>状态上下文</summary>
        protected TwinsStateContext stateContext;

        /// <summary>目标玩家</summary>
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

        public override bool? CanCWROverride() {
            return null;
        }

        public override void SetProperty() {
            npc.realLife = -1;

            for (int i = 0; i < ai.Length; i++) {
                ai[i] = 0;
            }

            //重置同步数据(只在魔焰眼生成时重置，因为它通常先生成)
            if (npc.type == NPCID.Spazmatism) {
                TwinsStateContext.ResetSyncData();
            }

            //初始化状态上下文
            InitializeStateContext();
        }

        /// <summary>初始化状态上下文和状态机</summary>
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

        /// <summary>查找目标玩家</summary>
        private void FindPlayer() {
            if (player != null && player.Alives()) {
                return;
            }
            npc.TargetClosest(true);
            player = Main.player[npc.target];
        }

        /// <summary>是否进入二阶段</summary>
        internal bool IsSecondPhase() {
            //如果还在登场演出阶段，绝对不进入二阶段
            bool isInDebut = (PrimaryAIState)ai[0] == PrimaryAIState.Debut || (PrimaryAIState)ai[0] == PrimaryAIState.Initialization;
            if (isInDebut) {
                return false;
            }

            //检查是否已经触发了二阶段
            if (TwinsStateContext.Phase2Triggered) {
                return true;
            }

            //检查自身血量(只有在非登场状态下才检查)
            bool selfLowHealth = (npc.life / (float)npc.lifeMax) < 0.6f;

            //检查另一只眼睛的状态
            NPC partner = TwinsStateContext.GetPartnerNpc(npc.type);
            if (partner != null && partner.active) {
                //获取另一只眼睛的AI控制器来检查它是否在登场演出
                var partnerOverride = partner.GetGlobalNPC<CWRNpc>();
                bool partnerInDebut = false;

                //通过ai[0]检查另一只眼睛是否在登场演出
                //ai[0] == 0 是初始化，ai[0] == 1 是登场演出
                if (partner.ai[0] <= 1) {
                    partnerInDebut = true;
                }

                //只有当另一只眼睛也不在登场演出时，才检查它的血量
                if (!partnerInDebut) {
                    bool partnerLowHealth = (partner.life / (float)partner.lifeMax) < 0.6f;
                    if (partnerLowHealth || selfLowHealth) {
                        //任意一只眼睛低血量都触发同步二阶段
                        TwinsStateContext.TriggerPhase2(npc.type);
                        return true;
                    }
                }
            }

            //只有自身低血量才触发
            if (selfLowHealth) {
                TwinsStateContext.TriggerPhase2(npc.type);
                return true;
            }

            return false;
        }

        #endregion

        #region AI核心

        public override bool AI() {
            if (CWRWorld.CanTimeFrozen()) {
                CWRNpc.DoTimeFrozen(npc);
                return false;
            }

            //更新帧动画
            UpdateAnimation();

            //更新精灵方向
            npc.spriteDirection = Math.Sign((npc.rotation + MathHelper.PiOver2).ToRotationVector2().X);

            FindPlayer();
            //死亡演出期间不切换到逃跑(避免打断殉爆演出，也不重置状态机索引)
            if ((player == null || !player.active || player.dead)
                && stateMachine?.CurrentState is not TwinsDeathState) {
                if (ai[0] != (int)PrimaryAIState.Flee) {
                    ai[1] = 0;
                }
                ai[0] = (int)PrimaryAIState.Flee;
                npc.netUpdate = true;//强制更新NPC
            }

            //更新上下文
            UpdateStateContext();

            //濒死检测：触发独立死亡演出(每只眼睛各自播放)
            CheckDeathPerformanceTrigger();

            return ProtogenesisAI();
        }

        /// <summary>更新帧动画</summary>
        private void UpdateAnimation() {
            stateContext.FrameCount++;
            if (stateContext.FrameCount > 5) {
                stateContext.FrameIndex = (stateContext.FrameIndex + 1) % 4;
                stateContext.FrameCount = 0;
            }
        }

        /// <summary>更新状态上下文</summary>
        private void UpdateStateContext() {
            stateContext.Npc = npc;
            stateContext.Target = player;
            stateContext.IsSecondPhase = IsSecondPhase();
            stateContext.IsDeathMode = CWRRef.GetDeathMode() || CWRRef.GetBossRushActive();

            //冲刺视觉每帧衰减(状态只推高)
            stateContext.DecayDashVisuals();

            //检测独眼狂暴模式(另一只眼睛死亡)
            CheckSoloRageMode();
        }

        /// <summary>检测独眼狂暴模式</summary>
        private void CheckSoloRageMode() {
            //只有在二阶段时才检测独眼狂暴
            if (!stateContext.IsSecondPhase || stateContext.IsSoloRageMode) {
                return;
            }

            //检查另一只眼睛是否存活
            NPC partner = TwinsStateContext.GetPartnerNpc(npc.type);
            bool partnerDead = partner == null || !partner.active;
            if (stateContext.IsDeathMode && partner.Alives()) {
                partnerDead = (partner.life / (float)partner.lifeMax) < 0.15f;
            }

            if (partnerDead) {
                //触发独眼狂暴模式
                stateContext.IsSoloRageMode = true;
                stateContext.SoloRageJustTriggered = true;

                //播放狂暴音效
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.3f, Volume = 1.5f }, npc.Center);
                }

                //狂暴特效
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

        /// <summary>濒死切死亡演出；仅服务端/单人，登场/逃跑/转阶段无敌不触发</summary>
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
            //仅在正式战斗阶段触发(登场/逃跑/初始化阶段不触发)
            if (ai[0] != (int)PrimaryAIState.Battle && ai[0] != (int)PrimaryAIState.EnragedBattle) {
                return;
            }
            //转阶段无敌等期间不触发
            if (stateContext.IsInPhaseTransition || npc.dontTakeDamage) {
                return;
            }
            if (npc.life <= DeathPerformanceTriggerLife) {
                stateMachine.ChangeState(new TwinsDeathState());
            }
        }

        /// <summary>原生模式AI</summary>
        private bool ProtogenesisAI() {
            //死亡演出接管：跳过常规战斗/转阶段/逃跑逻辑，仅驱动死亡状态机
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

            //初始化状态
            if (ai[0] == (int)PrimaryAIState.Initialization) {
                ai[0] = (int)PrimaryAIState.Debut;
                ai[1] = 0;
                npc.netUpdate = true;//强制更新NPC
            }

            //登场演出
            if (ai[0] == (int)PrimaryAIState.Debut) {
                if (!ExecuteDebutSequence()) {
                    ai[0] = (int)PrimaryAIState.Battle;
                    ai[1] = 0;
                    ai[2] = 0;
                    ai[3] = 0;

                    //初始化状态机
                    InitializeStateMachine();
                    npc.netUpdate = true;//强制更新NPC
                }
                return false;
            }

            npc.dontTakeDamage = false;

            //检测二阶段转换
            CheckPhaseTransition();

            //碰撞伤害控制:
            //魔焰眼: 默认禁用，只有在冲刺状态才由状态机启用
            //激光眼: 始终禁用，因为它没有体术类型攻击
            if (npc.type == NPCID.Retinazer) {
                npc.damage = 0;
            }

            //更新状态机
            stateMachine?.Update();

            return false;
        }

        /// <summary>初始化状态机</summary>
        private void InitializeStateMachine() {
            IVaultState<TwinsStateContext> initialState;

            //客户端从 npc.ai[1] 恢复服务端当前状态
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

            //初始化后默认禁用碰撞伤害
            //魔焰眼只有在冲刺状态才会由状态机启用伤害
            //激光眼始终不造成碰撞伤害
            npc.damage = 0;
        }

        /// <summary>检测阶段转换</summary>
        private void CheckPhaseTransition() {
            bool secondPhase = IsSecondPhase();

            if (secondPhase && ai[0] != (int)PrimaryAIState.EnragedBattle) {
                ai[0] = (int)PrimaryAIState.EnragedBattle;
                ai[1] = 0;
                ai[2] = 0;
                ai[3] = 0;

                //同步原版npc.ai数组，让原版绘制可以识别二阶段
                //原版双子魔眼在二阶段时npc.ai[0]为4
                npc.ai[0] = 4f;

                //清除所有负面buff
                for (int i = 0; i < npc.buffType.Length; i++) {
                    npc.buffTime[i] = 0;
                }

                //切换到转阶段动画状态而不是直接进入二阶段
                TwinsPhaseTransitionState transitionState = new TwinsPhaseTransitionState();
                stateMachine.ChangeState(transitionState);
            }
        }

        /// <summary>执行登场演出</summary>
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
                npc.netUpdate = true;//强制更新NPC
                return false;
            }

            npc.Center = Vector2.Lerp(npc.Center, toPoint, 0.065f);

            ai[1]++;

            return true;
        }

        #endregion

        #region 其他覆写

        public override bool? CheckDead() {
            //上下文缺失：保持原版死亡行为
            if (stateContext == null) {
                npc.dontTakeDamage = false;
                return true;
            }
            //死亡演出已完成：放行真正死亡(触发掉落与击杀标记)
            if (stateContext.DeathPerformanceFinished) {
                npc.dontTakeDamage = false;
                return true;
            }
            //锁血进死亡演出，兜底一击致死也先播演出
            npc.life = 1;
            npc.dontTakeDamage = true;
            if (!VaultUtils.isClient && stateMachine != null && stateMachine.CurrentState is not TwinsDeathState) {
                stateMachine.ChangeState(new TwinsDeathState());
            }
            return false;
        }

        public override bool CheckActive() => false;//不要自动消失

        #endregion

        #region 绘制

        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            //Draw 前推送本眼热感视觉(双眼独立，可不同态)
            PushThermalVisualState();

            //获取纹理
            Texture2D mainTexture = GetCurrentTexture();

            //绘制蓄力特效
            TwinsRenderHelper.DrawChargeEffect(spriteBatch, stateContext);

            //绘制本体（内部会读取上面推送的状态自动叠加描边/警告/冲刺滤镜，
            //并根据上下文执行速度拉伸与残影增强）
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

        /// <summary>按状态机/蓄力推送热感模式：冲刺→Dashing，蓄力→Warning，独眼狂暴→Idle 加强</summary>
        private void PushThermalVisualState() {
            if (stateContext == null) {
                return;
            }

            //死亡演出：红黄过载脉冲，最高优先级
            if (stateMachine?.CurrentState is TwinsDeathState) {
                MechBossVisualState.Push(npc.whoAmI, MechBossVisualMode.Warning, 1f,
                    0.5f + 0.5f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 18f));
                return;
            }

            //冲刺态优先级最高(具体冲刺状态或任何把冲刺视觉推满的状态，如合击冲撞)
            if (stateMachine?.CurrentState is SpazmatismDashingState
                || stateMachine?.CurrentState is SpazmatismPhase2DashingState
                || stateMachine?.CurrentState is SpazmatismShadowDashState
                || stateContext.DashStretch > 0.6f) {
                MechBossVisualState.Push(npc.whoAmI, MechBossVisualMode.Dashing, 1f, 1f);
                return;
            }

            //蓄力态：警告滤镜，强度跟 ChargeProgress
            if (stateContext.IsCharging && stateContext.ChargeProgress > 0f) {
                //冲刺蓄力（type 1, 8）的警告更强烈
                bool isDashCharge = stateContext.ChargeType == 1
                    || stateContext.ChargeType == 8
                    || stateContext.ChargeType == 11;
                float intensity = isDashCharge ? 0.95f : 0.8f;
                float progress = stateContext.ChargeProgress;
                MechBossVisualState.Push(npc.whoAmI, MechBossVisualMode.Warning, intensity, progress);
                return;
            }

            //独眼狂暴：Idle 滤镜加强
            if (stateContext.IsSoloRageMode) {
                MechBossVisualState.Push(npc.whoAmI, MechBossVisualMode.Idle, 0.55f, 0f);
                return;
            }

            //转阶段：保持警告色
            if (stateContext.IsInPhaseTransition) {
                MechBossVisualState.Push(npc.whoAmI, MechBossVisualMode.Warning, 0.7f, 0.5f);
                return;
            }

            //常态不推送，Read 零强度
        }

        /// <summary>获取当前纹理</summary>
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
