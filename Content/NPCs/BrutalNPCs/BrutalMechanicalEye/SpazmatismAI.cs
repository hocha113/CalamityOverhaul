using CalamityMod;
using CalamityMod.Events;
using CalamityMod.World;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Magic;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Items.Rogue;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime;
using CalamityOverhaul.Content.NPCs.Core;
using CalamityOverhaul.Content.Projectiles.Boss.MechanicalEye;
using CalamityOverhaul.Content.Projectiles.Boss.SkeletronPrime;
using CalamityOverhaul.Content.RemakeItems.ModifyBag;
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
    internal class SpazmatismAI : NPCOverride, ICWRLoader
    {
        private delegate void TwinsBigProgressBarDrawDelegate(TwinsBigProgressBar inds, ref BigProgressBarInfo info, SpriteBatch spriteBatch);
        public override int TargetID => NPCID.Spazmatism;
        protected Player player;
        protected bool accompany;
        protected int frameIndex;
        protected int frameCount;
        public static Color TextColor1 => new(155, 215, 215);
        public static Color TextColor2 => new(200, 54, 91);
        internal static Asset<Texture2D> SpazmatismAsset;
        internal static Asset<Texture2D> SpazmatismAltAsset;
        internal static Asset<Texture2D> RetinazerAsset;
        internal static Asset<Texture2D> RetinazerAltAsset;
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
            CWRHook.Add(methodInfo, OnTwinsBigProgressBarDrawHook);
            _cacheField = typeof(TwinsBigProgressBar).GetField("_cache", BindingFlags.NonPublic | BindingFlags.Instance);
            _headIndexField = typeof(TwinsBigProgressBar).GetField("_headIndex", BindingFlags.NonPublic | BindingFlags.Instance);
        }
        void ICWRLoader.LoadAsset() {
            SpazmatismAsset = CWRUtils.GetT2DAsset(CWRConstant.NPC + "BEYE/Spazmatism");
            SpazmatismAltAsset = CWRUtils.GetT2DAsset(CWRConstant.NPC + "BEYE/SpazmatismAlt");
            RetinazerAsset = CWRUtils.GetT2DAsset(CWRConstant.NPC + "BEYE/Retinazer");
            RetinazerAltAsset = CWRUtils.GetT2DAsset(CWRConstant.NPC + "BEYE/RetinazerAlt");
        }
        void ICWRLoader.UnLoadData() {
            SpazmatismAsset = null;
            SpazmatismAltAsset = null;
            RetinazerAsset = null;
            RetinazerAltAsset = null;
            _cacheField = null;
            _headIndexField = null;
        }

        public override bool? CanOverride() {
            if (CWRWorld.MachineRebellion) {
                return true;
            }
            return base.CanOverride();
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
        //我不清楚为什么需要反射这个才能保证不报错，反正我反射了之后就没再因为双子血条报错了
        private void OnTwinsBigProgressBarDrawHook(TwinsBigProgressBarDrawDelegate orig
            , TwinsBigProgressBar inds, ref BigProgressBarInfo info, SpriteBatch spriteBatch) {
            int headIndex = (int)_headIndexField.GetValue(inds);
            if (headIndex < 0 || headIndex >= TextureAssets.NpcHeadBoss.Length) {
                return;
            }

            Texture2D value = TextureAssets.NpcHeadBoss[headIndex].Value;
            Rectangle barIconFrame = value.Frame();
            BigProgressBarCache _cache = (BigProgressBarCache)_cacheField.GetValue(inds);
            BigProgressBarHelper.DrawFancyBar(spriteBatch, _cache.LifeCurrent, _cache.LifeMax, value, barIconFrame);
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            if (npc.type == NPCID.Spazmatism) {
                IItemDropRuleCondition condition = new DropInDeathMode();
                LeadingConditionRule rule = new LeadingConditionRule(condition);
                rule.Add(ModContent.ItemType<FocusingGrimoire>(), 4);
                rule.Add(ModContent.ItemType<GeminisTribute>(), 4);
                rule.Add(ModContent.ItemType<Dicoria>(), 4);
                npcLoot.Add(rule);
            }
        }

        public static void SetMachineRebellion(NPC npc) {
            npc.life = npc.lifeMax *= 20;
            npc.defDefense = npc.defense = 40;
            npc.defDamage = npc.damage *= 2;
        }

        public override void SetProperty() {
            npc.realLife = -1;

            for (int i = 0; i < ai.Length; i++) {
                ai[i] = 0;
            }

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
                ai[11] = 0;
                NPC skeletronPrime = CWRUtils.FindNPCFromeType(NPCID.SkeletronPrime);
                if (skeletronPrime.Alives()) {
                    ai[11] = skeletronPrime.ai[0] != 3 ? 1 : 0;
                }
            }

            if (CWRWorld.MachineRebellion) {
                npc.life = npc.lifeMax *= 22;
                npc.defDefense = npc.defense = 40;
                npc.defDamage = npc.damage *= 2;
            }
        }

        public static void SetEyeValue(NPC eye, Player player, Vector2 toPoint, Vector2 toTarget) {
            float roting = toTarget.ToRotation() - MathHelper.PiOver2;
            eye.damage = 0;
            eye.position += player.velocity;
            eye.Center = Vector2.Lerp(eye.Center, toPoint, 0.1f);
            eye.velocity = toTarget.UnitVector() * 0.01f;
            eye.EntityToRot(roting, 0.2f);
        }

        public static void SetEyeRealLife(NPC eye) {
            if (eye.realLife > 0) {
                return;
            }
            if (eye.type == NPCID.Spazmatism) {
                NPC retinazer = CWRUtils.FindNPCFromeType(NPCID.Retinazer);
                if (retinazer.Alives()) {
                    eye.realLife = retinazer.whoAmI;
                }
            }
            else {
                NPC spazmatism = CWRUtils.FindNPCFromeType(NPCID.Spazmatism);
                if (spazmatism.Alives()) {
                    eye.realLife = spazmatism.whoAmI;
                }
            }
        }

        public bool AccompanyAI() {
            if (!accompany) {
                return false;
            }

            NPC skeletronPrime = CWRUtils.FindNPCFromeType(NPCID.SkeletronPrime);
            float lifeRog = npc.life / (float)npc.lifeMax;
            bool bossRush = BossRushEvent.BossRushActive;
            bool death = CalamityWorld.death || bossRush;
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
                if (Debut()) {
                    return true;
                }
            }

            if (IsSecondPhase()) {
                npc.HitSound = SoundID.NPCHit4;
            }

            if (skeletronPrimeIsDead || skeletronPrime?.ai[1] == 3 || lowBloodVolume || isSpawnFirstStageFromeExeunt) {
                npc.dontTakeDamage = true;
                npc.position += new Vector2(0, -36);
                if (ai[6] == 0) {
                    if (isSpazmatism && !VaultUtils.isServer) {
                        if (lowBloodVolume) {
                            VaultUtils.Text(CWRLocText.GetTextValue("Spazmatism_Text3"), TextColor1);
                            VaultUtils.Text(CWRLocText.GetTextValue("Spazmatism_Text4"), TextColor2);
                        }
                        else if (skeletronPrime?.ai[1] == 3) {
                            VaultUtils.Text(CWRLocText.GetTextValue("Spazmatism_Text5"), TextColor1);
                            VaultUtils.Text(CWRLocText.GetTextValue("Spazmatism_Text5"), TextColor2);
                        }
                        else if (isSpawnFirstStageFromeExeunt) {
                            VaultUtils.Text(CWRLocText.GetTextValue("Spazmatism_Text6"), TextColor1);
                            VaultUtils.Text(CWRLocText.GetTextValue("Spazmatism_Text6"), TextColor2);
                        }
                        else {
                            VaultUtils.Text(CWRLocText.GetTextValue("Spazmatism_Text7"), TextColor1);
                            VaultUtils.Text(CWRLocText.GetTextValue("Spazmatism_Text7"), TextColor2);
                        }
                    }
                    for (int i = 0; i < 13; i++) {
                        Item.NewItem(npc.GetSource_FromAI(), npc.Hitbox, ItemID.Heart);
                    }
                }
                if (ai[6] > 120) {
                    npc.active = false;
                }
                ai[6]++;
                return true;
            }

            Vector2 toTarget = npc.Center.To(player.Center);
            Vector2 toPoint = skeletronPrime.Center;
            npc.damage = npc.defDamage;
            bool skeletronPrimeInSprint = skeletronPrime.ai[1] == 1;
            bool LaserWall = skeletronPrime.CWR().NPCOverride.ai[3] == 2;
            bool isDestroyer = HeadPrimeAI.setPosingStarmCount > 0;
            bool isIdle = skeletronPrime.CWR().NPCOverride.ai[10] > 0;

            if (isIdle) {
                toPoint = skeletronPrime.Center + new Vector2(isSpazmatism ? 50 : -50, -100);
                SetEyeValue(npc, player, toPoint, toTarget);
                return true;
            }

            if (LaserWall) {
                toPoint = player.Center + new Vector2(isSpazmatism ? 450 : -450, -400);
                SetEyeValue(npc, player, toPoint, toTarget);
                return true;
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
                            Projectile.NewProjectile(npc.GetSource_FromAI()
                                , npc.Center, toTarget.UnitVector() * 9, projType, projDamage, 0);
                        }
                        ai[5] = 0;
                        NetAISend();
                    }
                    ai[4]++;
                    SetEyeValue(npc, player, toPoint, toTarget);
                    return true;
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
                    return true;
                }

                switch (ai[1]) {
                    case 0:
                        toPoint = player.Center + new Vector2(isSpazmatism ? 600 : -600, -650);
                        if (death) {
                            toPoint = player.Center + new Vector2(isSpazmatism ? 500 : -500, -650);
                        }
                        if (ai[2] == 30 && !VaultUtils.isClient) {
                            float shootSpeed = death ? 8 : 6;
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
                return true;
            }

            if (ai[7] > 0) {
                ai[7]--;
            }

            npc.VanillaAI();
            SetEyeRealLife(npc);
            return true;
        }

        public bool ProtogenesisAI() {
            float lifeRog = npc.life / (float)npc.lifeMax;
            bool isSpazmatism = npc.type == NPCID.Spazmatism;
            bool bossRush = BossRushEvent.BossRushActive;
            bool death = CalamityWorld.death || bossRush;
            player = Main.player[npc.target];
            npc.dontTakeDamage = false;
            npc.damage = npc.defDamage;
            if (!player.Alives()) {
                npc.TargetClosest();
                player = Main.player[npc.target];
                if (!player.Alives()) {
                    ai[0] = 4;
                    NetAISend();
                }
            }
            int projDamage = death ? 36 : 30;
            int projType = 0;
            switch (ai[0]) {
                case 0:
                    //这里应该做点什么，比如初始化
                    ai[0] = 1;
                    NetAISend();
                    break;
                case 1:
                    Debut();
                    break;
                case 2:
                    if (!CalamityWorld.death && !BossRushEvent.BossRushActive) {
                        ai[0] = 3;
                        break;
                    }
                    //ai1作为子阶段计数
                    //ai2作为一个全局时间点滴
                    //ai3作为位置关键值
                    //ai4作为一个攻击计数器
                    //ai5作为一个镜像索引
                    //ai6作为阶段切换的计数，从0切换到1会加一次值，用于判定是否切换到冲刺阶段
                    //ai7在这里仅仅用作一个备用计数器
                    Vector2 toPoint = player.Center;
                    Vector2 offset = Vector2.Zero;
                    Vector2 toTarget = npc.Center.To(player.Center);

                    if (ai[5] == 0) {
                        ai[5] = 1;
                    }
                    if (ai[1] == 0) {
                        npc.damage = 0;
                        offset = isSpazmatism ? new Vector2(600, ai[3]) : new Vector2(-600, ai[3]);
                        offset.X *= ai[5];
                        projType = isSpazmatism ? ProjectileID.DD2BetsyFireball : ProjectileID.DeathLaser;
                        if (!VaultUtils.isClient && ai[2] > 30) {
                            if (isSpazmatism) {
                                for (int i = 0; i < 6; i++) {
                                    Vector2 origVer = toTarget.UnitVector() * 8;
                                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center
                                    , origVer + new Vector2(origVer.X * 0.16f * i, -0.1f * i), projType, projDamage, 0);
                                }
                            }
                            else {
                                for (int i = 0; i < ai[4]; i++) {
                                    Vector2 origVer = toTarget.UnitVector() * 8;
                                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center
                                    , origVer.RotatedBy((ai[4] / -2 + i) * 0.06f), projType, projDamage, 0);
                                }
                            }

                            ai[2] = 0;
                            if (isSpazmatism) {
                                if (ai[3] == 0) {
                                    ai[3] = -860;
                                }
                                ai[3] += 160;
                            }
                            else {
                                if (ai[3] == 0) {
                                    ai[3] = 420;
                                }
                            }
                            if (++ai[4] > 7) {
                                ai[4] = 0;
                                ai[3] = 0;
                                ai[2] = 0;
                                ai[1] = 1;
                                ai[5] *= -1;
                                ai[6]++;
                            }
                            NetAISend();
                        }
                        SetEyeValue(npc, player, toPoint + offset, toTarget);
                    }
                    else if (ai[1] == 1) {
                        npc.damage = 0;
                        offset = isSpazmatism ? new Vector2(600 + ai[3] / 2, -500 + ai[3]) : new Vector2(-600 - ai[3] / 2, -500 + ai[3]);
                        offset.X *= ai[5];
                        projType = isSpazmatism ? ModContent.ProjectileType<Fireball>() : ProjectileID.EyeLaser;
                        float maxNum = 6f;
                        if (ai[2] > 30) {
                            for (int i = 0; i < maxNum; i++) {
                                Vector2 ver = (MathHelper.TwoPi / maxNum * i).ToRotationVector2() * (4 + ai[4] * 0.8f);
                                Projectile.NewProjectile(npc.GetSource_FromAI()
                                    , npc.Center, ver, projType, projDamage, 0);
                            }
                            ai[2] = 0;
                            ai[3] += 160;
                            if (++ai[4] > 6) {
                                ai[4] = 0;
                                ai[3] = 0;
                                ai[2] = 0;
                                ai[1] = 0;
                                if (ai[6] >= 2) {
                                    ai[1] = 2;//如果轮回了2次，那么就切换到2吧，开始冲刺
                                }
                            }
                            NetAISend();
                        }
                        SetEyeValue(npc, player, toPoint + offset, toTarget);
                    }
                    else if (ai[1] == 2) {
                        if (ai[2] < 40) {
                            npc.damage = 0;
                            if (ai[2] == 2) {
                                SoundEngine.PlaySound(SoundID.Roar);
                            }
                            offset = isSpazmatism ? new Vector2(600, -500) * ai[5] : new Vector2(-600, -500) * ai[5];
                            SetEyeValue(npc, player, toPoint + offset, toTarget);
                        }
                        else if (ai[2] < 90) {
                            npc.damage = (int)(npc.defDamage * 1.25f);
                            if (ai[2] == 42) {
                                SoundEngine.PlaySound(SoundID.ForceRoar);
                                npc.velocity = toTarget.UnitVector() * (death ? 30 : 25);
                                npc.rotation = toTarget.ToRotation() - MathHelper.PiOver2;
                            }
                        }
                        else {
                            npc.damage = npc.defDamage;
                            npc.VanillaAI();
                            if (ai[2] > 160) {
                                ai[3] = 0;
                                ai[2] = 0;
                                ai[1] = 3;
                                ai[5] *= -1;
                                ai[6] = 0;
                                npc.damage = 0;
                                NetAISend();
                            }
                        }
                    }
                    else if (ai[1] == 3) {
                        if (ai[2] < 40) {
                            npc.damage = 0;
                            if (ai[2] == 2) {
                                SoundEngine.PlaySound(SoundID.Roar);
                            }

                            if (ai[5] == 1) {
                                offset = isSpazmatism ? new Vector2(600, -500) : new Vector2(-600, 500);
                            }
                            else {
                                offset = isSpazmatism ? new Vector2(600, 500) : new Vector2(-600, -500);
                            }

                            SetEyeValue(npc, player, toPoint + offset, toTarget);
                        }
                        else if (ai[2] < 90) {
                            npc.damage = (int)(npc.defDamage * 1.25f);
                            if (ai[2] == 42) {
                                SoundEngine.PlaySound(SoundID.ForceRoar);
                                npc.velocity = toTarget.UnitVector() * (death ? 32 : 26);
                                npc.rotation = toTarget.ToRotation() - MathHelper.PiOver2;
                            }
                        }
                        else {
                            npc.damage = npc.defDamage;
                            npc.VanillaAI();
                            if (ai[2] > 380) {
                                ai[4]++;
                                ai[3] = 0;
                                ai[2] = 0;
                                ai[1] = 2;
                                ai[6] = 0;
                                if (ai[4] >= 2) {
                                    ai[1] = 0;
                                    ai[4] = 0;
                                }
                                npc.damage = 0;
                                NetAISend();
                            }
                        }
                    }

                    ai[2]++;
                    break;
                case 3:
                    return false;
                case 4:
                    if (isSpazmatism && !VaultUtils.isServer && ai[2] == 2) {
                        VaultUtils.Text(CWRLocText.GetTextValue("Spazmatism_Text5"), TextColor1);
                        VaultUtils.Text(CWRLocText.GetTextValue("Spazmatism_Text5"), TextColor2);
                    }
                    npc.dontTakeDamage = true;
                    npc.damage = 0;
                    npc.velocity = new Vector2(0, -33);
                    if (++ai[2] > 200) {
                        npc.active = false;
                    }
                    break;
            }
            if (lifeRog < 0.6f && ai[0] == 2) {
                ai[6] = 0;
                ai[5] = 0;
                ai[4] = 0;
                ai[3] = 0;
                ai[2] = 0;
                ai[1] = 0;
                ai[0] = 3;
                NetAISend();
            }
            return true;
        }

        private bool Debut() {
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

        public override bool AI() {
            if (++frameCount > 5) {
                if (++frameIndex > 3) {
                    frameIndex = 0;
                }
                frameCount = 0;
            }
            npc.dontTakeDamage = false;
            npc.spriteDirection = Math.Sign((npc.rotation + MathHelper.PiOver2).ToRotationVector2().X);

            if (AccompanyAI()) {
                return false;
            }

            if (ProtogenesisAI()) {
                return false;
            }

            return true;
        }

        internal bool IsSecondPhase() {
            if (accompany) {
                return ai[0] == 2;
            }
            return (npc.life / (float)npc.lifeMax) < 0.6f && ai[0] != 1;
        }

        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (HeadPrimeAI.DontReform()) {
                return true;
            }

            Texture2D mainValue = npc.type == NPCID.Spazmatism ? SpazmatismAsset.Value : RetinazerAsset.Value;
            Rectangle rectangle = CWRUtils.GetRec(mainValue, frameIndex, 4);
            SpriteEffects spriteEffects = npc.spriteDirection > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically;
            float drawRot = npc.rotation + MathHelper.PiOver2;
            if (IsSecondPhase()) {
                mainValue = npc.type == NPCID.Spazmatism ? SpazmatismAltAsset.Value : RetinazerAltAsset.Value;
                rectangle = CWRUtils.GetRec(mainValue, frameIndex, 4);
            }
            float sengs = 0.2f;
            for (int i = 0; i < npc.oldPos.Length; i++) {
                Vector2 drawOldPos = npc.oldPos[i] + npc.Size / 2 - Main.screenPosition;
                Main.EntitySpriteDraw(mainValue, drawOldPos, rectangle
                , Color.White * sengs, drawRot, rectangle.Size() / 2, npc.scale * (0.7f + sengs), spriteEffects, 0);
                sengs *= 0.9f;
            }
            Main.EntitySpriteDraw(mainValue, npc.Center - Main.screenPosition, rectangle
            , Color.White, drawRot, rectangle.Size() / 2, npc.scale, spriteEffects, 0);
            return false;
        }
    }
}
