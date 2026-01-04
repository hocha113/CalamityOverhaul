using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Magic;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Items.Rogue;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Rendering;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Retinazer;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Spazmatism;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime;
using CalamityOverhaul.Content.Projectiles.Boss.MechanicalEye;
using CalamityOverhaul.Content.Projectiles.Boss.SkeletronPrime;
using CalamityOverhaul.Content.RemakeItems.ModifyBag;
using InnoVault.GameSystem;
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
    /// <summary>
    /// 双子魔眼AI控制器
    /// 使用状态机模式管理战斗行为
    /// </summary>
    internal class TwinsAIController : CWRNPCOverride, ICWRLoader
    {
        #region 常量与枚举

        /// <summary>
        /// AI主状态
        /// </summary>
        private enum PrimaryAIState
        {
            /// <summary>
            /// 初始化
            /// </summary>
            Initialization = 0,
            /// <summary>
            /// 登场演出
            /// </summary>
            Debut = 1,
            /// <summary>
            /// 常规战斗
            /// </summary>
            Battle = 2,
            /// <summary>
            /// 狂暴战斗(二阶段)
            /// </summary>
            EnragedBattle = 3,
            /// <summary>
            /// 逃跑退场
            /// </summary>
            Flee = 4
        }

        private const int AccompanySpawnStage = 11;

        #endregion

        #region 字段与属性

        private delegate void TwinsBigProgressBarDrawDelegate(
            TwinsBigProgressBar inds,
            ref BigProgressBarInfo info,
            SpriteBatch spriteBatch
        );

        public override int TargetID => NPCID.Spazmatism;

        /// <summary>
        /// 状态机实例
        /// </summary>
        protected TwinsStateMachine stateMachine;

        /// <summary>
        /// 状态上下文
        /// </summary>
        protected TwinsStateContext stateContext;

        /// <summary>
        /// 目标玩家
        /// </summary>
        protected Player player;

        /// <summary>
        /// 是否为随从模式
        /// </summary>
        protected bool accompany;

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
            if (CWRWorld.MachineRebellion) {
                return true;
            }
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

            //检测随从模式
            accompany = false;
            foreach (var n in Main.npc) {
                if (!n.active) {
                    continue;
                }
                if (n.type == NPCID.SkeletronPrime) {
                    accompany = true;
                }
            }

            if (accompany) {
                ai[AccompanySpawnStage] = 0;
                NPC skeletronPrime = CWRUtils.FindNPCFromeType(NPCID.SkeletronPrime);
                if (skeletronPrime.Alives()) {
                    ai[AccompanySpawnStage] = skeletronPrime.ai[0] != 3 ? 1 : 0;
                }
                npc.life = npc.lifeMax = (int)(npc.lifeMax * 0.8f);
            }

            if (CWRWorld.MachineRebellion) {
                npc.life = npc.lifeMax *= 32;
                npc.defDefense = npc.defense = 40;
                npc.defDamage = npc.damage *= 2;
            }

            //初始化状态上下文
            InitializeStateContext();
        }

        /// <summary>
        /// 初始化状态上下文和状态机
        /// </summary>
        private void InitializeStateContext() {
            stateContext = new TwinsStateContext {
                Npc = npc,
                Ai = ai,
                IsAccompanyMode = accompany,
                IsMachineRebellion = CWRWorld.MachineRebellion,
                IsDeathMode = CWRRef.GetDeathMode() || CWRRef.GetBossRushActive(),
                IsSpazmatism = npc.type == NPCID.Spazmatism
            };

            stateMachine = new TwinsStateMachine(stateContext);
        }

        #endregion

        #region Boss头像

        public override void BossHeadSlot(ref int index) {
            if (HeadPrimeAI.DontReform()) {
                return;
            }
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

        /// <summary>
        /// 设置眼睛的位置和旋转(用于随从模式)
        /// </summary>
        public static void SetEyeValue(NPC eye, Player player, Vector2 toPoint, Vector2 toTarget) {
            float targetRotation = toTarget.ToRotation() - MathHelper.PiOver2;
            eye.damage = 0;
            eye.position += player.velocity;
            eye.Center = Vector2.Lerp(eye.Center, toPoint, 0.1f);
            eye.velocity = toTarget.UnitVector() * 0.01f;
            eye.EntityToRot(targetRotation, 0.2f);
        }

        /// <summary>
        /// 查找目标玩家
        /// </summary>
        private void FindPlayer() {
            if (player != null && player.Alives()) {
                return;
            }
            npc.TargetClosest(true);
            player = Main.player[npc.target];
        }

        /// <summary>
        /// 是否进入二阶段
        /// </summary>
        internal bool IsSecondPhase() {
            if (accompany) {
                NPC skeletronPrime = CWRUtils.FindNPCFromeType(NPCID.SkeletronPrime);
                if (skeletronPrime == null || !skeletronPrime.active) {
                    return false;
                }
                return skeletronPrime.ai[0] == 3;
            }

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
            if (player == null || !player.active || player.dead) {
                ai[0] = (int)PrimaryAIState.Flee;
                NetAISend();
            }

            //更新上下文
            UpdateStateContext();

            bool reset;
            if (accompany) {
                reset = AccompanyAI();
            }
            else {
                reset = ProtogenesisAI();
            }

            return reset;
        }

        /// <summary>
        /// 更新帧动画
        /// </summary>
        private void UpdateAnimation() {
            stateContext.FrameCount++;
            if (stateContext.FrameCount > 5) {
                stateContext.FrameIndex = (stateContext.FrameIndex + 1) % 4;
                stateContext.FrameCount = 0;
            }
        }

        /// <summary>
        /// 更新状态上下文
        /// </summary>
        private void UpdateStateContext() {
            stateContext.Npc = npc;
            stateContext.Target = player;
            stateContext.IsSecondPhase = IsSecondPhase();
            stateContext.IsDeathMode = CWRRef.GetDeathMode() || CWRRef.GetBossRushActive();
        }

        #endregion

        #region 原生AI(独立战斗模式)

        /// <summary>
        /// 原生模式AI
        /// </summary>
        private bool ProtogenesisAI() {
            if (player.dead || !player.active) {
                npc.velocity.Y -= 0.5f;
                npc.EncourageDespawn(10);
                return false;
            }

            //初始化状态
            if (ai[0] == (int)PrimaryAIState.Initialization) {
                ai[0] = (int)PrimaryAIState.Debut;
                ai[1] = 0;
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
                }
                return false;
            }

            npc.dontTakeDamage = false;

            //检测二阶段转换
            CheckPhaseTransition();

            //更新状态机
            stateMachine?.Update();

            return false;
        }

        /// <summary>
        /// 初始化状态机
        /// </summary>
        private void InitializeStateMachine() {
            ITwinsState initialState;

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

            stateMachine.SetInitialState(initialState);
        }

        /// <summary>
        /// 检测阶段转换
        /// </summary>
        private void CheckPhaseTransition() {
            bool secondPhase = IsSecondPhase();

            if (secondPhase && ai[0] != (int)PrimaryAIState.EnragedBattle) {
                ai[0] = (int)PrimaryAIState.EnragedBattle;
                ai[1] = 0;
                ai[2] = 0;
                ai[3] = 0;

                //清除所有负面buff
                for (int i = 0; i < npc.buffType.Length; i++) {
                    npc.buffTime[i] = 0;
                }

                //切换到转阶段动画状态而不是直接进入二阶段
                TwinsPhaseTransitionState transitionState = new TwinsPhaseTransitionState();
                stateMachine.ForceChangeState(transitionState);
            }
        }

        /// <summary>
        /// 执行登场演出
        /// </summary>
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

                if (ai[1] == 90 && !VaultUtils.isServer && !accompany) {
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
                if (!VaultUtils.isServer && !accompany) {
                    SoundEngine.PlaySound(CWRSound.SpawnArmMgs, Main.LocalPlayer.Center);
                }
                npc.dontTakeDamage = false;
                npc.damage = npc.defDamage;
                ai[0] = 2;
                ai[1] = 0;
                NetAISend();
                return false;
            }

            npc.Center = Vector2.Lerp(npc.Center, toPoint, 0.065f);

            ai[1]++;

            return true;
        }

        #endregion

        #region 随从AI

        /// <summary>
        /// 随从模式AI(与骷髅王配合)
        /// </summary>
        public bool AccompanyAI() {
            NPC skeletronPrime = CWRUtils.FindNPCFromeType(NPCID.SkeletronPrime);
            float lifeRog = npc.life / (float)npc.lifeMax;
            bool bossRush = CWRRef.GetBossRushActive();
            bool death = CWRRef.GetDeathMode() || bossRush;
            bool isSpazmatism = npc.type == NPCID.Spazmatism;
            bool lowBloodVolume = lifeRog < 0.7f;
            bool skeletronPrimeIsDead = !skeletronPrime.Alives();
            bool skeletronPrimeIsTwo = skeletronPrimeIsDead ? false : (skeletronPrime.ai[0] == 3);
            bool isSpawnFirstStage = ai[11] == 1;
            bool isSpawnFirstStageFromeExeunt = false;

            if (!skeletronPrimeIsDead && isSpawnFirstStage) {
                isSpawnFirstStageFromeExeunt = ((skeletronPrime.life / (float)skeletronPrime.lifeMax) < 0.6f);
            }

            int projType = isSpazmatism ? ModContent.ProjectileType<Fireball>() : ProjectileID.EyeLaser;
            int projDamage = 36;
            if (CWRWorld.MachineRebellion) {
                projDamage = 92;
            }

            player = skeletronPrimeIsDead ? Main.player[npc.target] : Main.player[skeletronPrime.target];

            Lighting.AddLight(npc.Center, (isSpazmatism ? Color.OrangeRed : Color.BlueViolet).ToVector3());

            if (ai[0] == 0) {
                if (!VaultUtils.isServer && isSpazmatism) {
                    VaultUtils.Text(CWRLocText.GetTextValue("Spazmatism_Text1"), TextColor1);
                    VaultUtils.Text(CWRLocText.GetTextValue("Spazmatism_Text2"), TextColor2);
                }
                ai[0] = 1;
                NetAISend();
            }

            if (ai[0] == 1) {
                if (ExecuteDebutSequence()) {
                    return false;
                }
            }

            if (IsSecondPhase()) {
                npc.HitSound = SoundID.NPCHit4;
            }

            if (skeletronPrimeIsDead || skeletronPrime?.ai[1] == 3 || lowBloodVolume || isSpawnFirstStageFromeExeunt) {
                ExecuteAccompanyExit(skeletronPrime, isSpazmatism, lowBloodVolume, isSpawnFirstStageFromeExeunt);
                return false;
            }

            Vector2 toTarget = npc.Center.To(player.Center);
            Vector2 toPoint = skeletronPrime.Center;
            npc.damage = npc.defDamage;
            HeadPrimeAI headPrime = skeletronPrime.GetOverride<HeadPrimeAI>();
            bool skeletronPrimeInSprint = skeletronPrime.ai[1] == 1;
            bool LaserWall = headPrime.ai[3] == 2;
            bool isDestroyer = HeadPrimeAI.setPosingStarmCount > 0;
            bool isIdle = headPrime.ai[10] > 0;

            if (isIdle) {
                toPoint = skeletronPrime.Center + new Vector2(isSpazmatism ? 50 : -50, -100);
                SetEyeValue(npc, player, toPoint, toTarget);
                return false;
            }

            if (LaserWall) {
                toPoint = player.Center + new Vector2(isSpazmatism ? 450 : -450, -400);
                SetEyeValue(npc, player, toPoint, toTarget);
                return false;
            }

            if (isDestroyer) {
                ExecuteDestroyerPhase(isSpazmatism, death, projType, projDamage, toTarget, skeletronPrimeIsTwo);
                return false;
            }
            else if (ai[8] != 0) {
                ai[8] = 0;
                NetAISend();
            }

            if (skeletronPrimeInSprint || ai[7] > 0) {
                ExecuteAccompanyAttackPhase(isSpazmatism, death, projType, projDamage, toTarget, skeletronPrimeIsTwo, isDestroyer);
                return false;
            }

            if (ai[7] > 0) {
                ai[7]--;
            }

            npc.VanillaAI();
            return false;
        }

        private void ExecuteAccompanyExit(
            NPC skeletronPrime,
            bool isSpazmatism,
            bool lowBloodVolume,
            bool isSpawnFirstStageFromeExeunt
        ) {
            npc.dontTakeDamage = true;
            npc.position += new Vector2(0, -36);

            if (ai[6] == 0 && !VaultUtils.isServer) {
                if (lowBloodVolume) {
                    if (isSpazmatism) {
                        VaultUtils.Text(CWRLocText.GetTextValue("Spazmatism_Text3"), TextColor1);
                    }
                    else {
                        VaultUtils.Text(CWRLocText.GetTextValue("Spazmatism_Text4"), TextColor2);
                    }

                    for (int i = 0; i < 13; i++) {
                        Item.NewItem(npc.GetSource_FromAI(), npc.Hitbox, ItemID.Heart);
                    }
                }
                else if (skeletronPrime?.ai[1] == 3) {
                    VaultUtils.Text(CWRLocText.GetTextValue("Spazmatism_Text5"), TextColor2);
                }
                else if (isSpawnFirstStageFromeExeunt) {
                    VaultUtils.Text(CWRLocText.GetTextValue("Spazmatism_Text6"), TextColor2);
                }
                else {
                    VaultUtils.Text(CWRLocText.GetTextValue("Spazmatism_Text7"), TextColor2);
                }
            }

            if (ai[6] > 120) {
                npc.active = false;
            }
            ai[6]++;
        }

        private void ExecuteDestroyerPhase(
            bool isSpazmatism,
            bool death,
            int projType,
            int projDamage,
            Vector2 toTarget,
            bool skeletronPrimeIsTwo
        ) {
            Projectile projectile = null;
            foreach (var p in Main.projectile) {
                if (!p.active) {
                    continue;
                }
                if (p.type == ModContent.ProjectileType<SetPosingStarm>()) {
                    projectile = p;
                }
            }

            if (projectile.Alives()) {
                ai[8]++;
            }

            if (ai[8] == Mechanicalworm.DontAttackTime + 10) {
                NetAISend();
            }

            if (ai[8] > Mechanicalworm.DontAttackTime + 10) {
                int fireTime = 10;
                Vector2 toPoint;

                if (projectile.Alives()) {
                    fireTime = death ? 5 : 8;
                    toTarget = npc.Center.To(projectile.Center);
                    float speedRot = death ? 0.02f : 0.03f;
                    toPoint = projectile.Center + (ai[4] * speedRot + MathHelper.TwoPi / 2 * (isSpazmatism ? 1 : 2)).ToRotationVector2() * 1060;
                }
                else {
                    toPoint = player.Center + (ai[4] * 0.04f + MathHelper.TwoPi / 2 * (isSpazmatism ? 1 : 2)).ToRotationVector2() * 760;
                }

                if (++ai[5] > fireTime && ai[4] > 30) {
                    if (!VaultUtils.isClient) {
                        float shootSpeed = CWRWorld.MachineRebellion ? 12 : 9;
                        Projectile.NewProjectile(
                            npc.GetSource_FromAI(),
                            npc.Center,
                            toTarget.UnitVector() * shootSpeed,
                            projType,
                            projDamage,
                            0
                        );
                    }
                    ai[5] = 0;
                    NetAISend();
                }

                ai[4]++;
                SetEyeValue(npc, player, toPoint, toTarget);
            }
        }

        private void ExecuteAccompanyAttackPhase(
            bool isSpazmatism,
            bool death,
            int projType,
            int projDamage,
            Vector2 toTarget,
            bool skeletronPrimeIsTwo,
            bool isDestroyer
        ) {
            if (isDestroyer && ai[8] < Mechanicalworm.DontAttackTime + 10) {
                npc.damage = 0;
                Vector2 toPoint = player.Center + new Vector2(isSpazmatism ? 600 : -600, -150);
                if (death) {
                    toPoint = player.Center + new Vector2(isSpazmatism ? 500 : -500, -150);
                }
                SetEyeValue(npc, player, toPoint, toTarget);
                return;
            }

            switch (ai[1]) {
                case 0:
                    ExecuteAccompanyAttackCase0(isSpazmatism, death, projType, projDamage, toTarget);
                    break;
                case 1:
                    ExecuteAccompanyAttackCase1(isSpazmatism, death, projType, projDamage, toTarget, skeletronPrimeIsTwo);
                    break;
            }
        }

        private void ExecuteAccompanyAttackCase0(
            bool isSpazmatism,
            bool death,
            int projType,
            int projDamage,
            Vector2 toTarget
        ) {
            Vector2 toPoint = player.Center + new Vector2(isSpazmatism ? 600 : -600, -650);
            if (death) {
                toPoint = player.Center + new Vector2(isSpazmatism ? 500 : -500, -650);
            }

            if (ai[2] == 30 && !VaultUtils.isClient) {
                float shootSpeed = death ? 8 : 6;
                if (CWRWorld.MachineRebellion) {
                    shootSpeed = 12;
                }
                for (int i = 0; i < 6; i++) {
                    Vector2 ver = (MathHelper.TwoPi / 6f * i).ToRotationVector2() * shootSpeed;
                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, ver, projType, projDamage, 0);
                }
            }

            if (ai[2] > 80) {
                ai[7] = 10;
                ai[1] = 1;
                ai[2] = 0;
                NetAISend();
            }

            ai[2]++;
            SetEyeValue(npc, player, toPoint, toTarget);
        }

        private void ExecuteAccompanyAttackCase1(
            bool isSpazmatism,
            bool death,
            int projType,
            int projDamage,
            Vector2 toTarget,
            bool skeletronPrimeIsTwo
        ) {
            Vector2 toPoint = player.Center + new Vector2(isSpazmatism ? 700 : -700, ai[9]);

            if (++ai[2] > 24) {
                if (!VaultUtils.isClient) {
                    if (skeletronPrimeIsTwo) {
                        for (int i = 0; i < 3; i++) {
                            Vector2 ver = toTarget.RotatedBy((-1 + i) * 0.06f).UnitVector() * 5;
                            Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, ver, projType, projDamage, 0);
                        }
                    }
                    else {
                        Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, toTarget.UnitVector() * 6, projType, projDamage, 0);
                    }
                }
                ai[3]++;
                ai[2] = 0;
                NetAISend();
            }

            if (ai[2] == 2) {
                if (skeletronPrimeIsTwo) {
                    if (ai[10] == 0) {
                        ai[10] = 1;
                    }
                    if (!VaultUtils.isClient) {
                        ai[9] = isSpazmatism ? -600 : 600;
                        ai[9] += Main.rand.Next(-120, 90);
                    }
                    ai[9] *= ai[10];
                    ai[10] *= -1;
                    NetAISend();
                }
                else {
                    if (!VaultUtils.isClient) {
                        ai[9] = Main.rand.Next(140, 280) * (Main.rand.NextBool() ? -1 : 1);
                    }
                    NetAISend();
                }
            }

            if (ai[3] > 6) {
                ai[3] = 0;
                ai[2] = 0;
                ai[1] = 0;
                ai[7] = 0;
                NetAISend();
            }
            else if (ai[7] < 2) {
                ai[7] = 2;
            }

            SetEyeValue(npc, player, toPoint, toTarget);
        }

        #endregion

        #region 其他覆写

        public override bool? CheckDead() {
            npc.dontTakeDamage = false;
            return true;
        }

        #endregion

        #region 绘制

        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (HeadPrimeAI.DontReform()) {
                return true;
            }

            //获取纹理
            Texture2D mainTexture = GetCurrentTexture();

            //绘制蓄力特效
            TwinsRenderHelper.DrawChargeEffect(spriteBatch, stateContext);

            //绘制本体
            TwinsRenderHelper.DrawNpcBody(
                spriteBatch,
                npc,
                mainTexture,
                stateContext.FrameIndex,
                npc.rotation
            );

            return false;
        }

        /// <summary>
        /// 获取当前纹理
        /// </summary>
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
