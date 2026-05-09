using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core;
using CalamityOverhaul.Content.Projectiles.Boss.MechanicalEye;
using CalamityOverhaul.Content.Projectiles.Boss.SkeletronPrime;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime
{
    /// <summary>
    /// 双子魔眼随从模式AI处理器
    /// 负责与骷髅王配合时的所有行为逻辑
    /// </summary>
    internal class TwinsAccompanyHandler
    {
        #region 字段与属性

        private readonly TwinsStateContext context;

        private NPC Npc => context.Npc;
        private float[] Ai => context.Ai;
        private Player Player {
            get => context.Target;
            set => context.Target = value;
        }

        /// <summary>
        /// 撤离文本本地去重表：按 npc.whoAmI 记录是否已经在本端展示过撤离台词，
        /// 避免在多人模式中因 NPCOverride.ai 数组被服务端反复同步覆盖而出现"已撤离"刷屏。
        /// 静态生命周期，世界切换时由调用方手动 Clear。
        /// </summary>
        private static readonly HashSet<int> ExitTextShownSet = new HashSet<int>();

        /// <summary>
        /// 重置撤离去重表（应在 BOSS 重新生成或世界离开时调用）
        /// </summary>
        public static void ResetExitState() {
            ExitTextShownSet.Clear();
        }

        #endregion

        #region 构造

        public TwinsAccompanyHandler(TwinsStateContext context) {
            this.context = context;
        }

        #endregion

        #region 主逻辑

        /// <summary>
        /// 随从模式AI主循环
        /// </summary>
        /// <param name="isSecondPhase">是否处于二阶段的判定委托</param>
        /// <param name="executeDebutSequence">登场演出执行委托，返回true表示演出仍在进行</param>
        /// <returns>是否重置原版AI</returns>
        public bool Update(System.Func<bool> isSecondPhase, System.Func<bool> executeDebutSequence) {
            NPC skeletronPrime = CWRUtils.FindNPCFromeType(NPCID.SkeletronPrime);
            float lifeRog = Npc.life / (float)Npc.lifeMax;
            bool bossRush = CWRRef.GetBossRushActive();
            bool death = CWRRef.GetDeathMode() || bossRush;
            bool isSpazmatism = Npc.type == NPCID.Spazmatism;
            bool lowBloodVolume = lifeRog < 0.7f;
            bool skeletronPrimeIsDead = !skeletronPrime.Alives();
            bool skeletronPrimeIsTwo = skeletronPrimeIsDead ? false : skeletronPrime.ai[0] == 3;
            bool isSpawnFirstStage = Ai[11] == 1;
            bool isSpawnFirstStageFromeExeunt = false;

            if (!skeletronPrimeIsDead && isSpawnFirstStage) {
                isSpawnFirstStageFromeExeunt = skeletronPrime.life / (float)skeletronPrime.lifeMax < 0.6f;
            }

            int projType = isSpazmatism ? ModContent.ProjectileType<Fireball>() : ProjectileID.EyeLaser;
            int projDamage = 36;

            Player = skeletronPrimeIsDead ? Main.player[Npc.target] : Main.player[skeletronPrime.target];

            Lighting.AddLight(Npc.Center, (isSpazmatism ? Color.OrangeRed : Color.BlueViolet).ToVector3());

            if (Ai[0] == 0) {
                if (!VaultUtils.isServer && isSpazmatism) {
                    VaultUtils.Text(CWRLocText.GetTextValue("Spazmatism_Text1"), TwinsAIController.TextColor1);
                    VaultUtils.Text(CWRLocText.GetTextValue("Spazmatism_Text2"), TwinsAIController.TextColor2);
                }
                Ai[0] = 1;
                Npc.netUpdate = true;
            }

            if (Ai[0] == 1) {
                if (executeDebutSequence()) {
                    return false;
                }
            }

            if (isSecondPhase()) {
                Npc.HitSound = SoundID.NPCHit4;
            }

            if (skeletronPrimeIsDead || skeletronPrime?.ai[1] == 3 || lowBloodVolume || isSpawnFirstStageFromeExeunt) {
                ExecuteExit(skeletronPrime, isSpazmatism, lowBloodVolume, isSpawnFirstStageFromeExeunt);
                return false;
            }

            Vector2 toTarget = Npc.Center.To(Player.Center);
            Vector2 toPoint = skeletronPrime.Center;
            Npc.damage = Npc.defDamage;
            HeadPrimeAI headPrime = skeletronPrime.GetOverride<HeadPrimeAI>();
            bool skeletronPrimeInSprint = skeletronPrime.ai[1] == 1;
            bool LaserWall = headPrime.ai[3] == 2;
            bool isDestroyer = HeadPrimeAI.setPosingStarmCount > 0;
            bool isIdle = headPrime.ai[10] > 0;

            if (isIdle) {
                toPoint = skeletronPrime.Center + new Vector2(isSpazmatism ? 50 : -50, -100);
                TwinsAIController.SetEyeValue(Npc, Player, toPoint, toTarget);
                return false;
            }

            if (LaserWall) {
                toPoint = Player.Center + new Vector2(isSpazmatism ? 450 : -450, -400);
                TwinsAIController.SetEyeValue(Npc, Player, toPoint, toTarget);
                return false;
            }

            if (isDestroyer) {
                ExecuteDestroyerPhase(isSpazmatism, death, projType, projDamage, toTarget, skeletronPrimeIsTwo);
                return false;
            }
            else if (Ai[8] != 0) {
                Ai[8] = 0;
                Npc.netUpdate = true;
            }

            if (skeletronPrimeInSprint || Ai[7] > 0) {
                ExecuteAttackPhase(isSpazmatism, death, projType, projDamage, toTarget, skeletronPrimeIsTwo, isDestroyer);
                return false;
            }

            if (Ai[7] > 0) {
                Ai[7]--;
            }

            Npc.VanillaAI();
            return false;
        }

        #endregion

        #region 退场逻辑

        private void ExecuteExit(
            NPC skeletronPrime,
            bool isSpazmatism,
            bool lowBloodVolume,
            bool isSpawnFirstStageFromeExeunt
        ) {
            Npc.dontTakeDamage = true;
            Npc.position += new Vector2(0, -36);

            //撤离台词与心心掉落只在本端首次进入时触发，之后由 ExitTextShownSet 锁定不再重复
            //核心思路：
            //  1. NPCOverride 的 ai[6] 在多人模式下可能被服务端同步反复覆盖回 0，
            //     若仍以 Ai[6]==0 作为台词条件，会出现刷屏。
            //  2. 改用 npc.whoAmI 维度的本地静态集合做幂等，单端只播一次。
            //  3. 心心生成保持服务端单点，避免双端各下一份。
            if (!VaultUtils.isServer && ExitTextShownSet.Add(Npc.whoAmI)) {
                if (lowBloodVolume) {
                    if (isSpazmatism) {
                        VaultUtils.Text(CWRLocText.GetTextValue("Spazmatism_Text3"), TwinsAIController.TextColor1);
                    }
                    else {
                        VaultUtils.Text(CWRLocText.GetTextValue("Spazmatism_Text4"), TwinsAIController.TextColor2);
                    }
                }
                else if (skeletronPrime?.ai[1] == 3) {
                    VaultUtils.Text(CWRLocText.GetTextValue("Spazmatism_Text5"), TwinsAIController.TextColor2);
                }
                else if (isSpawnFirstStageFromeExeunt) {
                    VaultUtils.Text(CWRLocText.GetTextValue("Spazmatism_Text6"), TwinsAIController.TextColor2);
                }
                else {
                    VaultUtils.Text(CWRLocText.GetTextValue("Spazmatism_Text7"), TwinsAIController.TextColor2);
                }
            }

            //心心掉落由服务端单点处理，且仅在血量低位撤离时给奖励
            if (lowBloodVolume && !VaultUtils.isClient && Npc.localAI[1] == 0f) {
                for (int i = 0; i < 13; i++) {
                    Item.NewItem(Npc.GetSource_FromAI(), Npc.Hitbox, ItemID.Heart);
                }
                Npc.localAI[1] = 1f;
            }

            //计时使用 npc.localAI[0]（不会被 NPCOverride 同步覆盖），保证两端各自稳定走完撤离
            Npc.localAI[0] += 1f;

            //真正"消失"由服务端单点决策，客户端等待 SyncNPC 删除，
            //避免客户端单方面 active=false 后被服务端再次同步回来
            if (Npc.localAI[0] > 120f && !VaultUtils.isClient) {
                Npc.active = false;
                Npc.netUpdate = true;
                ExitTextShownSet.Remove(Npc.whoAmI);
            }
        }

        #endregion

        #region 毁灭者阶段

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
                Ai[8]++;
            }

            if (Ai[8] == Mechanicalworm.DontAttackTime + 10) {
                Npc.netUpdate = true;
            }

            if (Ai[8] > Mechanicalworm.DontAttackTime + 10) {
                int fireTime = 10;
                Vector2 toPoint;

                if (projectile.Alives()) {
                    fireTime = death ? 5 : 8;
                    toTarget = Npc.Center.To(projectile.Center);
                    float speedRot = death ? 0.02f : 0.03f;
                    toPoint = projectile.Center + (Ai[4] * speedRot + MathHelper.TwoPi / 2 * (isSpazmatism ? 1 : 2)).ToRotationVector2() * 1060;
                }
                else {
                    toPoint = Player.Center + (Ai[4] * 0.04f + MathHelper.TwoPi / 2 * (isSpazmatism ? 1 : 2)).ToRotationVector2() * 760;
                }

                if (++Ai[5] > fireTime && Ai[4] > 30) {
                    if (!VaultUtils.isClient) {
                        float shootSpeed = 9;
                        Projectile.NewProjectile(
                            Npc.GetSource_FromAI(),
                            Npc.Center,
                            toTarget.UnitVector() * shootSpeed,
                            projType,
                            projDamage,
                            0
                        );
                    }
                    Ai[5] = 0;
                    Npc.netUpdate = true;
                }

                Ai[4]++;
                TwinsAIController.SetEyeValue(Npc, Player, toPoint, toTarget);
            }
        }

        #endregion

        #region 攻击阶段

        private void ExecuteAttackPhase(
            bool isSpazmatism,
            bool death,
            int projType,
            int projDamage,
            Vector2 toTarget,
            bool skeletronPrimeIsTwo,
            bool isDestroyer
        ) {
            if (isDestroyer && Ai[8] < Mechanicalworm.DontAttackTime + 10) {
                Npc.damage = 0;
                Vector2 toPoint = Player.Center + new Vector2(isSpazmatism ? 600 : -600, -150);
                if (death) {
                    toPoint = Player.Center + new Vector2(isSpazmatism ? 500 : -500, -150);
                }
                TwinsAIController.SetEyeValue(Npc, Player, toPoint, toTarget);
                return;
            }

            switch (Ai[1]) {
                case 0:
                    ExecuteAttackCase0(isSpazmatism, death, projType, projDamage, toTarget);
                    break;
                case 1:
                    ExecuteAttackCase1(isSpazmatism, death, projType, projDamage, toTarget, skeletronPrimeIsTwo);
                    break;
            }
        }

        private void ExecuteAttackCase0(
            bool isSpazmatism,
            bool death,
            int projType,
            int projDamage,
            Vector2 toTarget
        ) {
            Vector2 toPoint = Player.Center + new Vector2(isSpazmatism ? 600 : -600, -650);
            if (death) {
                toPoint = Player.Center + new Vector2(isSpazmatism ? 500 : -500, -650);
            }

            if (Ai[2] == 30 && !VaultUtils.isClient) {
                float shootSpeed = death ? 8 : 6;
                for (int i = 0; i < 6; i++) {
                    Vector2 ver = (MathHelper.TwoPi / 6f * i).ToRotationVector2() * shootSpeed;
                    Projectile.NewProjectile(Npc.GetSource_FromAI(), Npc.Center, ver, projType, projDamage, 0);
                }
            }

            if (Ai[2] > 80) {
                Ai[7] = 10;
                Ai[1] = 1;
                Ai[2] = 0;
                Npc.netUpdate = true;
            }

            Ai[2]++;
            TwinsAIController.SetEyeValue(Npc, Player, toPoint, toTarget);
        }

        private void ExecuteAttackCase1(
            bool isSpazmatism,
            bool death,
            int projType,
            int projDamage,
            Vector2 toTarget,
            bool skeletronPrimeIsTwo
        ) {
            Vector2 toPoint = Player.Center + new Vector2(isSpazmatism ? 700 : -700, Ai[9]);

            if (++Ai[2] > 24) {
                if (!VaultUtils.isClient) {
                    if (skeletronPrimeIsTwo) {
                        for (int i = 0; i < 3; i++) {
                            Vector2 ver = toTarget.RotatedBy((-1 + i) * 0.06f).UnitVector() * 5;
                            Projectile.NewProjectile(Npc.GetSource_FromAI(), Npc.Center, ver, projType, projDamage, 0);
                        }
                    }
                    else {
                        Projectile.NewProjectile(Npc.GetSource_FromAI(), Npc.Center, toTarget.UnitVector() * 6, projType, projDamage, 0);
                    }
                }
                Ai[3]++;
                Ai[2] = 0;
                Npc.netUpdate = true;
            }

            if (Ai[2] == 2) {
                //彻底去随机化：
                //  使用 Ai[3]（已发射弹药计数）做确定性偏移，
                //  保证客户端和服务端在同一帧得到完全相同的 Ai[9] 值，
                //  不再依赖 Main.rand 与 npc.netUpdate 强同步。
                int shotIndex = (int)Ai[3];
                if (skeletronPrimeIsTwo) {
                    //三阶段随从：以 600 为基线，按 shotIndex 在 [-150,+90] 之间做正余弦波动
                    if (Ai[10] == 0) {
                        Ai[10] = 1;
                    }
                    float baseOffset = isSpazmatism ? -600f : 600f;
                    float wave = (float)System.Math.Sin(shotIndex * 1.13f) * 120f
                               - (float)System.Math.Cos(shotIndex * 0.74f) * 30f;
                    Ai[9] = (baseOffset + wave) * Ai[10];
                    Ai[10] *= -1;
                }
                else {
                    //二阶段常态：以 shotIndex 奇偶决定上下（±），余弦做幅度（140~280 间锯齿）
                    float magnitude = 140f + ((shotIndex * 47) % 141);
                    int dir = (shotIndex & 1) == 0 ? 1 : -1;
                    Ai[9] = magnitude * dir;
                }
            }

            if (Ai[3] > 6) {
                Ai[3] = 0;
                Ai[2] = 0;
                Ai[1] = 0;
                Ai[7] = 0;
                Npc.netUpdate = true;
            }
            else if (Ai[7] < 2) {
                Ai[7] = 2;
            }

            TwinsAIController.SetEyeValue(Npc, Player, toPoint, toTarget);
        }

        #endregion
    }
}
