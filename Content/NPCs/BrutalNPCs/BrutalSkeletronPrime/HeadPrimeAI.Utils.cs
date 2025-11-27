using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime
{
    /// <summary>
    /// 包含AI的辅助方法和效果
    /// </summary>
    internal partial class HeadPrimeAI
    {
        internal static bool DontReform() {
            if (!Main.expertMode) {
                return true;
            }//如果不是专家模式，就不要使用重做后的绘制
            if (CWRWorld.Revenge || CWRWorld.Death || CWRRef.GetBossRushActive() || CWRWorld.MachineRebellion) {
                return false;
            }//如果没有开启任何难度，也不要使用重做后的绘制
            return true;
        }

        internal static void FindPlayer(NPC npc) {
            if (npc.target < 0 || npc.target == Main.maxPlayers || Main.player[npc.target].dead || !Main.player[npc.target].active) {
                npc.TargetClosest();
            }
            if (Vector2.Distance(Main.player[npc.target].Center, npc.Center) > 3200f) {
                npc.TargetClosest();
            }
        }

        internal static int SetMultiplier(int num) {
            if (!CWRRef.GetBossRushActive()) {
                if (CWRRef.GetEarlyHardmodeProgressionReworkBool()) {
                    double firstMechMultiplier = 0.9f;
                    double secondMechMultiplier = 0.95f;
                    if (!NPC.downedMechBossAny) {
                        num = (int)(num * firstMechMultiplier);
                    }
                    else if ((!NPC.downedMechBoss1 && !NPC.downedMechBoss2) || (!NPC.downedMechBoss2 && !NPC.downedMechBoss3) || (!NPC.downedMechBoss3 && !NPC.downedMechBoss1)) {
                        num = (int)(num * secondMechMultiplier);
                    }
                }
                if (CWRWorld.Revenge) {
                    num = (int)(num * 0.75f);
                }
            }
            return num;
        }

        internal static void CheakDead(NPC npc, NPC head) {
            //所以，如果头部死亡，那么手臂也立马死亡
            if (head.active) {
                return;
            }

            npc.ai[2] += 10f;
            if (npc.ai[2] <= 50f && VaultUtils.isServer) {
                return;
            }

            npc.life = -1;
            npc.HitEffect(0, 10.0);
            npc.active = false;
        }

        internal static void CheakRam(out bool cannonAlive, out bool viceAlive, out bool sawAlive, out bool laserAlive) {
            cannonAlive = viceAlive = sawAlive = laserAlive = false;
            if (CWRWorld.primeCannon != -1) {
                if (Main.npc[CWRWorld.primeCannon].active)
                    cannonAlive = true;
            }
            if (CWRWorld.primeVice != -1) {
                if (Main.npc[CWRWorld.primeVice].active)
                    viceAlive = true;
            }
            if (CWRWorld.primeSaw != -1) {
                if (Main.npc[CWRWorld.primeSaw].active)
                    laserAlive = true;
            }
            if (CWRWorld.primeLaser != -1) {
                if (Main.npc[CWRWorld.primeLaser].active)
                    sawAlive = true;
            }
        }

        internal static void SpanFireLerterDustEffect(NPC npc, int modes) {
            Vector2 pos = npc.Center + (npc.rotation + MathHelper.PiOver2).ToRotationVector2() * 30;
            for (int i = 0; i < 4; i++) {
                float rot1 = MathHelper.PiOver2 * i;
                Vector2 vr = rot1.ToRotationVector2();
                for (int j = 0; j < modes; j++) {
                    BasePRT spark = new PRT_Spark(pos, vr * (0.1f + j * 0.34f), false, 13, Main.rand.NextFloat(1.2f, 1.3f), Color.Red);
                    PRTLoader.AddParticle(spark);
                }
            }
        }

        internal static void SendExtraAI(NPC npc) {
            if (VaultUtils.isServer) {
                //TODO
            }
        }

        internal bool TargetPlayerIsActive() => player == null || player.dead
            || Math.Abs(npc.position.X - player.position.X) > maxfindModes
            || Math.Abs(npc.position.Y - player.position.Y) > maxfindModes;

        private void ThisFromeFindPlayer() {
            if (TargetPlayerIsActive()) {
                npc.TargetClosest();
                player = Main.player[npc.target];
                //在Boss完成登场表演前不要去切换脱战行为，所以这里判断一下npc.ai0，
                //防止Boss在初始化阶段或者出场阶段时，因为生成距离过远等原因而被判定脱战
                if (npc.ai[0] > 1 && TargetPlayerIsActive()) {
                    if (npc.ai[1] == 4) {
                        for (int i = 0; i < 5; i++) {
                            VaultUtils.Text(CWRLocText.Instance.SkeletronPrime_Text.Value, Color.Red);
                        }
                    }
                    npc.ai[1] = 3f;
                }
            }
        }

        internal void SpawnHouengEffect() {
            for (int i = 0; i < 133; i++) {
                PRT_Light particle = new PRT_Light(npc.Center + VaultUtils.RandVr(0, npc.width), VaultUtils.RandVr(3, 13), Main.rand.Next(1, 3), Color.Red, 32);
                PRTLoader.AddParticle(particle);
            }
            for (int i = 0; i < 60; i++) {
                Vector2 dustV = VaultUtils.RandVr(3, 33);
                int dust = Dust.NewDust(npc.Center + VaultUtils.RandVr(0, npc.width), 1, 1, DustID.FireworkFountain_Red, dustV.X, dustV.Y);
                Main.dust[dust].scale = Main.rand.NextFloat(1, 6);
            }
        }

        private void SpawnEye() {
            if (bossRush || NPC.IsMechQueenUp) {
                return;
            }

            foreach (var findN in Main.ActiveNPCs) {//在召唤前先清除所有已经有了的眼睛
                if (findN.type == NPCID.Retinazer || findN.type == NPCID.Spazmatism) {
                    findN.active = false;
                }
            }

            if (!VaultUtils.isClient && npc.Center.TryFindClosestPlayer(out var findPlayer)) {
                VaultUtils.TrySpawnBossWithNet(findPlayer, NPCID.Retinazer, false);
                VaultUtils.TrySpawnBossWithNet(findPlayer, NPCID.Spazmatism, false);
            }
        }

        private void KillArm_OneToTwoStages() {
            if (npc.ai[0] != 2) {//2表明是初元阶段，这个杀死手臂的函数在这个时候才能运行
                return;
            }
            npc.ai[0] = 3;//杀死手臂后表明进入三阶段
            npc.netUpdate = true;
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

        internal void SpawnArm(int limit = 0) {
            if (VaultUtils.isClient) {
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

        public override bool? CheckDead() {
            if (npc.ai[0] == 1 || npc.ai[0] == 0) {
                npc.dontTakeDamage = true;
                npc.life = 1;
                return false;
            }
            return true;
        }

        public override bool? On_PreKill() {
            if (Main.zenithWorld) {
                NPC.downedMechBoss1 = NPC.downedMechBoss2 = NPC.downedMechBoss3 = true;
                if (Main.dedServ) {
                    NetMessage.SendData(MessageID.WorldData);
                }
            }

            if (CWRWorld.MachineRebellion) {
                CWRWorld.MachineRebellionDowned = true;
                if (Main.dedServ) {
                    NetMessage.SendData(MessageID.WorldData);
                }
            }
            return base.On_PreKill();
        }
    }
}
