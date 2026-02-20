using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime;
using CalamityOverhaul.Content.Projectiles.Boss.MechanicalEye;
using CalamityOverhaul.Content.Projectiles.Boss.SkeletronPrime;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core
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
            bool skeletronPrimeIsTwo = skeletronPrimeIsDead ? false : (skeletronPrime.ai[0] == 3);
            bool isSpawnFirstStage = Ai[11] == 1;
            bool isSpawnFirstStageFromeExeunt = false;

            if (!skeletronPrimeIsDead && isSpawnFirstStage) {
                isSpawnFirstStageFromeExeunt = ((skeletronPrime.life / (float)skeletronPrime.lifeMax) < 0.6f);
            }

            int projType = isSpazmatism ? ModContent.ProjectileType<Fireball>() : ProjectileID.EyeLaser;
            int projDamage = 36;
            if (CWRWorld.MachineRebellion) {
                projDamage = 92;
            }

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

            if (Ai[6] == 0 && !VaultUtils.isServer) {
                if (lowBloodVolume) {
                    if (isSpazmatism) {
                        VaultUtils.Text(CWRLocText.GetTextValue("Spazmatism_Text3"), TwinsAIController.TextColor1);
                    }
                    else {
                        VaultUtils.Text(CWRLocText.GetTextValue("Spazmatism_Text4"), TwinsAIController.TextColor2);
                    }

                    for (int i = 0; i < 13; i++) {
                        Item.NewItem(Npc.GetSource_FromAI(), Npc.Hitbox, ItemID.Heart);
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

            if (Ai[6] > 120) {
                Npc.active = false;
            }
            Ai[6]++;
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
                        float shootSpeed = CWRWorld.MachineRebellion ? 12 : 9;
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
                if (CWRWorld.MachineRebellion) {
                    shootSpeed = 12;
                }
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
                if (skeletronPrimeIsTwo) {
                    if (Ai[10] == 0) {
                        Ai[10] = 1;
                    }
                    if (!VaultUtils.isClient) {
                        Ai[9] = isSpazmatism ? -600 : 600;
                        Ai[9] += Main.rand.Next(-120, 90);
                    }
                    Ai[9] *= Ai[10];
                    Ai[10] *= -1;
                    Npc.netUpdate = true;
                }
                else {
                    if (!VaultUtils.isClient) {
                        Ai[9] = Main.rand.Next(140, 280) * (Main.rand.NextBool() ? -1 : 1);
                    }
                    Npc.netUpdate = true;
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
