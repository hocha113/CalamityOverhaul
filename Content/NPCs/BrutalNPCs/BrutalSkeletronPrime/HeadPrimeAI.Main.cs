using CalamityOverhaul.Content.NPCs.BrutalNPCs.Common;
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
        }

        public override bool AI() {
            if (CWRWorld.CanTimeFrozen()) {
                CWRNpc.DoTimeFrozen(npc);
                return false;
            }

            bossRush = CWRRef.GetBossRushActive();
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

            //死亡演出接管——一旦进入便完全托管本帧 AI，跳过常规寻敌/攻击/脱战/切阶段逻辑
            if (UpdateDeathPerformance()) {
                UpdateMechThermalVisualState();
                ai9++;
                return false;
            }

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

            //机械热感视觉状态——头部+四肢共用 npc.whoAmI 作为索引
            UpdateMechThermalVisualState();

            ai9++;
            return false;
        }

        /// <summary>
        /// 决定机械骷髅王的机械热感滤镜状态：
        /// <list type="bullet">
        /// <item>登场/传送（ai0==1 或 ai10>0）→ 不施加，避免和登场演出冲突</item>
        /// <item>三阶段无肢体狂暴（ai0==3）+ 高速突进 → Dashing 白热高速</item>
        /// <item>ai1==4（原版表示重点攻击/标识状态，已存在drawColor红化）→ Warning 红黄</item>
        /// <item>三阶段常态 → Idle 但强度提高，体现暴怒</item>
        /// <item>二阶段常态 → Idle 低强度作为夜间能见度兜底</item>
        /// </list>
        /// </summary>
        private void UpdateMechThermalVisualState() {
            //死亡演出——满强度红色警告脉动，烘托"严重过载即将解体"的氛围
            if (npc.ai[0] == DeathPerformanceMainState) {
                MechBossVisualState.Push(npc.whoAmI, MechBossVisualMode.Warning, 1f,
                    0.5f + 0.5f * (float)System.Math.Sin(Main.GlobalTimeWrappedHourly * 18.0));
                return;
            }

            //登场或传送期间不施加滤镜，保持原始演出
            if (npc.ai[0] == 1f || ai10 > 0f) {
                return;
            }

            float speedSq = npc.velocity.LengthSquared();

            //三阶段（无手）+ 高速突进 → Dashing
            if (npc.ai[0] == 3f && speedSq > 14f * 14f) {
                MechBossVisualState.Push(npc.whoAmI, MechBossVisualMode.Dashing, 1f, 1f);
                return;
            }

            //ai[1]==4 是原版的强攻击/标识状态（PostDraw 中也对它做了红化处理），
            //同期叠加警告色更好地烘托危险氛围
            if (npc.ai[1] == 4f) {
                MechBossVisualState.Push(npc.whoAmI, MechBossVisualMode.Warning, 0.95f, 0.7f);
                return;
            }

            //一阶段冲撞攻击（ai[1]==1，朝玩家高速突进）+ 高速 → Warning
            if (npc.ai[1] == 1f && speedSq > 12f * 12f) {
                MechBossVisualState.Push(npc.whoAmI, MechBossVisualMode.Warning, 0.85f, 0.6f);
                return;
            }

            //三阶段常态——红橙描边稍强一点
            if (npc.ai[0] == 3f) {
                MechBossVisualState.Push(npc.whoAmI, MechBossVisualMode.Idle, 0.8f, 0f);
                return;
            }

            //二阶段常态——保持低强度滤镜兜底夜晚能见度
            MechBossVisualState.Push(npc.whoAmI, MechBossVisualMode.Idle, 0.55f, 0f);
        }
    }
}
