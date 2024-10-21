using CalamityMod;
using CalamityMod.Events;
using CalamityMod.NPCs;
using CalamityMod.Particles;
using CalamityMod.World;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.Core;
using CalamityOverhaul.Content.Particles;

using CalamityOverhaul.Content.Projectiles.Boss.SkeletronPrime;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime
{
    internal class BrutalSkeletronPrimeAI : NPCOverride, ICWRLoader
    {
        #region Data
        public override int TargetID => NPCID.SkeletronPrime;
        public ThanatosSmokeParticleSet SmokeDrawer;
        private const int maxfindModes = 6000;
        private CalamityGlobalNPC calamityNPC;
        private Player player;
        private int frame = 0;
        private int frameCount;
        private int primeCannon;
        private int primeSaw;
        private int primeVice;
        private int primeLaser;
        private int oneToTwoPrsAddNumBloodValue;
        private bool cannonAlive;
        private bool viceAlive;
        private bool sawAlive;
        private bool laserAlive;
        private bool bossRush;
        private bool death;
        private bool noArm => !cannonAlive && !laserAlive && !sawAlive && !viceAlive;
        private bool noEye;
        internal static int setPosingStarmCount;
        internal ref float ai0 => ref ai[0];
        internal ref float ai1 => ref ai[1];
        internal ref float ai2 => ref ai[2];
        internal ref float ai3 => ref ai[3];
        internal ref float ai4 => ref ai[4];
        internal ref float ai5 => ref ai[5];
        internal ref float ai6 => ref ai[6];
        internal ref float ai7 => ref ai[7];
        internal ref float ai8 => ref ai[8];
        internal ref float ai9 => ref ai[9];
        internal ref float ai10 => ref ai[10];
        internal ref float ai11 => ref ai[11];
        internal static bool canLoaderAssetZunkenUp;
        internal static Asset<Texture2D> HandAsset;
        internal static Asset<Texture2D> BSPCannon;
        internal static Asset<Texture2D> BSPlaser;
        internal static Asset<Texture2D> BSPPliers;
        internal static Asset<Texture2D> BSPSAW;
        internal static Asset<Texture2D> BSPRAM;
        internal static Asset<Texture2D> BSPRAM_Forearm;
        internal static Asset<Texture2D> HandAssetGlow;
        internal static Asset<Texture2D> BSPCannonGlow;
        internal static Asset<Texture2D> BSPlaserGlow;
        internal static Asset<Texture2D> BSPPliersGlow;
        internal static Asset<Texture2D> BSPSAWGlow;
        internal static Asset<Texture2D> BSPRAMGlow;
        internal static Asset<Texture2D> BSPRAM_ForearmGlow;
        #endregion

        internal static void DrawArm(SpriteBatch spriteBatch, NPC rCurrentNPC, Vector2 screenPos) {
            if (!canLoaderAssetZunkenUp) {
                return;
            }

            NPC head = Main.npc[(int)rCurrentNPC.ai[1]];

            if (setPosingStarmCount > 0) {
                return;
            }

            if ((head.ai[1] == 1 || head.ai[1] == 2 || head.CWR().NPCOverride.ai[10] > 0)
                && head.CWR().NPCOverride.ai[11] >= 1 && setPosingStarmCount <= 0) {
                float rCurrentNPCRotation = rCurrentNPC.rotation;
                Vector2 drawPos = rCurrentNPC.Center + (rCurrentNPCRotation + MathHelper.PiOver2).ToRotationVector2() * -120;
                Rectangle drawRec = CWRUtils.GetRec(BSPRAM.Value);
                Vector2 drawOrig = drawRec.Size() / 2;
                SpriteEffects spriteEffects = SpriteEffects.None;
                float rotation7 = rCurrentNPCRotation;
                Color color7 = Lighting.GetColor((int)drawPos.X / 16, (int)(drawPos.Y / 16f));
                drawPos.X -= Main.screenPosition.X;
                drawPos.Y -= Main.screenPosition.Y;
                spriteBatch.Draw(BSPRAM.Value, drawPos, drawRec, color7, rotation7, drawOrig, 1f, spriteEffects, 0f);
                spriteBatch.Draw(BSPRAMGlow.Value, drawPos, drawRec, Color.White, rotation7, drawOrig, 1f, spriteEffects, 0f);


                int num24 = Dust.NewDust(rCurrentNPC.Center, 10, 10, DustID.FireworkFountain_Red, 0, 0, 0, Color.Gold, 0.5f);
                Main.dust[num24].noGravity = false;
                return;
            }

            Vector2 vector7 = new Vector2(rCurrentNPC.position.X + rCurrentNPC.width * 0.5f - 5f * rCurrentNPC.ai[0], rCurrentNPC.position.Y + 20f);
            for (int k = 0; k < 2; k++) {
                float num21 = head.position.X + head.width / 2 - vector7.X;
                float num22 = head.position.Y + head.height / 2 - vector7.Y;
                float num23;

                if (k == 0) {
                    num21 -= 200f * rCurrentNPC.ai[0];
                    num22 += 130f;
                    num23 = (float)Math.Sqrt(num21 * num21 + num22 * num22);
                    num23 = 92f / num23;
                    vector7.X += num21 * num23;
                    vector7.Y += num22 * num23;
                }
                else {
                    num21 -= 50f * rCurrentNPC.ai[0];
                    num22 += 80f;
                    num23 = (float)Math.Sqrt(num21 * num21 + num22 * num22);
                    num23 = 60f / num23;
                    vector7.X += num21 * num23;
                    vector7.Y += num22 * num23;
                }

                float rotation7 = (float)Math.Atan2(num22, num21) - 1.57f;
                Color color7 = Lighting.GetColor((int)vector7.X / 16, (int)(vector7.Y / 16f));

                Texture2D value = BSPRAM.Value;
                Texture2D glow = BSPRAMGlow.Value;
                if (k == 0) {
                    value = BSPRAM_Forearm.Value;
                    glow = BSPRAM_ForearmGlow.Value;
                }

                Vector2 drawPos = new Vector2(vector7.X - screenPos.X, vector7.Y - screenPos.Y);
                Vector2 drawOrig = new Vector2(TextureAssets.BoneArm.Width() * 0.5f, TextureAssets.BoneArm.Height() * 0.5f);
                Rectangle drawRec = new Rectangle(0, 0, TextureAssets.BoneArm.Width(), TextureAssets.BoneArm.Height());
                SpriteEffects spriteEffects = k == 1 ? SpriteEffects.None : SpriteEffects.FlipVertically;

                spriteBatch.Draw(value, drawPos, drawRec, color7, rotation7, drawOrig, 1f, spriteEffects, 0f);
                spriteBatch.Draw(glow, drawPos, drawRec, Color.White, rotation7, drawOrig, 1f, spriteEffects, 0f);

                if (k == 0) {
                    vector7.X += num21 * num23 / 2f;
                    vector7.Y += num22 * num23 / 2f;
                }
                else if (Main.instance.IsActive) {
                    vector7.X += num21 * num23 - 16f;
                    vector7.Y += num22 * num23 - 6f;
                    int num24 = Dust.NewDust(new Vector2(vector7.X, vector7.Y), 30, 10
                        , DustID.FireworkFountain_Red, num21 * 0.02f, num22 * 0.02f, 0, Color.Gold, 0.5f);
                    Main.dust[num24].noGravity = true;
                }
            }
        }

        void ICWRLoader.LoadAsset() {
            string path = CWRConstant.NPC + "BSP/";
            HandAsset = CWRUtils.GetT2DAsset(path + "BrutalSkeletron");
            BSPCannon = CWRUtils.GetT2DAsset(path + "BSPCannon");
            BSPlaser = CWRUtils.GetT2DAsset(path + "BSPlaser");
            BSPPliers = CWRUtils.GetT2DAsset(path + "BSPPliers");
            BSPSAW = CWRUtils.GetT2DAsset(path + "BSPSAW");
            BSPRAM = CWRUtils.GetT2DAsset(path + "BSPRAM");
            BSPRAM_Forearm = CWRUtils.GetT2DAsset(path + "BSPRAM_Forearm");
            HandAssetGlow = CWRUtils.GetT2DAsset(path + "BrutalSkeletronGlow");
            BSPCannonGlow = CWRUtils.GetT2DAsset(path + "BSPCannonGlow");
            BSPlaserGlow = CWRUtils.GetT2DAsset(path + "BSPlaserGlow");
            BSPPliersGlow = CWRUtils.GetT2DAsset(path + "BSPPliersGlow");
            BSPSAWGlow = CWRUtils.GetT2DAsset(path + "BSPSAWGlow");
            BSPRAMGlow = CWRUtils.GetT2DAsset(path + "BSPRAMGlow");
            BSPRAM_ForearmGlow = CWRUtils.GetT2DAsset(path + "BSPRAM_ForearmGlow");
            canLoaderAssetZunkenUp = true;
        }

        void ICWRLoader.UnLoadData() {
            HandAsset = null;
            BSPCannon = null;
            BSPlaser = null;
            BSPPliers = null;
            BSPSAW = null;
            BSPRAM = null;
            BSPRAM_Forearm = null;
            HandAssetGlow = null;
            BSPCannonGlow = null;
            BSPlaserGlow = null;
            BSPPliersGlow = null;
            BSPSAWGlow = null;
            BSPRAMGlow = null;
            BSPRAM_ForearmGlow = null;
            canLoaderAssetZunkenUp = false;
        }

        public override bool CanLoad() => true;

        internal static bool SetArmRot(NPC arm, NPC head, int type) {
            NPCOverride pCOverride = head.CWR().NPCOverride;
            for (int i = 0; i < arm.buffImmune.Length; i++) {
                arm.buffImmune[i] = true;
            }
            arm.damage = arm.defDamage;
            if (pCOverride.ai[10] > 0) {
                arm.damage = 0;
            }
            if (setPosingStarmCount > 0 || pCOverride.ai[10] > 0) {
                float rot2 = MathHelper.TwoPi / 4 * type + head.rotation;
                Vector2 toPoint2 = head.Center + rot2.ToRotationVector2() * head.width;
                arm.Center = Vector2.Lerp(arm.Center, toPoint2, 0.2f);
                arm.rotation = head.Center.To(arm.Center).ToRotation() - MathHelper.PiOver2;
                arm.velocity = Vector2.Zero;
                arm.position += head.velocity;
                arm.dontTakeDamage = true;
                arm.damage = 0;
                return true;
            }
            if ((head.ai[1] != 1 && head.ai[1] != 2) || pCOverride.ai[11] < 1) {
                return false;
            }
            float rot = pCOverride.ai[9] * 0.1f + MathHelper.TwoPi / 4 * type;
            Vector2 toPoint = head.Center + rot.ToRotationVector2() * head.width * 2;
            float origeRot = head.Center.To(arm.Center).ToRotation();
            arm.Center = Vector2.Lerp(arm.Center, toPoint, 0.2f);
            arm.rotation = origeRot - MathHelper.PiOver2;
            arm.velocity = Vector2.Zero;
            arm.position += head.velocity;
            arm.dontTakeDamage = true;
            arm.damage = 0;
            if (!CWRUtils.isClient && NPC.IsMechQueenUp && pCOverride.ai[9] % 6 == 0 && setPosingStarmCount <= 0 && pCOverride.ai[10] <= 0) {
                int projType = ProjectileID.DeathLaser;
                Vector2 ver = origeRot.ToRotationVector2() * 6;
                Projectile.NewProjectile(arm.GetSource_FromAI(), arm.Center, ver, projType, 36, 2);
            }
            return true;
        }

        internal static void FindPlayer(NPC npc) {
            if (npc.target < 0 || npc.target == Main.maxPlayers || Main.player[npc.target].dead || !Main.player[npc.target].active) {
                npc.TargetClosest();
            }
            if (Vector2.Distance(Main.player[npc.target].Center, npc.Center) > CalamityGlobalNPC.CatchUpDistance200Tiles) {
                npc.TargetClosest();
            }
        }

        internal static int SetMultiplier(int num) {
            if (!BossRushEvent.BossRushActive) {
                if (CalamityConfig.Instance.EarlyHardmodeProgressionRework) {
                    double firstMechMultiplier = CalamityGlobalNPC.EarlyHardmodeProgressionReworkFirstMechStatMultiplier_Expert;
                    double secondMechMultiplier = CalamityGlobalNPC.EarlyHardmodeProgressionReworkSecondMechStatMultiplier_Expert;
                    if (!NPC.downedMechBossAny) {
                        num = (int)(num * firstMechMultiplier);
                    }
                    else if ((!NPC.downedMechBoss1 && !NPC.downedMechBoss2) || (!NPC.downedMechBoss2 && !NPC.downedMechBoss3) || (!NPC.downedMechBoss3 && !NPC.downedMechBoss1)) {
                        num = (int)(num * secondMechMultiplier);
                    }
                }
                if (CalamityWorld.death) {
                    num = (int)(num * 0.75f);
                }
            }
            return num;
        }

        internal static void CheakDead(NPC npc, NPC head) {
            // 所以，如果头部死亡，那么手臂也立马死亡
            if (!head.active) {
                npc.ai[2] += 10f;
                if (npc.ai[2] > 50f || Main.netMode != NetmodeID.Server) {
                    npc.life = -1;
                    npc.HitEffect(0, 10.0);
                    npc.active = false;
                }
            }
        }

        internal static void CheakRam(out bool cannonAlive, out bool viceAlive, out bool sawAlive, out bool laserAlive) {
            cannonAlive = viceAlive = sawAlive = laserAlive = false;
            if (CalamityGlobalNPC.primeCannon != -1) {
                if (Main.npc[CalamityGlobalNPC.primeCannon].active)
                    cannonAlive = true;
            }
            if (CalamityGlobalNPC.primeVice != -1) {
                if (Main.npc[CalamityGlobalNPC.primeVice].active)
                    viceAlive = true;
            }
            if (CalamityGlobalNPC.primeSaw != -1) {
                if (Main.npc[CalamityGlobalNPC.primeSaw].active)
                    sawAlive = true;
            }
            if (CalamityGlobalNPC.primeLaser != -1) {
                if (Main.npc[CalamityGlobalNPC.primeLaser].active)
                    sawAlive = true;
            }
        }

        internal static void SpanFireLerterDustEffect(NPC npc, int modes) {
            Vector2 pos = npc.Center + (npc.rotation + MathHelper.PiOver2).ToRotationVector2() * 30;
            for (int i = 0; i < 4; i++) {
                float rot1 = MathHelper.PiOver2 * i;
                Vector2 vr = rot1.ToRotationVector2();
                for (int j = 0; j < modes; j++) {
                    BasePRT spark = new PRT_HeavenfallStar(pos, vr * (0.1f + j * 0.34f), false, 13, Main.rand.NextFloat(1.2f, 1.3f), Color.Red);
                    PRTLoader.AddParticle(spark);
                }
            }
        }

        private void ThisFromeFindPlayer(NPC npc) {
            if (Main.player[npc.target].dead || Math.Abs(npc.position.X - Main.player[npc.target].position.X) > maxfindModes
                || Math.Abs(npc.position.Y - Main.player[npc.target].position.Y) > maxfindModes) {
                npc.TargetClosest();
                if (Main.player[npc.target].dead || Math.Abs(npc.position.X - Main.player[npc.target].position.X) > maxfindModes
                    || Math.Abs(npc.position.Y - Main.player[npc.target].position.Y) > maxfindModes) {
                    npc.ai[1] = 3f;
                }
            }
        }

        internal static void SendExtraAI(NPC npc) {
            if (CWRUtils.isServer) {
                npc.SyncExtraAI();
            }
        }

        public override void SetProperty() {
            ai0 = ai1 = ai2 = ai3 = ai4 = ai5 = ai6 = ai7 = ai8 = ai9 = ai10 = ai11 = 0;
            setPosingStarmCount = 0;
            SmokeDrawer = new ThanatosSmokeParticleSet(-1, 3, 0f, 16f, 1.5f);
            for (int i = 0; i < npc.buffImmune.Length; i++) {
                npc.buffImmune[i] = true;
            }
            int newMaxLife = (int)(npc.lifeMax * 0.7f);
            npc.life = npc.lifeMax = newMaxLife;
        }

        public override bool AI() {
            SmokeDrawer.ParticleSpawnRate = 99999;
            bossRush = BossRushEvent.BossRushActive;
            death = CalamityWorld.death || bossRush;
            player = Main.player[npc.target];
            npc.defense = npc.defDefense;
            npc.reflectsProjectiles = false;
            npc.dontTakeDamage = false;
            noEye = !NPC.AnyNPCs(NPCID.Retinazer) && !NPC.AnyNPCs(NPCID.Spazmatism);

            if (npc.ai[3] != 0f) {
                NPC.mechQueen = npc.whoAmI;
            }

            calamityNPC = null;
            if (!npc.TryGetGlobalNPC(out calamityNPC)) {
                return true;
            }

            setPosingStarmCount = 0;
            int typeSetPosingStarm = ModContent.ProjectileType<SetPosingStarm>();
            foreach (var value in Main.projectile) {
                if (value.type == typeSetPosingStarm && value.active) {
                    setPosingStarmCount++;
                }
            }

            //0-初始阶段
            //1-登场表演
            //2-初元阶段
            //3-攻击更加猛烈的二阶段
            if (npc.ai[0] == 0f) {
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    npc.TargetClosest();
                    SendExtraAI(npc);
                    NetAISend();
                }
                //设置为1，表明完成了首次初始化
                npc.ai[0] = 1f;
            }

            ThisFromeFindPlayer(npc);
            CheakRam(out cannonAlive, out viceAlive, out sawAlive, out laserAlive);
            DealingDaytimeRage();

            //这个部分是机械骷髅王刚刚进行tp传送后的行为，由ai10属性控制，在这个期间，
            //它不应该做任何攻击性的事情，要防止npc.ai[1]为3，而ai10这个值会自动消减
            if (InIdleAI()) {
                return false;
            }

            switch (npc.ai[0]) {
                case 1:
                    Debut();
                    break;
                case 2:
                    if (setPosingStarmCount > 0 && !noEye) {
                        npc.damage = 0;
                        MoveToPoint(npc, player.Center + new Vector2(0, -300));
                        npc.rotation = npc.rotation.AngleLerp(npc.velocity.X / 15f * 0.5f, 0.75f);

                        ai3 = 0;
                        return false;
                    }
                    ProtogenesisAI();
                    break;
                case 3:
                    if (TwoStageAI()) {
                        return false;
                    }
                    ProtogenesisAI();
                    break;
            }

            if (npc.life < npc.lifeMax - 20 && bossRush) {
                LifeRecovery();
            }
            if (npc.life < npc.lifeMax / 2) {
                KillArm_OneToTwoStages();
            }

            //如果手臂已经没了并且还是处于阶段二，那么就手动切换至三阶段
            if (noArm && npc.ai[0] == 2) {
                SoundEngine.PlaySound(SoundID.Roar, npc.Center);
                npc.ai[0] = 3;
            }

            FindFrame();

            ai9++;
            return false;
        }

        internal static void SpawnHouengEffect(NPC npc) {
            for (int i = 0; i < 333; i++) {
                PRT_Light particle = new PRT_Light(npc.Center + CWRUtils.randVr(0, npc.width), new Vector2(0, -5), 3.2f, Color.Red, 122);
                PRTLoader.AddParticle(particle);
            }
        }

        private void SpawnEye() {
            if (bossRush || NPC.IsMechQueenUp) {
                return;
            }
            foreach (var npc in Main.npc) {
                if (npc.active && npc.type == NPCID.Retinazer || npc.type == NPCID.Spazmatism) {
                    npc.active = false;
                }
            }
            CWRUtils.SpawnBossNetcoded(player, NPCID.Retinazer);
            CWRUtils.SpawnBossNetcoded(player, NPCID.Spazmatism);
        }

        private void Debut() {
            if (ai0 == 0) {
                SpawnEye();
                npc.life = 1;
                npc.Center = player.Center + new Vector2(0, 1200);
            }

            npc.damage = 0;
            npc.dontTakeDamage = true;

            Vector2 toTarget = npc.Center.To(player.Center);
            npc.rotation = npc.rotation.AngleLerp(toTarget.X / 115f * 0.5f, 0.75f);
            npc.velocity = Vector2.Zero;
            npc.position += player.velocity;
            Vector2 toPoint = player.Center;

            if (ai0 < 60) {
                toPoint = player.Center + new Vector2(0, 500);
            }
            else {
                toPoint = player.Center + new Vector2(0, -500);
                if (ai0 == 90 && !CWRUtils.isServer) {
                    SoundEngine.PlaySound(CWRSound.MechanicalFullBloodFlow, Main.LocalPlayer.Center);
                }
                if (ai0 > 90) {
                    int addNum = (int)(npc.lifeMax / 80f);
                    if (npc.life >= npc.lifeMax) {
                        npc.life = npc.lifeMax;
                    }
                    else {
                        Lighting.AddLight(npc.Center, Color.White.ToVector3());
                        npc.life += addNum;
                        CombatText.NewText(npc.Hitbox, CombatText.HealLife, addNum);
                    }
                }
            }

            if (ai0 == 172 && !CWRUtils.isServer) {
                SpawnHouengEffect(npc);
                SoundEngine.PlaySound(CWRSound.SpawnArmMgs, Main.LocalPlayer.Center);
            }
            if (ai0 == 180 && !CWRUtils.isClient) {
                spanArm(npc);
            }

            if (ai0 > 220) {
                npc.dontTakeDamage = false;
                npc.damage = npc.defDamage;
                npc.ai[0] = 2;
                ai0 = 0;
                return;
            }

            npc.Center = Vector2.Lerp(npc.Center, toPoint, 0.065f);

            ai0++;
        }

        private void ProtogenesisAI() {
            if (npc.ai[1] == 0f) {
                npc.damage = 0;
                npc.ai[2] += 1f;
                float aiThreshold = Main.masterMode ? 300f : 600f;
                if (npc.ai[2] >= aiThreshold) {
                    npc.ai[2] = 0f;
                    npc.ai[1] = 1f;
                    calamityNPC.newAI[0]++;
                    if (!CWRUtils.isClient && calamityNPC.newAI[0] >= 2) {
                        int damage = SetMultiplier(npc.defDamage / 3);
                        Projectile.NewProjectile(npc.GetSource_FromAI(), player.Center, new Vector2(0, 0)
                            , ModContent.ProjectileType<SetPosingStarm>(), damage, 2, -1, 0, npc.whoAmI);
                        calamityNPC.newAI[0] = 0;
                        ai11++;
                        SendExtraAI(npc);
                        NetAISend();
                    }
                    npc.TargetClosest();
                    npc.netUpdate = true;
                }

                npc.rotation = NPC.IsMechQueenUp ? npc.rotation.AngleLerp(npc.velocity.X / 15f * 0.5f, 0.75f) : npc.velocity.X / 15f;

                float verticalAcceleration = 0.1f;
                float maxVerticalSpeed = 2f;
                float horizontalAcceleration = 0.1f;
                float maxHorizontalSpeed = 8f;
                float deceleration = Main.masterMode ? 0.94f : Main.expertMode ? 0.96f : 0.98f;
                int verticalOffset = 200;
                int verticalThreshold = 500;
                float horizontalOffset = 0f;
                int directionMultiplier = (Main.player[npc.target].Center.X < npc.Center.X) ? -1 : 1;

                if (NPC.IsMechQueenUp) {
                    horizontalOffset = -450f * directionMultiplier;
                    verticalOffset = 300;
                    verticalThreshold = 350;
                }

                if (Main.expertMode) {
                    verticalAcceleration = Main.masterMode ? 0.04f : 0.03f;
                    maxVerticalSpeed = Main.masterMode ? 5f : 4f;
                    horizontalAcceleration = Main.masterMode ? 0.1f : 0.08f;
                    maxHorizontalSpeed = Main.masterMode ? 10f : 9.5f;
                    if (death) {
                        verticalAcceleration += 0.01f;
                        maxVerticalSpeed += 0.5f;
                        horizontalAcceleration += 0.1f;
                        maxHorizontalSpeed += 1f;
                    }
                    if (bossRush) {
                        verticalAcceleration += 0.01f;
                        maxVerticalSpeed += 0.5f;
                        horizontalAcceleration += 0.1f;
                        maxHorizontalSpeed += 1f;
                    }
                    if (noArm) {
                        verticalAcceleration += 0.01f;
                        maxVerticalSpeed += 0.125f;
                        horizontalAcceleration += 0.025f;
                        maxHorizontalSpeed += 0.25f;
                    }
                }

                AdjustVerticalMovement(npc, verticalAcceleration, maxVerticalSpeed, deceleration, verticalOffset, verticalThreshold);
                AdjustHorizontalMovement(npc, horizontalAcceleration, maxHorizontalSpeed, deceleration, horizontalOffset);
            }
            else if (npc.ai[1] == 1f) {
                npc.defense *= 2;
                npc.damage = npc.defDamage * 2;
                calamityNPC.CurrentlyIncreasingDefenseOrDR = true;

                npc.ai[2]++;
                if (npc.ai[2] == 2f) {
                    SoundEngine.PlaySound(SoundID.ForceRoar, npc.Center);
                }

                if (npc.ai[2] == 38f && ai11 >= 1 && !noArm) {//只有当ai11的值大于等于1后才会进行冲刺
                    SoundStyle sound = new SoundStyle("CalamityMod/Sounds/Custom/ExoMechs/AresEnraged");
                    SoundEngine.PlaySound(sound with { Pitch = 1.18f }, npc.Center);
                }

                float aiThreshold = Main.masterMode ? 300f : 400f;
                if (npc.ai[2] >= aiThreshold) {
                    npc.ai[2] = 0f;
                    npc.ai[1] = 0f;
                }

                UpdateRotation(npc);

                Vector2 targetVector = Main.player[npc.target].Center - npc.Center;
                float distanceToTarget = targetVector.Length();
                float initialSpeed = 5f;
                float speedMultiplier = CalculateSpeedMultiplier(distanceToTarget, initialSpeed);
                if (NPC.IsMechQueenUp) {
                    float mechQueenSpeedFactor = NPC.npcsFoundForCheckActive[NPCID.TheDestroyerBody] ? 0.6f : 0.75f;
                    speedMultiplier *= mechQueenSpeedFactor;
                }

                UpdateVelocity(calamityNPC, npc, targetVector, speedMultiplier, distanceToTarget);
            }
            else if (npc.ai[1] == 2f) {
                EnrageNPC(calamityNPC, npc);
                UpdateRotation(npc);
                MoveTowardsPlayer(npc, 10f, 8f, 32f, 100f);
            }
            else {
                if (npc.ai[1] != 3f) {
                    return;
                }
                HandleDespawn(npc);
            }
        }

        private bool TwoStageAI() {
            if (ai6 == 0 && ai9 > 2 && death && !bossRush) {
                ai3 = 3;
                SpawnEye();
                ai6 = 1;
                NetAISend();
            }

            int type = ProjectileID.RocketSkeleton;
            int damage = SetMultiplier(npc.GetProjectileDamage(type));
            float rocketSpeed = 10f;
            Vector2 cannonSpreadTargetDist = (player.Center - npc.Center).SafeNormalize(Vector2.UnitY) * rocketSpeed;
            int numProj = bossRush ? 5 : 3;
            float rotation = MathHelper.ToRadians(bossRush ? 15 : 9);

            if (npc.ai[1] != 1 || ai3 == 3) {
                SmokeDrawer.ParticleSpawnRate = 3;
                SmokeDrawer.BaseMoveRotation = MathHelper.ToRadians(90);
                SmokeDrawer.SpawnAreaCompactness = 80f;

                if (npc.life > npc.lifeMax / 10 && noEye) {
                    npc.life -= 10;
                }
            }
            SmokeDrawer.Update();

            if (setPosingStarmCount > 0 && !noEye && ai3 != 3) {
                npc.damage = 0;
                MoveToPoint(npc, player.Center + new Vector2(0, -300));
                npc.rotation = npc.rotation.AngleLerp(npc.velocity.X / 15f * 0.5f, 0.75f);

                ai3 = 1;
                return true;
            }

            switch (ai3) {
                case 0:
                    if (++ai4 > 90) {
                        npc.TargetClosest();

                        if (!CWRUtils.isClient) {
                            if (ai8 % 2 == 0) {
                                int totalProjectiles = bossRush ? 9 : 6;
                                if (!noEye) {
                                    totalProjectiles = 3;
                                }
                                Vector2 laserFireDirection = npc.Center.To(player.Center).UnitVector();
                                for (int j = 0; j < totalProjectiles; j++) {
                                    Vector2 vector = laserFireDirection.RotatedBy((totalProjectiles / -2 + j) * 0.2f) * 6;
                                    if (bossRush) {
                                        vector *= 1.45f;
                                    }
                                    int proj = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center + vector.SafeNormalize(Vector2.UnitY) * 100f
                                        , vector, ModContent.ProjectileType<DeadLaser>(), damage, 0f, Main.myPlayer, 1f, 0f);
                                }
                                SpanFireLerterDustEffect(npc, 73);
                            }
                            else {
                                for (int i = 0; i < numProj; i++) {
                                    float rotoffset = MathHelper.Lerp(-rotation, rotation, i / (float)(numProj - 1));
                                    Vector2 perturbedSpeed = cannonSpreadTargetDist.RotatedBy(rotoffset);
                                    if (death && Main.masterMode || bossRush) {
                                        Projectile.NewProjectile(npc.GetSource_FromAI()
                                        , npc.Center, perturbedSpeed
                                        , ModContent.ProjectileType<PrimeCannonOnSpan>(), damage, 0f
                                        , Main.myPlayer, npc.whoAmI, npc.target, rotoffset);
                                    }
                                    else {
                                        SoundEngine.PlaySound(SoundID.Item62, npc.Center);
                                        int proj = Projectile.NewProjectile(npc.GetSource_FromAI()
                                            , npc.Center + perturbedSpeed.SafeNormalize(Vector2.UnitY) * 40f
                                            , perturbedSpeed, type, damage, 0f, Main.myPlayer, npc.target, 2f);
                                        Main.projectile[proj].timeLeft = 600;
                                    }
                                }
                            }
                        }

                        ai4 = 0;
                        ai5++;
                        NetAISend();
                    }

                    if (ai5 > 3 || npc.ai[1] == 1) {
                        ai3 = 1;
                        ai4 = 0;
                        ai5 = 0;
                        ai8++;
                        NetAISend();
                    }
                    break;
                case 1:
                    if (ai4 > 90 && noEye && ai5 <= 2 && ai10 <= 0) {
                        npc.TargetClosest();
                        if (!CWRUtils.isClient) {
                            float maxLerNum = death ? 13 : 9f;
                            for (int i = 0; i < maxLerNum; i++) {
                                float rotoffset = MathHelper.TwoPi / maxLerNum * i;
                                Vector2 perturbedSpeed = cannonSpreadTargetDist.RotatedBy(rotoffset);
                                if (death && Main.masterMode || bossRush || ModGanged.InfernumModeOpenState) {
                                    Projectile.NewProjectile(npc.GetSource_FromAI()
                                    , npc.Center, perturbedSpeed
                                    , ModContent.ProjectileType<PrimeCannonOnSpan>(), damage, 0f
                                    , Main.myPlayer, npc.whoAmI, npc.target, rotoffset);
                                }
                                else {
                                    SoundEngine.PlaySound(SoundID.Item62, npc.Center);
                                    int proj = Projectile.NewProjectile(npc.GetSource_FromAI()
                                        , npc.Center + perturbedSpeed.SafeNormalize(Vector2.UnitY) * 40f
                                        , perturbedSpeed, type, damage, 0f, Main.myPlayer, npc.target, 2f);
                                    Main.projectile[proj].timeLeft = 600;
                                }
                            }
                        }
                        ai4 = 0;
                        ai5++;
                    }

                    if (npc.ai[1] != 1 && ai5 > 2 && ++ai7 > 60) {
                        ai3 = 2;
                        ai4 = 0;
                        ai5 = 0;
                        ai7 = 0;
                        NetAISend();
                    }

                    ai4++;
                    break;
                case 2:
                    npc.damage = 0;
                    if (ai7 == 0) {
                        if (setPosingStarmCount > 0 || !death) {
                            ai3 = 0;
                            ai4 = 0;
                            ai5 = 0;
                            ai7 = 0;
                            NetAISend();
                            return false;
                        }

                        npc.TargetClosest();
                        Vector2 pos2 = player.Center;

                        if (!CWRUtils.isClient) {
                            int maxShootNum = 40;
                            for (int i = 0; i < maxShootNum; i++) {
                                int maxD = 200;
                                int maxF = maxD * maxShootNum;
                                int toL = maxF / -2;
                                Vector2 spanPos = pos2 + new Vector2(toL + maxD * i, 1800);
                                Vector2 vr1 = new Vector2(0, -6);
                                Projectile.NewProjectile(npc.GetSource_FromAI()
                                        , spanPos, vr1
                                        , ModContent.ProjectileType<PrimeCannonOnSpan>(), damage, 0f
                                        , Main.myPlayer, -1, -1, vr1.ToRotation());
                            }
                            for (int i = 0; i < maxShootNum; i++) {
                                int maxD = 200;
                                int maxF = maxD * maxShootNum;
                                int toL = maxF / -2;
                                Vector2 spanPos = pos2 + new Vector2(-1800, toL + maxD * i);
                                Vector2 vr1 = new Vector2(6, 0);
                                Projectile.NewProjectile(npc.GetSource_FromAI()
                                        , spanPos, vr1
                                        , ModContent.ProjectileType<PrimeCannonOnSpan>(), damage, 0f
                                        , Main.myPlayer, -1, -1, vr1.ToRotation());
                            }
                        }
                        ai7++;
                    }

                    if (!CWRUtils.isServer) {
                        foreach (Player p in Main.player) {
                            if (p.dead || !p.active) {
                                continue;
                            }
                            p.Calamity().infiniteFlight = true;
                        }
                    }

                    if (++ai5 > 120) {
                        npc.damage = npc.defDamage * 2;
                        ai3 = 0;
                        ai4 = 0;
                        ai5 = 0;
                        ai7 = 0;
                        NetAISend();
                    }

                    break;
                case 3:
                    npc.damage = 0;
                    npc.dontTakeDamage = true;
                    npc.ai[1] = 0;

                    Vector2 toTarget = npc.Center.To(player.Center);
                    npc.rotation = npc.rotation.AngleLerp(toTarget.X / 115f * 0.5f, 0.75f);
                    npc.velocity = Vector2.Zero;
                    npc.position += player.velocity;
                    Vector2 toPoint = player.Center;

                    toPoint = player.Center + new Vector2(0, death ? -400 : -500);
                    int value = npc.lifeMax - npc.life;
                    if (ai4 == 0) {
                        oneToTwoPrsAddNumBloodValue = (int)(value / 360f);
                    }
                    if (npc.life >= npc.lifeMax) {
                        npc.life = npc.lifeMax;
                    }
                    else {
                        npc.life += oneToTwoPrsAddNumBloodValue;
                        CombatText.NewText(npc.Hitbox, CombatText.HealLife, oneToTwoPrsAddNumBloodValue);
                    }

                    npc.Center = Vector2.Lerp(npc.Center, toPoint, 0.065f);

                    if (ai4 == 0 && CWRUtils.isServer) {
                        SoundEngine.PlaySound(CWRSound.MechanicalFullBloodFlow, Main.LocalPlayer.Center);
                    }

                    ai4++;
                    if (ai4 > 360f || npc.life >= npc.lifeMax) {
                        npc.dontTakeDamage = false;
                        npc.damage = npc.defDamage * 2;
                        ai3 = 0;
                        ai4 = 0;
                    }

                    return true;
            }

            return false;
        }

        private void KillArm_OneToTwoStages() {
            if (npc.ai[0] != 2) {//2表明是初元阶段，这个杀死手臂的函数在这个时候才能运行
                return;
            }
            foreach (NPC index in Main.npc) {
                if (!index.active) {
                    continue;
                }
                if (index.type == NPCID.PrimeCannon) {
                    index.life = 0;
                    index.HitEffect();
                    index.active = false;
                }
                if (index.type == NPCID.PrimeVice) {
                    index.life = 0;
                    index.HitEffect();
                    index.active = false;
                }
                if (index.type == NPCID.PrimeSaw) {
                    index.life = 0;
                    index.HitEffect();
                    index.active = false;
                }
                if (index.type == NPCID.PrimeLaser) {
                    index.life = 0;
                    index.HitEffect();
                    index.active = false;
                }
            }
            npc.ai[0] = 3;//杀死手臂后表明进入三阶段
        }

        private void LifeRecovery() {
            if (cannonAlive) {
                npc.life += 1;
            }
            if (laserAlive) {
                npc.life += 1;
            }
            if (sawAlive) {
                npc.life += 1;
            }
            if (viceAlive) {
                npc.life += 1;
            }
        }

        private void FindFrame() {
            if (++frameCount > 10) {
                if (npc.ai[1] == 0) {
                    if (noArm && ai9 > 2) {
                        if (++frame > 11) {
                            frame = 8;
                        }
                    }
                    else {
                        if (++frame > 3) {
                            frame = 0;
                        }
                    }
                }
                else if (npc.ai[1] == 1) {
                    if (++frame > 7) {
                        frame = 4;
                    }
                }
                frameCount = 0;
            }
        }

        private bool InIdleAI() {
            if (npc.ai[1] != 3 && ai10 > 0) {
                npc.damage = 0;

                if (ai4 == 0) {
                    npc.velocity = new Vector2(0, -6);
                }
                if (++ai4 < 30) {
                    npc.velocity *= 0.98f;
                }
                else {
                    MoveToPoint(npc, player.Center + new Vector2(0, -300));
                }

                npc.rotation = npc.rotation.AngleLerp(npc.velocity.X / 15f * 0.5f, 0.75f);

                if (noArm) {
                    SmokeDrawer.ParticleSpawnRate = 3;
                    SmokeDrawer.BaseMoveRotation = MathHelper.ToRadians(90);
                    SmokeDrawer.SpawnAreaCompactness = 80f;
                }
                SmokeDrawer.Update();

                ai10--;
                if (ai10 <= 0) {
                    npc.damage = npc.defDamage * (noArm ? 2 : 1);
                    ai4 = 0;
                }
                return true;
            }
            return false;
        }

        private void DealingDaytimeRage() {
            if (Main.IsItDay()) {
                if (npc.ai[1] != 3f && npc.ai[1] != 2f) {
                    npc.ai[1] = 2f;
                    SoundEngine.PlaySound(SoundID.ForceRoar, npc.Center);
                }
            }
            else if (npc.ai[1] == 2f) {
                npc.ai[1] = 0;
                SoundEngine.PlaySound(SoundID.ForceRoar, npc.Center);
            }
        }

        #region SetFromeAIFunc
        private void MoveToPoint(NPC npc, Vector2 point) {
            npc.ChasingBehavior(point, 20);
        }

        private void AdjustVerticalMovement(NPC npc, float acceleration, float maxSpeed, float deceleration, int offset, int threshold) {
            if (npc.position.Y > Main.player[npc.target].position.Y - offset) {
                if (npc.velocity.Y > 0f) {
                    npc.velocity.Y *= deceleration;
                }
                npc.velocity.Y -= acceleration;
                if (npc.velocity.Y > maxSpeed) {
                    npc.velocity.Y = maxSpeed;
                }
            }
            else if (npc.position.Y < Main.player[npc.target].position.Y - threshold) {
                if (npc.velocity.Y < 0f) {
                    npc.velocity.Y *= deceleration;
                }
                npc.velocity.Y += acceleration;
                if (npc.velocity.Y < -maxSpeed) {
                    npc.velocity.Y = -maxSpeed;
                }
            }
        }

        private void AdjustHorizontalMovement(NPC npc, float acceleration, float maxSpeed, float deceleration, float offset) {
            if (npc.Center.X > Main.player[npc.target].Center.X + 100f + offset) {
                if (npc.velocity.X > 0f) {
                    npc.velocity.X *= deceleration;
                }
                npc.velocity.X -= acceleration;
                if (npc.velocity.X > maxSpeed) {
                    npc.velocity.X = maxSpeed;
                }
            }

            if (npc.Center.X < Main.player[npc.target].Center.X - 100f + offset) {
                if (npc.velocity.X < 0f) {
                    npc.velocity.X *= deceleration;
                }
                npc.velocity.X += acceleration;
                if (npc.velocity.X < -maxSpeed) {
                    npc.velocity.X = -maxSpeed;
                }
            }
        }

        private void UpdateRotation(NPC npc) {
            if (NPC.IsMechQueenUp || ai3 == 3) {
                npc.rotation = npc.rotation.AngleLerp(npc.velocity.X / 15f * 0.5f, 0.75f);
            }
            else {
                npc.rotation += npc.direction * 0.3f;
            }
        }

        private float CalculateSpeedMultiplier(float distance, float initialSpeed) {
            if (Main.expertMode) {
                float speed = Main.masterMode ? 5.5f : 4f;
                float speedFactor = Main.masterMode ? 1.125f : 1.1f;
                if (distance > 150f) speed *= Main.masterMode ? 1.05f : 1.025f;
                for (int threshold = 200; threshold <= 600; threshold += 50) {
                    if (distance > threshold) speed *= speedFactor;
                }
                return speed;
            }
            return initialSpeed;
        }

        private void UpdateVelocity(CalamityGlobalNPC calamityNPC, NPC npc, Vector2 targetVector, float speedMultiplier, float distance) {
            float adjustedSpeed = speedMultiplier / distance;
            if (death && ai11 >= 1) {
                if (--calamityNPC.newAI[2] <= 0) {
                    npc.velocity.X = targetVector.X * adjustedSpeed;
                    npc.velocity.Y = targetVector.Y * adjustedSpeed;
                }
                else {
                    npc.velocity *= 0.99f;
                }

                if (death || Main.masterMode) {
                    if (++calamityNPC.newAI[1] > 90) {
                        Vector2 toD = npc.Center.To(player.Center) + player.velocity;
                        toD = toD.UnitVector();
                        npc.velocity += toD * 23;
                        if (Main.npc[primeCannon].active)
                            Main.npc[primeCannon].velocity += toD * 33;
                        if (Main.npc[primeSaw].active)
                            Main.npc[primeSaw].velocity += toD * 53;
                        if (Main.npc[primeLaser].active)
                            Main.npc[primeLaser].velocity += toD * 33;
                        if (Main.npc[primeVice].active)
                            Main.npc[primeVice].velocity += toD * 53;

                        calamityNPC.newAI[2] = 60;
                        calamityNPC.newAI[1] = 0;
                        npc.netUpdate = true;
                        SendExtraAI(npc);
                    }
                }
            }
            else {
                npc.velocity.X = targetVector.X * adjustedSpeed;
                npc.velocity.Y = targetVector.Y * adjustedSpeed;
            }
            if (NPC.IsMechQueenUp) {
                float distanceToPlayer = Vector2.Distance(npc.Center, Main.player[npc.target].Center);
                if (distanceToPlayer < 0.1f) distanceToPlayer = 0f;
                if (distanceToPlayer < speedMultiplier)
                    npc.velocity = npc.velocity.SafeNormalize(Vector2.Zero) * distanceToPlayer;
            }
        }

        private void EnrageNPC(CalamityGlobalNPC calamityNPC, NPC npc) {
            // 增加 NPC 的伤害和防御
            npc.damage = 1000;
            npc.defense = 9999;
            // 标记当前正在愤怒状态和增加防御力或伤害减免
            calamityNPC.CurrentlyEnraged = true;
            calamityNPC.CurrentlyIncreasingDefenseOrDR = true;
        }

        private void MoveTowardsPlayer(NPC npc, float baseSpeed, float minSpeed, float maxSpeed, float speedDivisor) {
            // 计算玩家与 NPC 之间的向量和距离
            Vector2 npcCenter = npc.Center;
            Vector2 playerCenter = Main.player[npc.target].Center;
            Vector2 directionToPlayer = playerCenter - npcCenter;
            float distanceToPlayer = directionToPlayer.Length();
            // 计算速度
            float adjustedSpeed = baseSpeed + distanceToPlayer / speedDivisor;
            adjustedSpeed = Math.Clamp(adjustedSpeed, minSpeed, maxSpeed);
            // 根据计算出的向量调整速度
            directionToPlayer.Normalize();
            npc.velocity = directionToPlayer * adjustedSpeed;
        }

        private void HandleDespawn(NPC npc) {
            if (NPC.IsMechQueenUp) {
                DespawnNPC(NPCID.Retinazer);
                DespawnNPC(NPCID.Spazmatism);
                // 如果 Retinazer 和 Spazmatism 都不在，则变形并消失
                if (!NPC.AnyNPCs(NPCID.Retinazer) && !NPC.AnyNPCs(NPCID.Spazmatism)) {
                    TransformOrDespawnNPC(NPCID.TheDestroyer, NPCID.TheDestroyerTail, npc);
                }
                AdjustVelocity(npc, 0.1f, 0.95f, 13f);
            }
            else {
                npc.velocity = Vector2.Zero;
                if (++ai1 >= 60) {
                    SpawnHouengEffect(npc);
                    npc.active = false;
                    return;
                }
                else {
                    int value = npc.lifeMax - npc.life;
                    int addNum = value / 60;
                    npc.life += addNum;
                    if (npc.life > npc.lifeMax) {
                        npc.life = npc.lifeMax;
                    }
                }
            }
        }

        private void DespawnNPC(int npcID) {
            int npcIndex = NPC.FindFirstNPC(npcID);
            if (npcIndex >= 0) {
                Main.npc[npcIndex].EncourageDespawn(5);
            }
        }

        private void TransformOrDespawnNPC(int findNpcID, int transformNpcID, NPC npc) {
            int npcIndex = NPC.FindFirstNPC(findNpcID);
            if (npcIndex >= 0) {
                Main.npc[npcIndex].Transform(transformNpcID);
            }
            npc.EncourageDespawn(5);
        }

        private void AdjustVelocity(NPC npc, float verticalAcceleration, float horizontalDeceleration, float maxVerticalSpeed) {
            npc.velocity.Y += verticalAcceleration;
            if (npc.velocity.Y < 0f) {
                npc.velocity.Y *= horizontalDeceleration;
            }

            npc.velocity.X *= horizontalDeceleration;
            if (npc.velocity.Y > maxVerticalSpeed) {
                npc.velocity.Y = maxVerticalSpeed;
            }
        }

        private void spanArm(NPC npc, int limit = 0) {
            if (CWRUtils.isClient) {
                return;
            }
            if (limit == 1 || limit == 0) {
                primeCannon = NPC.NewNPC(npc.GetSource_FromAI(), (int)npc.Center.X, (int)npc.Center.Y, NPCID.PrimeCannon, npc.whoAmI);
                Main.npc[primeCannon].ai[0] = -1f;
                Main.npc[primeCannon].ai[1] = npc.whoAmI;
                Main.npc[primeCannon].target = npc.target;
                Main.npc[primeCannon].netUpdate = true;
            }
            if (limit == 2 || limit == 0) {
                primeSaw = NPC.NewNPC(npc.GetSource_FromAI(), (int)npc.Center.X, (int)npc.Center.Y, NPCID.PrimeSaw, npc.whoAmI);
                Main.npc[primeSaw].ai[0] = 1f;
                Main.npc[primeSaw].ai[1] = npc.whoAmI;
                Main.npc[primeSaw].target = npc.target;
                Main.npc[primeSaw].netUpdate = true;
            }
            if (limit == 3 || limit == 0) {
                primeVice = NPC.NewNPC(npc.GetSource_FromAI(), (int)npc.Center.X, (int)npc.Center.Y, NPCID.PrimeVice, npc.whoAmI);
                Main.npc[primeVice].ai[0] = -1f;
                Main.npc[primeVice].ai[1] = npc.whoAmI;
                Main.npc[primeVice].target = npc.target;
                Main.npc[primeVice].ai[3] = 150f;
                Main.npc[primeVice].netUpdate = true;
            }
            if (limit == 4 || limit == 0) {
                primeLaser = NPC.NewNPC(npc.GetSource_FromAI(), (int)npc.Center.X, (int)npc.Center.Y, NPCID.PrimeLaser, npc.whoAmI);
                Main.npc[primeLaser].ai[0] = 1f;
                Main.npc[primeLaser].ai[1] = npc.whoAmI;
                Main.npc[primeLaser].target = npc.target;
                Main.npc[primeLaser].ai[3] = 150f;
                Main.npc[primeLaser].netUpdate = true;
            }
        }
        #endregion

        public override bool? CheckDead() {
            if (npc.ai[0] == 1 || npc.ai[0] == 0) {
                npc.dontTakeDamage = true;
                npc.life = 1;
                return false;
            }
            return true;
        }

        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) 
            => npc.ai[0] == 1 && npc.life < npc.lifeMax && !death && !CalamityWorld.revenge;

        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (!canLoaderAssetZunkenUp) {
                return false;
            }

            if (npc.ai[0] == 1 && npc.life < npc.lifeMax) {
                return true;
            }

            Texture2D mainValue = HandAsset.Value;
            Texture2D mainValue2 = HandAssetGlow.Value;
            Rectangle rectangle = CWRUtils.GetRec(mainValue, frame, 12);
            Vector2 orig = rectangle.Size() / 2;

            SmokeDrawer?.DrawSet(npc.Center);

            Main.EntitySpriteDraw(mainValue, npc.Center - Main.screenPosition, rectangle
                , drawColor, npc.rotation, orig, npc.scale, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(mainValue2, npc.Center - Main.screenPosition, rectangle
                , Color.White, npc.rotation, orig, npc.scale, SpriteEffects.None, 0);

            if (player != null && noEye && npc.ai[0] == 3) {
                Vector2 toD = player.Center.To(npc.Center);
                Vector2 origpos = player.Center - Main.screenPosition;
                float alp = toD.Length() / 400f;
                if (alp > 1) {
                    alp = 1;
                }
                Vector2 drawPos1 = new Vector2(-toD.X, toD.Y) + origpos;
                Main.EntitySpriteDraw(mainValue, drawPos1, rectangle
                , drawColor * alp, npc.rotation, orig, npc.scale, SpriteEffects.None, 0);
                Vector2 drawPos2 = new Vector2(-toD.X, -toD.Y) + origpos;
                Main.EntitySpriteDraw(mainValue, drawPos2, rectangle
                , drawColor * alp, npc.rotation, orig, npc.scale, SpriteEffects.None, 0);
                Vector2 drawPos3 = new Vector2(toD.X, -toD.Y) + origpos;
                Main.EntitySpriteDraw(mainValue, drawPos3, rectangle
                , drawColor * alp, npc.rotation, orig, npc.scale, SpriteEffects.None, 0);
            }

            return false;
        }
    }
}
