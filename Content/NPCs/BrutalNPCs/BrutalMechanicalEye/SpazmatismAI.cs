using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Magic;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Items.Rogue;
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
    internal class SpazmatismAI : CWRNPCOverride, ICWRLoader
    {
        #region 枚举与常量
        //AI主状态
        private enum PrimaryAIState
        {
            Initialization = 0, //初始化
            Debut = 1,          //登场演出
            Battle = 2,         //常规战斗
            EnragedBattle = 3,  //狂暴战斗
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
            rule.SimpleAdd(ModContent.ItemType<FocusingGrimoire>(), 4);
            rule.SimpleAdd(ModContent.ItemType<GeminisTribute>(), 4);
            rule.SimpleAdd(ModContent.ItemType<Dicoria>(), 4);
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
                npc.life = npc.lifeMax = (int)(npc.lifeMax * 0.8f);//降低一点血量
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

        private void FindPlayer() {
            if (player != null && player.Alives()) return;
            npc.TargetClosest(true);
            player = Main.player[npc.target];
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

            bool reset;
            //根据是否为随从，执行不同的AI逻辑
            if (accompany) {
                reset = AccompanyAI();
            }
            else {
                reset = ProtogenesisAI();
            }

            return reset;//阻止原版AI运行
        }
        #endregion

        #region 随从AI (Accompany AI)
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
                if (AccompanyDebut()) {
                    return false;
                }
            }

            if (IsSecondPhase()) {
                npc.HitSound = SoundID.NPCHit4;
            }

            if (skeletronPrimeIsDead || skeletronPrime?.ai[1] == 3 || lowBloodVolume || isSpawnFirstStageFromeExeunt) {
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
                    if (projectile.Alives()) {
                        fireTime = death ? 5 : 8;
                        toTarget = npc.Center.To(projectile.Center);
                        float speedRot = death ? 0.02f : 0.03f;
                        int modelong = death ? 1060 : 1160;
                        toPoint = projectile.Center + (ai[4] * speedRot + MathHelper.TwoPi / 2 * (isSpazmatism ? 1 : 2)).ToRotationVector2() * 1060;
                    }
                    else {
                        toPoint = player.Center + (ai[4] * 0.04f + MathHelper.TwoPi / 2 * (isSpazmatism ? 1 : 2)).ToRotationVector2() * 760;
                    }

                    if (++ai[5] > fireTime && ai[4] > 30) {//这里需要停一下，不要立即开火
                        if (!VaultUtils.isClient) {
                            float shootSpeed = 9;
                            if (CWRWorld.MachineRebellion) {
                                shootSpeed = 12;
                            }
                            Projectile.NewProjectile(npc.GetSource_FromAI()
                                , npc.Center, toTarget.UnitVector() * shootSpeed, projType, projDamage, 0);
                        }
                        ai[5] = 0;
                        NetAISend();
                    }
                    ai[4]++;
                    SetEyeValue(npc, player, toPoint, toTarget);
                    return false;
                }
            }
            else if (ai[8] != 0) {
                ai[8] = 0;
                NetAISend();
            }

            if (skeletronPrimeInSprint || ai[7] > 0) {
                if (isDestroyer && ai[8] < Mechanicalworm.DontAttackTime + 10) {
                    npc.damage = 0;
                    toPoint = player.Center + new Vector2(isSpazmatism ? 600 : -600, -150);
                    if (death) {
                        toPoint = player.Center + new Vector2(isSpazmatism ? 500 : -500, -150);
                    }
                    SetEyeValue(npc, player, toPoint, toTarget);
                    return false;
                }

                switch (ai[1]) {
                    case 0:
                        toPoint = player.Center + new Vector2(isSpazmatism ? 600 : -600, -650);
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
                        break;
                    case 1:
                        toPoint = player.Center + new Vector2(isSpazmatism ? 700 : -700, ai[9]);
                        if (++ai[2] > 24) {//一阶段两侧激光发射频率，数字越大频率越慢
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
                            if (skeletronPrimeIsTwo) {//二阶段上下大幅度飞舞
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
                        break;
                }

                SetEyeValue(npc, player, toPoint, toTarget);
                return false;
            }

            if (ai[7] > 0) {
                ai[7]--;
            }

            npc.VanillaAI();
            return false;
        }

        private bool AccompanyDebut() {
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

        #region 原生AI (Protogenesis AI)
        private bool ProtogenesisAI() {
            //npc.dontTakeDamage = false;
            //npc.damage = npc.defDamage;

            ////当血量低于阈值时，强制切换到二阶段战斗
            //float lifeRatio = npc.life / (float)npc.lifeMax;
            //bool isEnraged = CalamityWorld.death || BossRushEvent.BossRushActive;
            //if (lifeRatio < 0.6f && (PrimaryAIState)ai[0] == PrimaryAIState.Battle) {
            //   //切换到二阶段
            //   ai[0] = (int)PrimaryAIState.EnragedBattle;
            //   //重置所有计时器和状态
            //   ai[1] = (int)AttackState.CircularShot;
            //   ai[GlobalTimer] = 0;
            //   ai[PositionalKey] = 0;
            //   ai[AttackCounter] = 0;
            //   ai[MirrorIndex] = 1;
            //   ai[PhaseManager] = 0; //阶段管理器
            //   //阶段转换演出
            //   npc.dontTakeDamage = true;
            //   SoundEngine.PlaySound(SoundID.Roar, npc.Center);
            //   NetAISend();
            //   return false;
            //}

            //PrimaryAIState state = (PrimaryAIState)ai[0];
            //switch (state) {
            //   case PrimaryAIState.Initialization:
            //       ai[0] = (int)PrimaryAIState.Debut;
            //       NetAISend();
            //       break;
            //   case PrimaryAIState.Debut:
            //       HandleDebut();
            //       break;
            //   case PrimaryAIState.Battle:
            //       HandleProtogenesisBattle(isEnraged, CWRWorld.Death);
            //       break;
            //   case PrimaryAIState.EnragedBattle:
            //       //处理阶段转换时的无敌和回血演出
            //       if (npc.dontTakeDamage) {
            //           ai[GlobalTimer]++;
            //           int healAmount = (int)(npc.lifeMax / 120f);
            //           if (npc.life < npc.lifeMax * 0.7f) //回血到一个特定值
            //           {
            //               npc.life += healAmount;
            //               CombatText.NewText(npc.Hitbox, Color.Lime, healAmount);
            //           }
            //           if (ai[GlobalTimer] > 120) {
            //               npc.dontTakeDamage = false;
            //               ai[GlobalTimer] = 0;
            //           }
            //           return false; //在演出期间不做任何事
            //       }
            //       return true;
            //   case PrimaryAIState.Flee:
            //       HandleFlee();
            //       break;
            //}

            return true;
        }

        private void HandleProtogenesisBattle(bool isEnraged, bool death = false) {
            AttackState attackState = (AttackState)ai[1];
            switch (attackState) {
                case AttackState.CircularShot:
                    HandleCircularShot(isEnraged, death);
                    break;
                case AttackState.BarrageAndDash:
                    HandleBarrageAndDash(isEnraged, death);
                    break;
                case AttackState.PreparingDash:
                    HandlePreparingDash(isEnraged, death);
                    break;
                case AttackState.Dashing:
                    HandleDashing(isEnraged, death);
                    break;
                case AttackState.PostDash:
                    HandlePostDash(isEnraged, death);
                    break;
            }
            ai[GlobalTimer]++;
        }

        //新的、模块化的攻击行为
        private void HandleCircularShot(bool isEnraged, bool death) {
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
                    int projectiles = death ? 8 : 6; //二阶段发射更多弹幕
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

        private void HandleBarrageAndDash(bool isEnraged, bool death) {
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
                        float spread = death ? 0.3f : 0.5f;
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

        private void HandlePreparingDash(bool isEnraged, bool death) {
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

        private void HandleDashing(bool isEnraged, bool death) {
            const int DashSetupTime = 10;

            //冲刺前短暂锁定目标
            if (ai[GlobalTimer] == 1) {
                SoundEngine.PlaySound(SoundID.ForceRoar, npc.Center);
                Vector2 toTarget = npc.Center.To(player.Center);
                float speed = isEnraged ? 32f : 26f;
                if (death) speed *= 1.2f;
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

        private void HandlePostDash(bool isEnraged, bool death) {
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