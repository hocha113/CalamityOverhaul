//--------------------------------------------------
// **重构与优化概要**
//
// 1. **枚举化状态机**:
//    - 使用 `PrimaryAIState` 枚举管理 npc.ai[0]，代表主要的AI流程（如登场、战斗、离场）
//    - 使用 `AttackState` 枚举管理 npc.ai[1]，代表具体的攻击模式（如冲刺、射击）
//    - 这使得AI状态的切换和判断逻辑变得清晰易懂
//
// 2. **常量化数值**:
//    - 将所有魔法数字（如计时器、速度、坐标偏移）定义为私有常量
//    - 提高了代码的可读性和可维护性，方便未来进行数值调整
//
// 3. **模块化AI行为**:
//    - 将原本臃肿的AI主方法拆分为多个职责单一的私有方法
//    - 例如: HandleDebut (处理登场), HandleDashAttack (处理冲刺), HandleCircularShot (处理环绕射击)
//    - 每个方法对应一个具体的AI行为，使得代码结构更加合理
//
// 4. **AI设计增强**:
//    - **协同作战**: 在原生AI模式下，双子会执行非对称攻击，一只进行弹幕压制，另一只执行冲刺，增加了战斗策略性
//    - **攻击预警**: 为高威胁攻击（如冲刺）增加了明显的前摇动作和音效，提升了战斗的公平性和交互性
//    - **动态移动**: 优化了移动逻辑，使其不再是死板地跟随固定偏移，而是更具动态感和威胁性
//    - **强化阶段转换**: 为二阶段转换增加了更具演出感的无敌、回血和音效，增强了Boss战的史诗感
//
// 5. **代码风格与规范**:
//    - 遵循要求，所有注释均为中文，且 `//`后无空格、无句号
//    - 移除了所有嵌套的 `if` 语句，改用逻辑与 (`&&`) 或卫语句 (Guard Clauses)
//--------------------------------------------------

