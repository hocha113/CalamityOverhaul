using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Retinazer;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Spazmatism;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Common
{
    /// <summary>超新星对撞，集合→蓄力→对撞，碰撞点超新星+双色弹幕环</summary>
    [InnoVault.StateMachines.VaultState((int)TwinsStateIndex.TwinsCombinedAttack, typeof(TwinsStateContext))]
    internal class TwinsCombinedAttackState : TwinsStateBase
    {
        public override string StateName => "TwinsCombinedAttack";
        public override TwinsStateIndex StateIndex => TwinsStateIndex.TwinsCombinedAttack;

        private const int GatherPhase = 50;
        private const int AlignPhase = 36;
        private int ChargePhase => Context.IsAsuraMode ? 52 : 62;
        private const int CollisionPhase = 30;
        private const int BurstPhase = 42;
        private const int RecoveryPhase = 35;
        private const int MaxPartnerWait = 120;

        private int TotalDuration => GatherPhase + AlignPhase + ChargePhase + CollisionPhase + BurstPhase + RecoveryPhase;

        private TwinsStateContext Context;
        private NPC partnerNpc;
        private Vector2 collisionPoint;
        private bool hasCollided;
        private bool hasBurst;
        private float chargeSpeed;
        private int comboStep;
        private int partnerWait;

        public TwinsCombinedAttackState() : this(0) {
        }

        public TwinsCombinedAttackState(int currentComboStep) {
            comboStep = currentComboStep;
        }

        public override void OnEnter(TwinsStateContext context) {
            base.OnEnter(context);
            Context = context;
            hasCollided = false;
            hasBurst = false;
            chargeSpeed = context.IsAsuraMode ? 26f : 23f;
            partnerWait = 0;

            //寻找另一只眼睛
            partnerNpc = TwinsStateContext.GetPartnerNpc(context.Npc.type);
        }

        public override ITwinsState OnUpdate(TwinsStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            //如果没有找到伙伴，直接返回普通状态
            if (partnerNpc == null || !partnerNpc.active) {
                TwinsStateContext.ClearComboSignal();
                return GetDefaultState();
            }

            Timer++;

            if (Timer <= GatherPhase) {
                ExecuteGatherPhase(npc, player);

                //集合末就绪，等双方同拍
                if (Timer == GatherPhase) {
                    TwinsStateContext.MarkComboReady(context.IsSpazmatism);
                    if (!TwinsStateContext.BothComboReady && partnerWait < MaxPartnerWait) {
                        Timer--;
                        partnerWait++;
                    }
                }
            }
            else if (Timer <= GatherPhase + AlignPhase) {
                ExecuteAlignPhase(npc, player);
            }
            else if (Timer <= GatherPhase + AlignPhase + ChargePhase) {
                ExecuteChargePhase(npc, player);
            }
            else if (Timer <= GatherPhase + AlignPhase + ChargePhase + CollisionPhase) {
                ExecuteCollisionPhase(npc, player);
            }
            else if (Timer <= GatherPhase + AlignPhase + ChargePhase + CollisionPhase + BurstPhase) {
                ExecuteBurstPhase(npc, player);
            }
            else {
                ExecuteRecoveryPhase(npc, player);
            }

            if (Timer >= TotalDuration) {
                return GetDefaultState();
            }

            return null;
        }

        /// <summary>获取默认返回状态，保持招式套路循环</summary>
        private ITwinsState GetDefaultState() {
            if (Context.IsSpazmatism) {
                return new SpazmatismFlameChaseState(comboStep);
            }
            else {
                return new RetinazerVerticalBarrageState(comboStep);
            }
        }

        /// <summary>集合阶段，弹簧飞抵玩家两侧</summary>
        private void ExecuteGatherPhase(NPC npc, Player player) {
            float progress = Timer / (float)GatherPhase;

            //计算碰撞点(玩家位置)
            collisionPoint = player.Center;

            //移动到玩家两侧
            float sideOffset = Context.IsSpazmatism ? -500f : 500f;
            Vector2 targetPos = player.Center + new Vector2(sideOffset, 0);
            TwinsMotion.SpringHover(npc, targetPos, 0.022f, 0.105f);
            FaceTarget(npc, player.Center);

            Context.SetChargeState(10, progress * 0.2f);

            //集合粒子
            if (Timer % 3 == 0) {
                TwinsMotion.ChargeGatherFX(npc.Center, Context.IsSpazmatism, progress * 0.4f, 60f);
            }
        }

        /// <summary>对位阶段，精确对位并相互校准</summary>
        private void ExecuteAlignPhase(NPC npc, Player player) {
            int phaseTimer = Timer - GatherPhase;
            float progress = phaseTimer / (float)AlignPhase;

            //更新碰撞点
            collisionPoint = player.Center;

            //精确对位
            float sideOffset = Context.IsSpazmatism ? -450f : 450f;
            Vector2 targetPos = player.Center + new Vector2(sideOffset, 0);
            npc.Center = Vector2.Lerp(npc.Center, targetPos, 0.1f);
            npc.velocity *= 0.88f;

            //面向碰撞点
            FaceTarget(npc, collisionPoint);

            Context.SetChargeState(10, 0.2f + progress * 0.2f);

            //双眼之间的电荷预兆
            if (!VaultUtils.isServer && phaseTimer % 4 == 0 && partnerNpc != null) {
                Vector2 linkPos = Vector2.Lerp(npc.Center, partnerNpc.Center, Main.rand.NextFloat());
                PRTLoader.NewParticle<PRT_TwinsSpark>(linkPos + Main.rand.NextVector2Circular(16, 16),
                    Main.rand.NextVector2Circular(1.5f, 1.5f), Color.White, Main.rand.NextFloat(0.8f, 1.2f))?.Configure(14, 0);
            }
        }

        /// <summary>蓄力阶段，锁定绷紧，能量内聚到极限</summary>
        private void ExecuteChargePhase(NPC npc, Player player) {
            int phaseTimer = Timer - GatherPhase - AlignPhase;
            float progress = phaseTimer / (float)ChargePhase;

            //锁定位置，末段绷紧颤抖
            npc.velocity = Vector2.Zero;
            if (progress > 0.6f && !VaultUtils.isServer) {
                npc.position += Main.rand.NextVector2Circular(2f, 2f) * progress;
            }
            FaceTarget(npc, collisionPoint);

            Context.SetChargeState(10, 0.4f + progress * 0.6f);

            if (!VaultUtils.isServer) {
                //能量内聚(密度随进度提升)
                if (phaseTimer % 2 == 0) {
                    TwinsMotion.ChargeGatherFX(npc.Center, Context.IsSpazmatism, progress, 100f);
                }

                //冲刺预警线火花
                if (phaseTimer % 3 == 0 && progress > 0.3f) {
                    Vector2 toCollision = (collisionPoint - npc.Center).SafeNormalize(Vector2.Zero);
                    float lineDist = 50f + (progress - 0.3f) / 0.7f * 240f;
                    PRTLoader.NewParticle<PRT_TwinsSpark>(npc.Center + toCollision * lineDist,
                        toCollision * 3f, Color.White, 1.1f)?.Configure(13, Context.IsSpazmatism ? 1 : 0);
                }

                if (phaseTimer == 1) {
                    SoundEngine.PlaySound(SoundID.Item15 with { Pitch = 0f, Volume = 0.9f }, npc.Center);
                }

                //蓄力完闪光+咆哮
                if (phaseTimer == ChargePhase - 3) {
                    Color themeColor = Context.IsSpazmatism ? TwinsMotion.SpazColor : TwinsMotion.RetinColor;
                    PRTLoader.NewParticle<PRT_DWave>(npc.Center, Vector2.Zero, themeColor, 0.16f)?
                        .Configure(Vector2.One, 0f, 0.85f, 12);
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.2f }, npc.Center);
                }
            }
        }

        /// <summary>碰撞阶段，音爆起步全速对撞，接近碰撞点保持高速(撞击感)</summary>
        private void ExecuteCollisionPhase(NPC npc, Player player) {
            Context.ResetChargeState();

            //向碰撞点冲刺
            if (!hasCollided) {
                Vector2 toCollision = (collisionPoint - npc.Center).SafeNormalize(Vector2.Zero);
                TwinsMotion.DashLaunch(npc, toCollision, chargeSpeed, Context.IsSpazmatism, 1.2f);
                hasCollided = true;
            }

            //每帧开碰撞伤，激光会被控重置
            EnableContactDamage(npc);

            //朝向速度方向
            FaceVelocity(npc);
            Context.PushDashVisuals(1f, 1f);

            //冲刺轨迹
            if (!VaultUtils.isServer && Timer % 2 == 0) {
                PRTLoader.NewParticle<PRT_TwinsSpark>(
                    npc.Center - npc.velocity.SafeNormalize(Vector2.Zero) * 28f + Main.rand.NextVector2Circular(13, 13),
                    -npc.velocity * 0.16f, Color.White, Main.rand.NextFloat(1.1f, 1.7f))?
                    .Configure(15, Context.IsSpazmatism ? 1 : 0);
            }

            //临近碰撞点急刹(保留冲击姿态)
            float distToCollision = Vector2.Distance(npc.Center, collisionPoint);
            if (distToCollision < 90f) {
                npc.velocity *= 0.82f;
            }
        }

        /// <summary>爆发，殉爆光团+冲击环+双色弹幕环</summary>
        private void ExecuteBurstPhase(NPC npc, Player player) {
            int phaseTimer = Timer - GatherPhase - AlignPhase - ChargePhase - CollisionPhase;
            float progress = phaseTimer / (float)BurstPhase;

            //爆发瞬间
            if (!hasBurst) {
                hasBurst = true;

                //停止移动并关闭碰撞伤害
                npc.velocity = Vector2.Zero;
                DisableContactDamage(npc);

                //超新星扭曲环，魔焰侧生成
                if (Context.IsSpazmatism && !VaultUtils.isClient) {
                    Projectile.NewProjectile(npc.GetSource_FromAI(), collisionPoint, Vector2.Zero,
                        ModContent.ProjectileType<TwinsSupernovaBlast>(), 0, 0f, Main.myPlayer, 2f, 2f);
                }

                //本色弹幕环交错
                if (!VaultUtils.isClient) {
                    int projectileCount = 9;
                    int projType = Context.IsSpazmatism
                        ? ModContent.ProjectileType<Fireball>()
                        : ModContent.ProjectileType<RetinazerLaser>();
                    float baseSpeed = Context.IsSpazmatism ? 7.5f : 9f;
                    //魔焰偶相位，激光奇相位
                    float phaseOffset = Context.IsSpazmatism ? 0f : MathHelper.Pi / projectileCount;

                    for (int i = 0; i < projectileCount; i++) {
                        float angle = MathHelper.TwoPi / projectileCount * i + phaseOffset;
                        Vector2 vel = angle.ToRotationVector2() * baseSpeed;
                        Projectile.NewProjectile(npc.GetSource_FromAI(), collisionPoint, vel,
                            projType, 26, 0f, Main.myPlayer);
                    }

                    //第二波慢速余焰(再错开半相位)
                    for (int i = 0; i < projectileCount / 2; i++) {
                        float angle = MathHelper.TwoPi / (projectileCount / 2) * i + phaseOffset + MathHelper.Pi / projectileCount * 0.5f;
                        Vector2 vel = angle.ToRotationVector2() * (baseSpeed * 0.55f);
                        Projectile.NewProjectile(npc.GetSource_FromAI(), collisionPoint, vel,
                            projType, 26, 0f, Main.myPlayer);
                    }
                }

                //双方各绘一层，音效魔焰侧
                if (!VaultUtils.isServer) {
                    Color themeColor = Context.IsSpazmatism ? TwinsMotion.SpazColor : TwinsMotion.RetinColor;

                    //殉爆光团核心
                    PRTLoader.NewParticle<PRT_MechExplosion>(collisionPoint, Vector2.Zero, themeColor, 2.6f)?
                        .Configure(36, themeColor);

                    //多层错相冲击环
                    PRTLoader.NewParticle<PRT_DWave>(collisionPoint, Vector2.Zero, themeColor, 0.3f)?
                        .Configure(Vector2.One, Main.rand.NextFloat(MathHelper.TwoPi), 2.1f, 24);
                    PRTLoader.NewParticle<PRT_DWave>(collisionPoint, Vector2.Zero, Color.White * 0.85f, 0.18f)?
                        .Configure(Vector2.One, Main.rand.NextFloat(MathHelper.TwoPi), 1.3f, 18);

                    //放射状能量火花
                    for (int i = 0; i < 22; i++) {
                        PRTLoader.NewParticle<PRT_TwinsSpark>(collisionPoint, VaultUtils.RandVr(6, 16),
                            Color.White, Main.rand.NextFloat(1.4f, 2.4f))?.Configure(24, Context.IsSpazmatism ? 1 : 0);
                    }

                    //烟尘余波
                    for (int i = 0; i < 6; i++) {
                        PRTLoader.NewParticle<PRT_Smoke>(collisionPoint + Main.rand.NextVector2Circular(30, 30),
                            VaultUtils.RandVr(2, 5), themeColor * 0.6f, Main.rand.NextFloat(1f, 1.6f))?
                            .Configure(40, 0.55f, 0.03f, true, 0f);
                    }

                    if (Context.IsSpazmatism) {
                        SoundEngine.PlaySound(SoundID.Item62 with { Volume = 1.5f }, collisionPoint);
                        SoundEngine.PlaySound(SoundID.DD2_KoboldExplosion with { Pitch = -0.4f, Volume = 1.2f }, collisionPoint);
                        TwinsMotion.Shake(collisionPoint, 13f, 26);
                    }
                }
            }

            //反冲后退(被爆炸冲击波推开)
            Vector2 retreatDir = (npc.Center - collisionPoint).SafeNormalize(Vector2.Zero);
            npc.velocity = retreatDir * 11f * (1f - VaultUtils.EaseOutQuad(progress));
            FaceTarget(npc, collisionPoint);
            Context.PushDashVisuals(0.4f * (1f - progress), 0.5f);

            //后续扩散涟漪
            if (!VaultUtils.isServer && phaseTimer % 8 == 0 && progress < 0.7f && Context.IsSpazmatism) {
                PRTLoader.NewParticle<PRT_DWave>(collisionPoint, Vector2.Zero,
                    Color.Lerp(TwinsMotion.SpazColor, TwinsMotion.RetinColor, Main.rand.NextFloat()) * 0.6f, 0.4f)?
                    .Configure(Vector2.One, Main.rand.NextFloat(MathHelper.TwoPi), 1.6f, 20);
            }
        }

        private void ExecuteRecoveryPhase(NPC npc, Player player) {
            npc.velocity *= 0.92f;
            FaceTarget(npc, player.Center);

            if (!VaultUtils.isServer && Timer % 5 == 0) {
                PRTLoader.NewParticle<PRT_TwinsSpark>(npc.Center + Main.rand.NextVector2Circular(20, 20),
                    new Vector2(0, -2f), Color.White, 0.85f)?.Configure(14, Context.IsSpazmatism ? 1 : 0);
            }
        }

        public override void OnExit(TwinsStateContext context) {
            base.OnExit(context);
            DisableContactDamage(context.Npc);
            TwinsStateContext.ClearComboSignal();
        }
    }
}
