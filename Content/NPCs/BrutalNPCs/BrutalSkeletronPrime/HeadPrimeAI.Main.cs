using CalamityOverhaul.Content.Projectiles.Boss.SkeletronPrime;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime
{
    /// <summary>
    /// 包含AI的主要逻辑和状态管理
    /// </summary>
    internal partial class HeadPrimeAI
    {
        public override void SetProperty() {
            ai0 = ai1 = ai2 = ai3 = ai4 = ai5 = ai6 = ai7 = ai8 = ai9 = ai10 = ai11 = 0;
            setPosingStarmCount = 0;
            int newMaxLife = (int)(npc.lifeMax * 0.7f);
            npc.life = npc.lifeMax = newMaxLife;
            npc.defDefense = npc.defense = 20;
            if (CWRWorld.MachineRebellion) {
                npc.life = npc.lifeMax *= 28;
                npc.defDefense = npc.defense = 40;
                npc.defDamage = npc.damage *= 3;
            }
        }

        public override bool AI() {
            if (CWRWorld.CanTimeFrozen()) {
                CWRNpc.DoTimeFrozen(npc);
                return false;
            }

            bossRush = CWRRef.GetBossRushActive() || CWRWorld.MachineRebellion;
            death = CWRRef.GetDeathMode() || bossRush;
            player = Main.player[npc.target];
            npc.defense = npc.defDefense;
            npc.reflectsProjectiles = false;
            npc.dontTakeDamage = false;
            noEye = !NPC.AnyNPCs(NPCID.Retinazer) && !NPC.AnyNPCs(NPCID.Spazmatism);

            if (npc.ai[3] != 0f) {
                NPC.mechQueen = npc.whoAmI;
            }

            setPosingStarmCount = 0;
            int typeSetPosingStarm = ModContent.ProjectileType<SetPosingStarm>();
            foreach (var value in Main.ActiveProjectiles) {
                if (value.type == typeSetPosingStarm) {
                    setPosingStarmCount++;
                }
            }

            //0-初始阶段
            //1-登场表演
            //2-初元阶段
            //3-攻击更加猛烈的二阶段
            if (npc.ai[0] == 0f) {
                if (!VaultUtils.isClient) {
                    npc.TargetClosest();
                    npc.netUpdate = true;//强制更新NPC
                }
                //设置为1，表明完成了首次初始化
                npc.ai[0] = 1f;
            }

            ThisFromeFindPlayer();
            CheakRam(out cannonAlive, out viceAlive, out sawAlive, out laserAlive);
            if (npc.ai[0] > 1) {
                DealingFury();
            }

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
                        MoveToPoint(player.Center + new Vector2(0, -300));
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

            if (!VaultUtils.isClient && npc.life < npc.lifeMax / 2) {
                KillArm_OneToTwoStages();
            }

            //如果手臂已经没了并且还是处于阶段二，那么就手动切换至三阶段
            if (noArm && npc.ai[0] == 2) {
                SoundEngine.PlaySound(SoundID.Roar, npc.Center);
                npc.ai[0] = 3;
            }

            ai9++;
            return false;
        }
    }
}