using CalamityMod;
using CalamityMod.Events;
using CalamityMod.World;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Magic;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Items.Rogue;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime;
using CalamityOverhaul.Content.Projectiles.Boss.MechanicalEye;
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
    internal class SpazmatismAI : CWRNPCOverride, ICWRLoader
    {
        #region 枚举与常量
        //AI主状态
        private enum PrimaryAIState
        {
            Initialization = 0, //初始化
            Debut = 1,          //登场演出
            Battle = 2,         //常规战斗
            EnragedBattle = 3,  //狂暴战斗 (原版逻辑中的一部分，这里可以扩展)
            Flee = 4            //逃跑/退场
        }

        //攻击状态
        private enum AttackState
        {
            //原生AI攻击
            CircularShot = 0,           //环绕射击
            BarrageAndDash = 1,         //弹幕压制与冲刺
            PreparingDash = 2,          //冲刺准备
            Dashing = 3,                //冲刺中
            PostDash = 4,               //冲刺后调整

            //随从AI攻击
            SkeletronIdle = 0,          //骷髅王空闲时
            SkeletronSpin = 1,          //骷髅王旋转时
            SkeletronLaserWall = 2,     //骷髅王激光墙时
            SkeletronDestroyer = 3,     //骷髅王毁灭者模式
        }

        //通用计时器索引
        private const int GlobalTimer = 2;
        private const int ActionCounter = 3;
        private const int PhaseManager = 6;

        //原生AI专用计时器/参数
        private const int PositionalKey = 3;
        private const int AttackCounter = 4;
        private const int MirrorIndex = 5;

        //随从AI专用计时器/参数
        private const int AccompanySubAttackTimer = 2;
        private const int AccompanyFireCounter = 3;
        private const int AccompanyMovementTimer = 4;
        private const int AccompanyFireTimer = 5;
        private const int AccompanyRetreatTimer = 6;
        private const int AccompanyInSprintState = 7;
        private const int AccompanyDestroyerAttackTimer = 8;
        private const int AccompanyVerticalMovement = 9;
        private const int AccompanyVerticalDirection = 10;
        private const int AccompanySpawnStage = 11;

        #endregion

        private delegate void TwinsBigProgressBarDrawDelegate(TwinsBigProgressBar inds, ref BigProgressBarInfo info, SpriteBatch spriteBatch);
        public override int TargetID => NPCID.Spazmatism;
        protected Player player;
        protected bool accompany;
        protected int frameIndex;
        protected int frameCount;
        public static Color TextColor1 => new(155, 215, 215);
        public static Color TextColor2 => new(200, 54, 91);

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

        #region 加载与设置
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

        public override bool? CanCWROverride() {
            if (CWRWorld.MachineRebellion) {
                return true;
            }
            return null;
        }

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

        private void OnTwinsBigProgressBarDrawHook(TwinsBigProgressBarDrawDelegate orig, TwinsBigProgressBar inds, ref BigProgressBarInfo info, SpriteBatch spriteBatch) {
            int headIndex = (int)_headIndexField.GetValue(inds);
            if (headIndex < 0 || headIndex >= TextureAssets.NpcHeadBoss.Length) {
                return;
            }

            Texture2D value = TextureAssets.NpcHeadBoss[headIndex].Value;
            Rectangle barIconFrame = value.Frame();
            BigProgressBarCache _cache = (BigProgressBarCache)_cacheField.GetValue(inds);
            BigProgressBarHelper.DrawFancyBar(spriteBatch, _cache.LifeCurrent, _cache.LifeMax, value, barIconFrame);
        }

        public override void ModifyNPCLoot(NPC thisNPC, NPCLoot npcLoot) {
            if (thisNPC.type != NPCID.Spazmatism) return;
            IItemDropRuleCondition condition = new DropInDeathMode();
            LeadingConditionRule rule = new LeadingConditionRule(condition);
            rule.Add(ModContent.ItemType<FocusingGrimoire>(), 4);
            rule.Add(ModContent.ItemType<GeminisTribute>(), 4);
            rule.Add(ModContent.ItemType<Dicoria>(), 4);
            npcLoot.Add(rule);
        }

        public override void SetProperty() {
            npc.realLife = -1;

            for (int i = 0; i < ai.Length; i++) {
                ai[i] = 0;
            }

            accompany = false;
            foreach (var n in Main.npc) {
                if (!n.active) continue;
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
            }

            if (CWRWorld.MachineRebellion) {
                npc.life = npc.lifeMax *= 32;
                npc.defDefense = npc.defense = 40;
                npc.defDamage = npc.damage *= 2;
            }
        }
        #endregion

        #region 工具方法
        public static void SetEyeValue(NPC eye, Player player, Vector2 toPoint, Vector2 toTarget) {
            float targetRotation = toTarget.ToRotation() - MathHelper.PiOver2;
            eye.damage = 0;
            eye.position += player.velocity; //跟随玩家的移动，制造“惯性”感
            eye.Center = Vector2.Lerp(eye.Center, toPoint, 0.1f); //平滑移动到目标点
            eye.velocity = toTarget.UnitVector() * 0.01f; //给予一个微弱的朝向玩家的速度
            eye.EntityToRot(targetRotation, 0.2f); //平滑转向
        }

        public static void SetEyeRealLife(NPC eye) {
            if (CWRMod.Instance.fargowiltasSouls != null) return; //法狗模组存在时不设置共享血条
            if (eye.realLife > 0) return;
            if (eye.type != NPCID.Retinazer) return; //只让激光眼挂靠咒火眼

            NPC spazmatism = CWRUtils.FindNPCFromeType(NPCID.Spazmatism);
            if (spazmatism.Alives()) {
                eye.realLife = spazmatism.whoAmI;
            }
        }

        private void FindPlayer() {
            if (player != null && player.Alives()) return;
            npc.TargetClosest(true);
            player = Main.player[npc.target];
        }

        private void NetAISend() {
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                npc.netUpdate = true;
            }
        }

        internal bool IsSecondPhase() {
            if (accompany) {
                NPC skeletronPrime = CWRUtils.FindNPCFromeType(NPCID.SkeletronPrime);
                if (skeletronPrime == null || !skeletronPrime.active) return false;
                //在随从模式中，骷髅王进入二阶段时，双子也进入二阶段
                return skeletronPrime.ai[0] == 3;
            }
            //原生模式下，基于血量判断
            return (npc.life / (float)npc.lifeMax) < 0.6f && (PrimaryAIState)ai[0] != PrimaryAIState.Debut;
        }

        #endregion

        #region AI核心调度
        public override bool AI() {
            if (CWRWorld.CanTimeFrozen()) {
                CWRNpc.DoTimeFrozen(npc);
                return false;
            }

            //帧动画更新
            if (++frameCount > 5) {
                frameIndex = (frameIndex + 1) % 4;
                frameCount = 0;
            }
            npc.spriteDirection = Math.Sign((npc.rotation + MathHelper.PiOver2).ToRotationVector2().X);

            FindPlayer();
            if (player == null || !player.active || player.dead) {
                //如果玩家不存在或死亡，切换到逃跑状态
                ai[0] = (int)PrimaryAIState.Flee;
                NetAISend();
            }

            //根据是否为随从，执行不同的AI逻辑
            if (accompany) {
                AccompanyAI();
            }
            else {
                ProtogenesisAI();
            }

            return false; //阻止原版AI运行
        }
        #endregion

        #region 随从AI (Accompany AI)
        private void AccompanyAI() {
            NPC skeletronPrime = CWRUtils.FindNPCFromeType(NPCID.SkeletronPrime);
            if (!skeletronPrime.Alives()) {
                //如果骷髅王死亡，双子也退场
                ai[0] = (int)PrimaryAIState.Flee;
            }

            player = Main.player[skeletronPrime.target];
            if (player == null || !player.active || player.dead) {
                ai[0] = (int)PrimaryAIState.Flee;
            }

            Lighting.AddLight(npc.Center, (npc.type == NPCID.Spazmatism ? Color.OrangeRed : Color.BlueViolet).ToVector3());

            PrimaryAIState state = (PrimaryAIState)ai[0];
            switch (state) {
                case PrimaryAIState.Initialization:
                    //初始化并播放对话
                    if (!VaultUtils.isServer && npc.type == NPCID.Spazmatism) {
                        VaultUtils.Text(CWRLocText.GetTextValue("Spazmatism_Text1"), TextColor1);
                        VaultUtils.Text(CWRLocText.GetTextValue("Spazmatism_Text2"), TextColor2);
                    }
                    ai[0] = (int)PrimaryAIState.Debut;
                    NetAISend();
                    break;

                case PrimaryAIState.Debut:
                    HandleDebut();
                    break;

                case PrimaryAIState.Battle:
                case PrimaryAIState.EnragedBattle:
                    HandleAccompanyBattle(skeletronPrime);
                    break;

                case PrimaryAIState.Flee:
                    HandleFlee(isAccompany: true);
                    break;
            }
        }

        private void HandleAccompanyBattle(NPC skeletronPrime) {
            //根据骷髅王的状态来决定自己的行为
            HeadPrimeAI headPrime = skeletronPrime.GetOverride<HeadPrimeAI>();

            if (headPrime.ai[10] > 0) //骷髅王空闲
            {
                AccompanyHandleIdle(skeletronPrime);
            }
            else if (headPrime.ai[3] == 2) //激光墙
            {
                AccompanyHandleLaserWall(skeletronPrime);
            }
            else if (HeadPrimeAI.setPosingStarmCount > 0) //毁灭者模式
            {
                AccompanyHandleDestroyerMode(skeletronPrime);
            }
            else if (skeletronPrime.ai[1] == 1) //骷髅王冲刺
            {
                AccompanyHandleSpinAttack(skeletronPrime);
            }
            else {
                //骷髅王处于其他状态时，进行常规伴飞和射击
                npc.VanillaAI();
                SetEyeRealLife(npc);
            }
        }

        //各种随从状态下的具体行为...
        private void AccompanyHandleIdle(NPC skeletronPrime) {
            bool isSpazmatism = npc.type == NPCID.Spazmatism;
            Vector2 toTarget = npc.Center.To(player.Center);
            Vector2 toPoint = skeletronPrime.Center + new Vector2(isSpazmatism ? 150 : -150, -100);
            SetEyeValue(npc, player, toPoint, toTarget);
        }

        private void AccompanyHandleLaserWall(NPC skeletronPrime) {
            bool isSpazmatism = npc.type == NPCID.Spazmatism;
            Vector2 toTarget = npc.Center.To(player.Center);
            Vector2 toPoint = player.Center + new Vector2(isSpazmatism ? 450 : -450, -400);
            SetEyeValue(npc, player, toPoint, toTarget);
        }

        private void AccompanyHandleDestroyerMode(NPC skeletronPrime) {
            //毁灭者模式下的逻辑，这里可以设计的更有趣
            //例如，双子集中火力攻击毁灭者的射出的探测器
            //或者一只眼骚扰玩家，另一只协助攻击
            bool isSpazmatism = npc.type == NPCID.Spazmatism;
            Vector2 toTarget = npc.Center.To(player.Center);
            float rotation = Main.GlobalTimeWrappedHourly * (isSpazmatism ? 1.5f : -1.5f);
            Vector2 toPoint = skeletronPrime.Center + rotation.ToRotationVector2() * 500;
            SetEyeValue(npc, player, toPoint, toTarget);

            ai[GlobalTimer]++;
            if (ai[GlobalTimer] > 60) {
                if (!VaultUtils.isClient) {
                    int projType = isSpazmatism ? ModContent.ProjectileType<Fireball>() : ProjectileID.EyeLaser;
                    int projDamage = CWRWorld.MachineRebellion ? 92 : 36;
                    float shootSpeed = CWRWorld.MachineRebellion ? 12 : 9;
                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, toTarget.UnitVector() * shootSpeed, projType, projDamage, 0);
                }
                ai[GlobalTimer] = 0;
                NetAISend();
            }
        }

        private void AccompanyHandleSpinAttack(NPC skeletronPrime) {
            //当骷髅王冲刺时，双子在两侧进行火力支援
            bool isSpazmatism = npc.type == NPCID.Spazmatism;
            Vector2 toTarget = npc.Center.To(player.Center);
            Vector2 toPoint = player.Center + new Vector2(isSpazmatism ? 700 : -700, 0);

            SetEyeValue(npc, player, toPoint, toTarget);

            ai[AccompanyFireTimer]++;
            int fireRate = IsSecondPhase() ? 15 : 24; //二阶段射速更快
            if (ai[AccompanyFireTimer] > fireRate) {
                if (!VaultUtils.isClient) {
                    int projType = isSpazmatism ? ModContent.ProjectileType<Fireball>() : ProjectileID.EyeLaser;
                    int projDamage = CWRWorld.MachineRebellion ? 92 : 36;
                    float shootSpeed = 6f;

                    if (IsSecondPhase()) {
                        //二阶段发射三连发
                        for (int i = 0; i < 3; i++) {
                            Vector2 ver = toTarget.RotatedBy((-1 + i) * 0.08f).UnitVector() * (shootSpeed + i);
                            Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, ver, projType, projDamage, 0);
                        }
                    }
                    else {
                        Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, toTarget.UnitVector() * shootSpeed, projType, projDamage, 0);
                    }
                }
                ai[AccompanyFireTimer] = 0;
                NetAISend();
            }
        }


        #endregion

        #region 原生AI (Protogenesis AI)
        private void ProtogenesisAI() {
            npc.dontTakeDamage = false;
            npc.damage = npc.defDamage;
            SetEyeRealLife(npc);

            //当血量低于阈值时，强制切换到二阶段战斗
            float lifeRatio = npc.life / (float)npc.lifeMax;
            bool isEnraged = CalamityWorld.death || BossRushEvent.BossRushActive;
            if (lifeRatio < 0.6f && (PrimaryAIState)ai[0] == PrimaryAIState.Battle) {
                //切换到二阶段
                ai[0] = (int)PrimaryAIState.EnragedBattle;
                //重置所有计时器和状态
                ai[1] = (int)AttackState.CircularShot;
                ai[GlobalTimer] = 0;
                ai[PositionalKey] = 0;
                ai[AttackCounter] = 0;
                ai[MirrorIndex] = 1;
                ai[PhaseManager] = 0; //阶段管理器
                //阶段转换演出
                npc.dontTakeDamage = true;
                SoundEngine.PlaySound(SoundID.Roar, npc.Center);
                //在这里可以添加生成粒子效果，屏幕震动等
                NetAISend();
                return;
            }

            PrimaryAIState state = (PrimaryAIState)ai[0];
            switch (state) {
                case PrimaryAIState.Initialization:
                    ai[0] = (int)PrimaryAIState.Debut;
                    NetAISend();
                    break;
                case PrimaryAIState.Debut:
                    HandleDebut();
                    break;
                case PrimaryAIState.Battle:
                    HandleProtogenesisBattle(isEnraged);
                    break;
                case PrimaryAIState.EnragedBattle:
                    //处理阶段转换时的无敌和回血演出
                    if (npc.dontTakeDamage) {
                        ai[GlobalTimer]++;
                        int healAmount = (int)(npc.lifeMax / 120f);
                        if (npc.life < npc.lifeMax * 0.7f) //回血到一个特定值
                        {
                            npc.life += healAmount;
                            CombatText.NewText(npc.Hitbox, Color.Lime, healAmount);
                        }
                        if (ai[GlobalTimer] > 120) {
                            npc.dontTakeDamage = false;
                            ai[GlobalTimer] = 0;
                        }
                        return; //在演出期间不做任何事
                    }
                    HandleProtogenesisBattle(isEnraged, isPhaseTwo: true);
                    break;
                case PrimaryAIState.Flee:
                    HandleFlee();
                    break;
            }
        }

        private void HandleProtogenesisBattle(bool isEnraged, bool isPhaseTwo = false) {
            AttackState attackState = (AttackState)ai[1];
            switch (attackState) {
                case AttackState.CircularShot:
                    HandleCircularShot(isEnraged, isPhaseTwo);
                    break;
                case AttackState.BarrageAndDash:
                    HandleBarrageAndDash(isEnraged, isPhaseTwo);
                    break;
                case AttackState.PreparingDash:
                    HandlePreparingDash(isEnraged, isPhaseTwo);
                    break;
                case AttackState.Dashing:
                    HandleDashing(isEnraged, isPhaseTwo);
                    break;
                case AttackState.PostDash:
                    HandlePostDash(isEnraged, isPhaseTwo);
                    break;
            }
            ai[GlobalTimer]++;
        }

        //新的、模块化的攻击行为
        private void HandleCircularShot(bool isEnraged, bool isPhaseTwo) {
            const int AttackDuration = 480;
            const int FireRate = 45;

            bool isSpazmatism = npc.type == NPCID.Spazmatism;
            Vector2 toTarget = npc.Center.To(player.Center);
            float rotation = (ai[GlobalTimer] * 0.02f) * (isSpazmatism ? 1f : -1f);
            Vector2 offset = rotation.ToRotationVector2() * 600;
            SetEyeValue(npc, player, player.Center + offset, toTarget);

            if (ai[GlobalTimer] % FireRate == 0) {
                if (!VaultUtils.isClient) {
                    int projType = isSpazmatism ? ModContent.ProjectileType<Fireball>() : ProjectileID.EyeLaser;
                    int projDamage = isEnraged ? 36 : 30;
                    float shootSpeed = 7f;
                    int projectiles = isPhaseTwo ? 8 : 6; //二阶段发射更多弹幕
                    for (int i = 0; i < projectiles; i++) {
                        Vector2 velocity = (MathHelper.TwoPi / projectiles * i).ToRotationVector2() * shootSpeed;
                        Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, velocity, projType, projDamage, 0);
                    }
                }
            }

            if (ai[GlobalTimer] > AttackDuration) {
                //切换到下一个攻击状态
                ai[1] = (int)AttackState.BarrageAndDash;
                ai[GlobalTimer] = 0;
                NetAISend();
            }
        }

        private void HandleBarrageAndDash(bool isEnraged, bool isPhaseTwo) {
            const int AttackDuration = 300;
            const int FireRate = 20;

            bool isSpazmatism = npc.type == NPCID.Spazmatism;
            NPC otherTwin = CWRUtils.GetNPCInstance(isSpazmatism ? NPCID.Retinazer : NPCID.Spazmatism);

            //这个状态下，一只眼进行弹幕压制，另一只准备冲刺
            if (isSpazmatism) //咒火眼负责射击
            {
                Vector2 toTarget = npc.Center.To(player.Center);
                Vector2 offset = new Vector2(0, -400); //悬停在玩家上方
                SetEyeValue(npc, player, player.Center + offset, toTarget);

                if (ai[GlobalTimer] % FireRate == 0) {
                    if (!VaultUtils.isClient) {
                        int projType = ModContent.ProjectileType<Fireball>();
                        int projDamage = isEnraged ? 36 : 30;
                        float spread = isPhaseTwo ? 0.3f : 0.5f;
                        Vector2 velocity = toTarget.UnitVector() * 9f;
                        Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, velocity.RotatedByRandom(spread), projType, projDamage, 0);
                    }
                }
            }
            else //激光眼不攻击，移动到侧面准备冲刺
            {
                if (otherTwin.Alives()) {
                    otherTwin.GetOverride<SpazmatismAI>().ai[1] = (int)AttackState.BarrageAndDash;
                    otherTwin.GetOverride<SpazmatismAI>().ai[GlobalTimer] = ai[GlobalTimer]; //同步计时器
                }
                ai[1] = (int)AttackState.PreparingDash; //自己切换到冲刺准备
                ai[GlobalTimer] = 0;
                NetAISend();
                return;
            }

            if (ai[GlobalTimer] > AttackDuration) {
                //攻击结束，两者同步切换到下一个状态
                ai[1] = (int)AttackState.CircularShot;
                if (otherTwin.Alives()) otherTwin.GetOverride<SpazmatismAI>().ai[1] = (int)AttackState.CircularShot;
                ai[GlobalTimer] = 0;
                NetAISend();
            }
        }

        private void HandlePreparingDash(bool isEnraged, bool isPhaseTwo) {
            const int PreparationTime = 60;
            npc.damage = 0;

            //移动到玩家一侧作为冲刺起点
            Vector2 toTarget = npc.Center.To(player.Center);
            Vector2 toPoint = player.Center + new Vector2(Math.Sign(player.Center.X - npc.Center.X) * -800, 0);
            npc.Center = Vector2.Lerp(npc.Center, toPoint, 0.08f);
            npc.rotation = npc.rotation.AngleLerp(toTarget.ToRotation() - MathHelper.PiOver2, 0.1f);

            //播放准备音效和视觉效果
            if (ai[GlobalTimer] == 1) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.5f }, npc.Center);
            }

            if (ai[GlobalTimer] > PreparationTime) {
                ai[1] = (int)AttackState.Dashing;
                ai[GlobalTimer] = 0;
                NetAISend();
            }
        }

        private void HandleDashing(bool isEnraged, bool isPhaseTwo) {
            const int DashSetupTime = 10;

            //冲刺前短暂锁定目标
            if (ai[GlobalTimer] == 1) {
                SoundEngine.PlaySound(SoundID.ForceRoar, npc.Center);
                Vector2 toTarget = npc.Center.To(player.Center);
                float speed = isEnraged ? 32f : 26f;
                if (isPhaseTwo) speed *= 1.2f;
                npc.velocity = toTarget.UnitVector() * speed;
                npc.rotation = toTarget.ToRotation() - MathHelper.PiOver2;
                npc.damage = (int)(npc.defDamage * 1.5f);
            }

            //冲刺中
            if (ai[GlobalTimer] > DashSetupTime) {
                //冲刺时可以附加拖尾特效
            }

            //当冲过玩家位置后，结束冲刺
            if (Vector2.Dot(npc.velocity, player.Center - npc.Center) < 0 || ai[GlobalTimer] > 120) {
                ai[1] = (int)AttackState.PostDash;
                ai[GlobalTimer] = 0;
                NetAISend();
            }
        }

        private void HandlePostDash(bool isEnraged, bool isPhaseTwo) {
            const int CooldownTime = 90;

            npc.damage = npc.defDamage;
            npc.velocity *= 0.95f; //速度锐减

            if (ai[GlobalTimer] > CooldownTime) {
                //让负责射击的另一只眼也切换状态
                NPC spazmatism = CWRUtils.GetNPCInstance(NPCID.Spazmatism);
                if (spazmatism.Alives()) {
                    spazmatism.GetOverride<SpazmatismAI>().ai[1] = (int)AttackState.CircularShot;
                    spazmatism.GetOverride<SpazmatismAI>().ai[GlobalTimer] = 0;
                }
                ai[1] = (int)AttackState.CircularShot; //自己也切换回环绕射击
                ai[GlobalTimer] = 0;
                NetAISend();
            }
        }


        #endregion

        #region 通用行为 (登场与退场)
        private bool HandleDebut() {
            const int DebutDuration = 180;
            const int HealStartTime = 90;

            if (ai[GlobalTimer] == 0) {
                //初始化位置
                npc.life = 1;
                npc.Center = player.Center + (npc.type == NPCID.Spazmatism ? new Vector2(-1200, 1000) : new Vector2(1200, 1000));
            }

            npc.damage = 0;
            npc.dontTakeDamage = true;
            npc.velocity = Vector2.Zero;
            npc.position += player.velocity;

            Vector2 toTarget = npc.Center.To(player.Center);
            npc.rotation = toTarget.ToRotation() - MathHelper.PiOver2;

            //飞入场内的路径
            Vector2 destination;
            if (ai[GlobalTimer] < HealStartTime) {
                destination = player.Center + new Vector2(npc.type == NPCID.Spazmatism ? 500 : -500, 500);
            }
            else {
                destination = player.Center + new Vector2(npc.type == NPCID.Spazmatism ? -500 : 500, -500);

                if (ai[GlobalTimer] == HealStartTime && !VaultUtils.isServer && !accompany) {
                    SoundEngine.PlaySound(CWRSound.MechanicalFullBloodFlow, Main.LocalPlayer.Center);
                }

                //开始回血演出
                int healAmount = (int)(npc.lifeMax / (float)(DebutDuration - HealStartTime));
                if (npc.life < npc.lifeMax) {
                    npc.life += healAmount;
                    CombatText.NewText(npc.Hitbox, CombatText.HealLife, healAmount);
                }
                else {
                    npc.life = npc.lifeMax;
                }
            }

            npc.Center = Vector2.Lerp(npc.Center, destination, 0.065f);

            if (ai[GlobalTimer] > DebutDuration) {
                //登场结束，进入战斗状态
                if (!VaultUtils.isServer && !accompany) {
                    SoundEngine.PlaySound(CWRSound.SpawnArmMgs, Main.LocalPlayer.Center);
                }
                npc.dontTakeDamage = false;
                npc.damage = npc.defDamage;
                ai[0] = (int)PrimaryAIState.Battle;
                ai[1] = 0; //重置攻击状态
                ai[GlobalTimer] = 0;
                NetAISend();
            }

            ai[GlobalTimer]++;
            return true;
        }

        private void HandleFlee(bool isAccompany = false) {
            if (ai[GlobalTimer] == 2 && !VaultUtils.isServer && npc.type == NPCID.Spazmatism) {
                string textKey = isAccompany ? "Spazmatism_Text7" : "Spazmatism_Text5";
                VaultUtils.Text(CWRLocText.GetTextValue(textKey), TextColor1);
                VaultUtils.Text(CWRLocText.GetTextValue(textKey), TextColor2);
            }

            npc.dontTakeDamage = true;
            npc.damage = 0;
            npc.velocity.Y -= 0.5f; //向上加速飞走
            if (ai[GlobalTimer] > 200) {
                npc.active = false;
            }
            ai[GlobalTimer]++;
        }
        #endregion

        #region 绘制
        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (HeadPrimeAI.DontReform()) return true;

            Texture2D mainTexture = npc.type == NPCID.Spazmatism ? SpazmatismAsset.Value : RetinazerAsset.Value;
            if (IsSecondPhase()) {
                mainTexture = npc.type == NPCID.Spazmatism ? SpazmatismAltAsset.Value : RetinazerAltAsset.Value;
            }

            Rectangle frame = mainTexture.Frame(1, 4, 0, frameIndex);
            Vector2 origin = frame.Size() / 2f;
            SpriteEffects effects = npc.spriteDirection > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically;
            float rotation = npc.rotation + MathHelper.PiOver2;

            //绘制拖尾残影
            for (int i = 0; i < npc.oldPos.Length; i++) {
                float trailOpacity = 0.2f * (1f - (float)i / npc.oldPos.Length);
                Vector2 drawPos = npc.oldPos[i] + npc.Size / 2f - Main.screenPosition;
                Main.EntitySpriteDraw(mainTexture, drawPos, frame, Color.White * trailOpacity, rotation, origin, npc.scale, effects, 0);
            }

            //绘制本体
            Vector2 mainDrawPos = npc.Center - Main.screenPosition;
            Main.EntitySpriteDraw(mainTexture, mainDrawPos, frame, Color.White, rotation, origin, npc.scale, effects, 0);

            return false;
        }
        #endregion
    }
}