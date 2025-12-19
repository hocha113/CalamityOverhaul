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
            //检查玩家状态
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
                if (!AccompanyDebut()) {
                    ai[0] = (int)PrimaryAIState.Battle;
                    ai[1] = 0;
                    ai[2] = 0;
                    ai[3] = 0;
                }
                return false;
            }

            //二阶段检测与转换
            bool secondPhase = IsSecondPhase();
            if (secondPhase && ai[0] != (int)PrimaryAIState.EnragedBattle) {
                ai[0] = (int)PrimaryAIState.EnragedBattle;
                ai[1] = 0;
                ai[2] = 0;
                ai[3] = 0;
                //转换时的特效或音效
                SoundEngine.PlaySound(SoundID.Roar, npc.Center);
                //清除所有负面buff
                for (int i = 0; i < npc.buffType.Length; i++) {
                    npc.buffTime[i] = 0;
                }
            }

            //根据类型分发逻辑
            if (npc.type == NPCID.Spazmatism) {
                if (secondPhase) SpazmatismAI_Phase2();
                else SpazmatismAI_Phase1();
            }
            else {
                if (secondPhase) RetinazerAI_Phase2();
                else RetinazerAI_Phase1();
            }

            return false;
        }

        private void SpazmatismAI_Phase1() {
            //魔焰眼一阶段逻辑
            //ai[1]:子状态 0=悬停射击 1=冲刺准备 2=冲刺
            //ai[2]:计时器
            //ai[3]:计数器

            int shootRate = 60;
            int dashCountMax = 3;
            float moveSpeed = 14f;
            float dashSpeed = 28f;

            if (CWRWorld.MachineRebellion) {
                shootRate = 45;
                moveSpeed = 18f;
                dashSpeed = 35f;
            }

            switch ((int)ai[1]) {
                case 0: //悬停射击
                    Vector2 hoverTarget = player.Center + new Vector2(npc.Center.X < player.Center.X ? -400 : 400, -200);
                    MoveTo(hoverTarget, moveSpeed, 0.05f);
                    npc.rotation = (player.Center - npc.Center).ToRotation() - MathHelper.PiOver2;

                    ai[2]++;
                    if (ai[2] >= shootRate) {
                        if (!VaultUtils.isClient) {
                            Vector2 shootVel = (player.Center - npc.Center).UnitVector() * 12f;
                            Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, shootVel, ModContent.ProjectileType<Fireball>(), 30, 0f, Main.myPlayer);
                        }
                        SoundEngine.PlaySound(SoundID.Item34, npc.Center);
                        ai[2] = 0;
                        ai[3]++;
                    }

                    if (ai[3] >= 6) { //射击6次后进入冲刺
                        ai[1] = 1;
                        ai[2] = 0;
                        ai[3] = 0;
                    }
                    break;

                case 1: //冲刺准备
                    npc.velocity *= 0.9f;
                    npc.rotation = (player.Center - npc.Center).ToRotation() - MathHelper.PiOver2;
                    ai[2]++;
                    if (ai[2] >= 30) {
                        SoundEngine.PlaySound(SoundID.Roar, npc.Center);
                        ai[1] = 2;
                        ai[2] = 0;
                        npc.velocity = (player.Center - npc.Center).UnitVector() * dashSpeed;
                    }
                    break;

                case 2: //冲刺
                    npc.rotation = npc.velocity.ToRotation() - MathHelper.PiOver2;
                    ai[2]++;
                    if (ai[2] >= 40) { //冲刺持续时间
                        npc.velocity *= 0.5f;
                        ai[3]++;
                        if (ai[3] >= dashCountMax) {
                            ai[1] = 0; //回到悬停
                            ai[3] = 0;
                        }
                        else {
                            ai[1] = 1; //继续冲刺
                        }
                        ai[2] = 0;
                    }
                    break;
            }
        }

        private void SpazmatismAI_Phase2() {
            //魔焰眼二阶段逻辑
            //ai[1]:子状态 0=喷火追击 1=连续冲刺
            
            float chaseSpeed = 8f;
            float turnSpeed = 0.15f;
            int flameDuration = 360;
            int dashCountMax = 5;

            if (CWRWorld.MachineRebellion) {
                chaseSpeed = 12f;
                turnSpeed = 0.25f;
                flameDuration = 420;
                dashCountMax = 7;
            }

            switch ((int)ai[1]) {
                case 0: //喷火追击
                    Vector2 targetDir = (player.Center - npc.Center).UnitVector();
                    npc.velocity = Vector2.Lerp(npc.velocity, targetDir * chaseSpeed, turnSpeed);
                    npc.rotation = npc.velocity.ToRotation() - MathHelper.PiOver2;

                    ai[2]++;
                    if (ai[2] % 5 == 0) { //高频率喷火
                        if (!VaultUtils.isClient) {
                            Vector2 fireVel = npc.velocity.UnitVector() * 14f;
                            Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, fireVel, ProjectileID.EyeFire, 35, 0f, Main.myPlayer);
                        }
                        SoundEngine.PlaySound(SoundID.Item34, npc.Center);
                    }

                    if (ai[2] >= flameDuration) {
                        ai[1] = 1;
                        ai[2] = 0;
                        ai[3] = 0;
                    }
                    break;

                case 1: //连续冲刺
                    if (ai[2] == 0) { //准备
                        npc.velocity *= 0.9f;
                        npc.rotation = (player.Center - npc.Center).ToRotation() - MathHelper.PiOver2;
                    }
                    
                    ai[2]++;
                    if (ai[2] == 20) { //冲刺开始
                        SoundEngine.PlaySound(SoundID.Roar, npc.Center);
                        npc.velocity = (player.Center - npc.Center).UnitVector() * 40f;
                    }

                    if (ai[2] > 20) {
                        npc.rotation = npc.velocity.ToRotation() - MathHelper.PiOver2;
                    }

                    if (ai[2] >= 50) { //单次冲刺结束
                        npc.velocity *= 0.4f;
                        ai[2] = 0;
                        ai[3]++;
                        if (ai[3] >= dashCountMax) {
                            ai[1] = 0; //回到喷火
                            ai[3] = 0;
                        }
                    }
                    break;
            }
        }

        private void RetinazerAI_Phase1() {
            //激光眼一阶段逻辑
            //ai[1]:子状态 0=悬停射击 1=调整位置
            
            int shootRate = 50;
            float moveSpeed = 12f;

            if (CWRWorld.MachineRebellion) {
                shootRate = 35;
                moveSpeed = 16f;
            }

            switch ((int)ai[1]) {
                case 0: //悬停射击
                    Vector2 hoverTarget = player.Center + new Vector2(0, -350);
                    MoveTo(hoverTarget, moveSpeed, 0.08f);
                    npc.rotation = (player.Center - npc.Center).ToRotation() - MathHelper.PiOver2;

                    ai[2]++;
                    if (ai[2] >= shootRate) {
                        if (!VaultUtils.isClient) {
                            Vector2 shootVel = (player.Center - npc.Center).UnitVector() * 10f;
                            Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, shootVel, ProjectileID.DeathLaser, 25, 0f, Main.myPlayer);
                        }
                        SoundEngine.PlaySound(SoundID.Item33, npc.Center);
                        ai[2] = 0;
                        ai[3]++;
                    }

                    if (ai[3] >= 8) { //射击8次后调整位置
                        ai[1] = 1;
                        ai[2] = 0;
                        ai[3] = 0;
                    }
                    break;

                case 1: //调整位置
                    Vector2 reposition = player.Center + Main.rand.NextVector2CircularEdge(400, 400);
                    MoveTo(reposition, moveSpeed * 1.5f, 0.1f);
                    npc.rotation = (player.Center - npc.Center).ToRotation() - MathHelper.PiOver2;
                    
                    ai[2]++;
                    if (ai[2] >= 60 || Vector2.Distance(npc.Center, reposition) < 50) {
                        ai[1] = 0;
                        ai[2] = 0;
                    }
                    break;
            }
        }

        private void RetinazerAI_Phase2() {
            //激光眼二阶段逻辑
            //ai[1]:子状态 0=垂直弹幕 1=水平弹幕 2=精准狙击
            int rapidFireRate = 15;

            if (CWRWorld.MachineRebellion) {
                rapidFireRate = 10;
            }

            switch ((int)ai[1]) {
                case 0: //垂直弹幕
                    Vector2 targetPos = player.Center + new Vector2(npc.Center.X < player.Center.X ? -400 : 400, 0);
                    //保持Y轴对齐
                    float yDiff = player.Center.Y - npc.Center.Y;
                    npc.velocity.Y = MathHelper.Lerp(npc.velocity.Y, yDiff * 0.1f, 0.1f);
                    npc.velocity.X = MathHelper.Lerp(npc.velocity.X, (targetPos.X - npc.Center.X) * 0.05f, 0.1f);
                    
                    npc.rotation = (player.Center - npc.Center).ToRotation() - MathHelper.PiOver2;

                    ai[2]++;
                    if (ai[2] % rapidFireRate == 0) {
                        if (!VaultUtils.isClient) {
                            Vector2 shootVel = (player.Center - npc.Center).UnitVector() * 16f;
                            Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, shootVel, ProjectileID.DeathLaser, 30, 0f, Main.myPlayer);
                        }
                        SoundEngine.PlaySound(SoundID.Item12, npc.Center);
                    }

                    if (ai[2] >= 240) {
                        ai[1] = 1;
                        ai[2] = 0;
                    }
                    break;

                case 1: //水平弹幕
                    targetPos = player.Center + new Vector2(0, -400);
                    //保持X轴对齐
                    float xDiff = player.Center.X - npc.Center.X;
                    npc.velocity.X = MathHelper.Lerp(npc.velocity.X, xDiff * 0.1f, 0.1f);
                    npc.velocity.Y = MathHelper.Lerp(npc.velocity.Y, (targetPos.Y - npc.Center.Y) * 0.05f, 0.1f);

                    npc.rotation = (player.Center - npc.Center).ToRotation() - MathHelper.PiOver2;

                    ai[2]++;
                    if (ai[2] % rapidFireRate == 0) {
                        if (!VaultUtils.isClient) {
                            Vector2 shootVel = (player.Center - npc.Center).UnitVector() * 16f;
                            Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, shootVel, ProjectileID.DeathLaser, 30, 0f, Main.myPlayer);
                        }
                        SoundEngine.PlaySound(SoundID.Item12, npc.Center);
                    }

                    if (ai[2] >= 240) {
                        ai[1] = 2;
                        ai[2] = 0;
                    }
                    break;

                case 2: //精准狙击
                    npc.velocity *= 0.9f;
                    npc.rotation = (player.Center - npc.Center).ToRotation() - MathHelper.PiOver2;
                    
                    ai[2]++;
                    if (ai[2] < 60) {
                        //蓄力特效
                        if (ai[2] % 5 == 0) {
                            Dust.NewDust(npc.Center, npc.width, npc.height, DustID.RedTorch, 0, 0, 0, default, 2f);
                        }
                    }
                    else if (ai[2] == 60) {
                        if (!VaultUtils.isClient) {
                            Vector2 toPlayer = (player.Center - npc.Center).UnitVector();
                            int projectileCount = 13; //弹幕数量
                            float spreadAngle = MathHelper.ToRadians(60); //扇形角度
                            float baseSpeed = 6f; //基础速度
                            if (CWRWorld.Death) {
                                baseSpeed += 3;
                            }

                            for (int i = 0; i < projectileCount; i++) {
                                float angle = MathHelper.Lerp(-spreadAngle / 2, spreadAngle / 2, i / (float)(projectileCount - 1));
                                Vector2 shootVel = toPlayer.RotatedBy(angle) * baseSpeed;
                                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, shootVel, ModContent.ProjectileType<DeadLaser>(), 55, 0f, Main.myPlayer);
                            }
                        }
                        SoundEngine.PlaySound(SoundID.Item33, npc.Center);
                        //后坐力
                        npc.velocity = -(player.Center - npc.Center).UnitVector() * 15f;
                    }

                    if (ai[2] >= 90) {
                        ai[3]++;
                        ai[2] = 0;
                        if (ai[3] >= 4) { //狙击4次后循环
                            ai[1] = 0;
                            ai[3] = 0;
                        }
                    }
                    break;
            }
        }

        private void MoveTo(Vector2 target, float speed, float inertia) {
            Vector2 direction = target - npc.Center;
            direction.Normalize();
            Vector2 desiredVelocity = direction * speed;
            npc.velocity = (npc.velocity * (1f - inertia)) + (desiredVelocity * inertia);
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