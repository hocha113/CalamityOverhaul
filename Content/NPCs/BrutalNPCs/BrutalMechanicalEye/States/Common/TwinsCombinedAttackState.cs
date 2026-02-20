using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Retinazer;
using CalamityOverhaul.Content.Projectiles.Boss.MechanicalEye;
using CalamityOverhaul.Content.Projectiles.Boss.SkeletronPrime;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Common
{
    /// <summary>
    /// 双子魔眼合击状态
    /// 魔焰眼和激光眼同步进行碰撞合击
    /// </summary>
    internal class TwinsCombinedAttackState : TwinsStateBase
    {
        public override string StateName => "TwinsCombinedAttack";
        public override TwinsStateIndex StateIndex => TwinsStateIndex.TwinsCombinedAttack;

        /// <summary>
        /// 集合阶段
        /// </summary>
        private const int GatherPhase = 50;

        /// <summary>
        /// 对位阶段
        /// </summary>
        private const int AlignPhase = 40;

        /// <summary>
        /// 蓄力阶段
        /// </summary>
        private const int ChargePhase = 60;

        /// <summary>
        /// 碰撞阶段
        /// </summary>
        private const int CollisionPhase = 30;

        /// <summary>
        /// 爆发阶段
        /// </summary>
        private const int BurstPhase = 40;

        /// <summary>
        /// 恢复阶段
        /// </summary>
        private const int RecoveryPhase = 35;

        /// <summary>
        /// 总时长
        /// </summary>
        private const int TotalDuration = GatherPhase + AlignPhase + ChargePhase + CollisionPhase + BurstPhase + RecoveryPhase;

        private TwinsStateContext Context;
        private NPC partnerNpc;
        private Vector2 collisionPoint;
        private Vector2 myStartPos;
        private Vector2 partnerStartPos;
        private bool hasCollided;
        private bool hasBurst;
        private float chargeSpeed;
        private int comboStep;

        public TwinsCombinedAttackState(int currentComboStep = 0) {
            comboStep = currentComboStep;
        }

        public override void OnEnter(TwinsStateContext context) {
            base.OnEnter(context);
            Context = context;
            hasCollided = false;
            hasBurst = false;
            chargeSpeed = context.IsMachineRebellion ? 25f : 20f;

            //寻找另一只眼睛
            FindPartner(context);
        }

        /// <summary>
        /// 寻找配对的眼睛
        /// </summary>
        private void FindPartner(TwinsStateContext context) {
            int partnerType = context.IsSpazmatism ? NPCID.Retinazer : NPCID.Spazmatism;
            foreach (var n in Main.npc) {
                if (n.active && n.type == partnerType) {
                    partnerNpc = n;
                    break;
                }
            }
        }

        public override ITwinsState OnUpdate(TwinsStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            //如果没有找到伙伴，直接返回普通状态
            if (partnerNpc == null || !partnerNpc.active) {
                return GetDefaultState();
            }

            Timer++;

            //阶段1: 集合
            if (Timer <= GatherPhase) {
                ExecuteGatherPhase(npc, player);
            }
            //阶段2: 对位
            else if (Timer <= GatherPhase + AlignPhase) {
                ExecuteAlignPhase(npc, player);
            }
            //阶段3: 蓄力
            else if (Timer <= GatherPhase + AlignPhase + ChargePhase) {
                ExecuteChargePhase(npc, player);
            }
            //阶段4: 碰撞
            else if (Timer <= GatherPhase + AlignPhase + ChargePhase + CollisionPhase) {
                ExecuteCollisionPhase(npc, player);
            }
            //阶段5: 爆发
            else if (Timer <= GatherPhase + AlignPhase + ChargePhase + CollisionPhase + BurstPhase) {
                ExecuteBurstPhase(npc, player);
            }
            //阶段6: 恢复
            else {
                ExecuteRecoveryPhase(npc, player);
            }

            //状态结束
            if (Timer >= TotalDuration) {
                return GetDefaultState();
            }

            return null;
        }

        /// <summary>
        /// 获取默认返回状态，保持招式套路循环
        /// </summary>
        private ITwinsState GetDefaultState() {
            if (Context.IsSpazmatism) {
                return new Spazmatism.SpazmatismFlameChaseState(comboStep);
            }
            else {
                return new RetinazerVerticalBarrageState(comboStep);
            }
        }

        /// <summary>
        /// 集合阶段
        /// </summary>
        private void ExecuteGatherPhase(NPC npc, Player player) {
            float progress = Timer / (float)GatherPhase;

            //计算碰撞点(玩家位置)
            collisionPoint = player.Center;

            //移动到玩家两侧
            float sideOffset = Context.IsSpazmatism ? -500f : 500f;
            Vector2 targetPos = player.Center + new Vector2(sideOffset, 0);
            MoveTo(npc, targetPos, 16f, 0.1f);
            FaceTarget(npc, player.Center);

            //设置蓄力状态
            context.SetChargeState(10, progress * 0.2f);

            //集合粒子
            if (!VaultUtils.isServer && Timer % 3 == 0) {
                int dustType = Context.IsSpazmatism ? DustID.SolarFlare : DustID.Vortex;
                Dust dust = Dust.NewDustDirect(npc.Center + Main.rand.NextVector2Circular(20, 20), 1, 1, dustType, 0, 0, 100, default, 1.2f);
                dust.noGravity = true;
            }
        }

        /// <summary>
        /// 对位阶段
        /// </summary>
        private void ExecuteAlignPhase(NPC npc, Player player) {
            int phaseTimer = Timer - GatherPhase;
            float progress = phaseTimer / (float)AlignPhase;

            //更新碰撞点
            collisionPoint = player.Center;

            //精确对位
            float sideOffset = Context.IsSpazmatism ? -450f : 450f;
            Vector2 targetPos = player.Center + new Vector2(sideOffset, 0);
            npc.Center = Vector2.Lerp(npc.Center, targetPos, 0.08f);
            npc.velocity *= 0.9f;

            //面向碰撞点
            FaceTarget(npc, collisionPoint);

            //记录起始位置
            myStartPos = npc.Center;
            if (partnerNpc != null) {
                partnerStartPos = partnerNpc.Center;
            }

            //设置蓄力状态
            context.SetChargeState(10, 0.2f + progress * 0.2f);

            //对位连接线粒子
            if (!VaultUtils.isServer && phaseTimer % 3 == 0 && partnerNpc != null) {
                Vector2 midPoint = (npc.Center + partnerNpc.Center) / 2f;
                int dustType = DustID.Electric;
                Dust dust = Dust.NewDustDirect(midPoint + Main.rand.NextVector2Circular(30, 30), 1, 1, dustType, 0, 0, 100, default, 1f);
                dust.noGravity = true;
                dust.velocity = Vector2.Zero;
            }
        }

        /// <summary>
        /// 蓄力阶段
        /// </summary>
        private void ExecuteChargePhase(NPC npc, Player player) {
            int phaseTimer = Timer - GatherPhase - AlignPhase;
            float progress = phaseTimer / (float)ChargePhase;

            //锁定位置
            npc.velocity = Vector2.Zero;
            FaceTarget(npc, collisionPoint);

            //设置蓄力状态
            context.SetChargeState(10, 0.4f + progress * 0.6f);

            //强力蓄力特效
            if (!VaultUtils.isServer) {
                int dustType = Context.IsSpazmatism ? DustID.SolarFlare : DustID.Vortex;

                //能量聚集
                if (phaseTimer % 2 == 0) {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    float dist = 80f - progress * 50f;
                    Vector2 dustPos = npc.Center + angle.ToRotationVector2() * dist;
                    Dust dust = Dust.NewDustDirect(dustPos, 1, 1, dustType, 0, 0, 100, default, 1.5f + progress);
                    dust.noGravity = true;
                    dust.velocity = (npc.Center - dustPos).SafeNormalize(Vector2.Zero) * (5f + progress * 4f);
                }

                //冲刺预警线
                if (phaseTimer % 3 == 0 && progress > 0.3f) {
                    Vector2 toCollision = (collisionPoint - npc.Center).SafeNormalize(Vector2.Zero);
                    float lineDist = 50f + (progress - 0.3f) / 0.7f * 200f;
                    Vector2 linePos = npc.Center + toCollision * lineDist;
                    Dust dust = Dust.NewDustDirect(linePos, 1, 1, dustType, 0, 0, 100, default, 1.3f);
                    dust.noGravity = true;
                    dust.velocity = toCollision * 3f;
                }

                //双眼之间的能量连接
                if (phaseTimer % 4 == 0 && partnerNpc != null && progress > 0.5f) {
                    int linkPoints = 8;
                    for (int i = 0; i < linkPoints; i++) {
                        float t = i / (float)(linkPoints - 1);
                        Vector2 linkPos = Vector2.Lerp(npc.Center, partnerNpc.Center, t);
                        linkPos += Main.rand.NextVector2Circular(10, 10);
                        Dust dust = Dust.NewDustDirect(linkPos, 1, 1, DustID.Electric, 0, 0, 100, default, 1.2f);
                        dust.noGravity = true;
                        dust.velocity = Vector2.Zero;
                    }
                }

                //蓄力音效
                if (phaseTimer == 1) {
                    SoundEngine.PlaySound(SoundID.Item15 with { Pitch = 0f, Volume = 0.9f }, npc.Center);
                }

                //蓄力完成
                if (phaseTimer == ChargePhase - 3) {
                    for (int i = 0; i < 15; i++) {
                        float angle = MathHelper.TwoPi / 15f * i;
                        Vector2 vel = angle.ToRotationVector2() * 6f;
                        Dust dust = Dust.NewDustDirect(npc.Center, 1, 1, dustType, vel.X, vel.Y, 0, default, 2f);
                        dust.noGravity = true;
                    }
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.2f }, npc.Center);
                }
            }
        }

        /// <summary>
        /// 碰撞阶段
        /// </summary>
        private void ExecuteCollisionPhase(NPC npc, Player player) {
            int phaseTimer = Timer - GatherPhase - AlignPhase - ChargePhase;
            float progress = phaseTimer / (float)CollisionPhase;

            //停止蓄力特效
            context.ResetChargeState();

            //向碰撞点冲刺
            if (!hasCollided) {
                Vector2 toCollision = (collisionPoint - npc.Center).SafeNormalize(Vector2.Zero);
                npc.velocity = toCollision * chargeSpeed;
                hasCollided = true;

                SoundEngine.PlaySound(SoundID.Item74 with { Pitch = 0.1f, Volume = 1.2f }, npc.Center);
            }

            //朝向速度方向
            FaceVelocity(npc);

            //冲刺轨迹
            if (!VaultUtils.isServer) {
                int dustType = Context.IsSpazmatism ? DustID.SolarFlare : DustID.Vortex;
                for (int i = 0; i < 3; i++) {
                    Dust dust = Dust.NewDustDirect(npc.Center + Main.rand.NextVector2Circular(15, 15), 1, 1, dustType, -npc.velocity.X * 0.2f, -npc.velocity.Y * 0.2f, 100, default, 1.5f);
                    dust.noGravity = true;
                }
            }

            //接近碰撞点时减速
            float distToCollision = Vector2.Distance(npc.Center, collisionPoint);
            if (distToCollision < 100f) {
                npc.velocity *= 0.9f;
            }
        }

        /// <summary>
        /// 爆发阶段
        /// </summary>
        private void ExecuteBurstPhase(NPC npc, Player player) {
            int phaseTimer = Timer - GatherPhase - AlignPhase - ChargePhase - CollisionPhase;
            float progress = phaseTimer / (float)BurstPhase;

            //爆发瞬间
            if (!hasBurst) {
                hasBurst = true;

                //停止移动
                npc.velocity = Vector2.Zero;

                //发射弹幕
                if (!VaultUtils.isClient) {
                    int projectileCount = Context.IsMachineRebellion ? 16 : 12;
                    int projType = Context.IsSpazmatism ? ModContent.ProjectileType<Fireball>() : ModContent.ProjectileType<DeadLaser>();
                    float baseSpeed = 8f;

                    for (int i = 0; i < projectileCount; i++) {
                        float angle = MathHelper.TwoPi / projectileCount * i;
                        Vector2 vel = angle.ToRotationVector2() * baseSpeed;
                        Projectile.NewProjectile(
                            npc.GetSource_FromAI(),
                            collisionPoint,
                            vel,
                            projType,
                            40,
                            0f,
                            Main.myPlayer
                        );
                    }

                    //第二波稍慢的弹幕
                    for (int i = 0; i < projectileCount / 2; i++) {
                        float angle = MathHelper.TwoPi / (projectileCount / 2) * i + MathHelper.Pi / projectileCount;
                        Vector2 vel = angle.ToRotationVector2() * (baseSpeed * 0.6f);
                        Projectile.NewProjectile(
                            npc.GetSource_FromAI(),
                            collisionPoint,
                            vel,
                            projType,
                            35,
                            0f,
                            Main.myPlayer
                        );
                    }
                }

                //爆发特效
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item62 with { Volume = 1.5f }, collisionPoint);

                    //巨大的爆发粒子
                    for (int i = 0; i < 60; i++) {
                        float angle = MathHelper.TwoPi / 60f * i;
                        Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(8f, 16f);
                        int dustType = Main.rand.NextBool() ? DustID.SolarFlare : DustID.Vortex;
                        Dust dust = Dust.NewDustDirect(collisionPoint, 1, 1, dustType, vel.X, vel.Y, 0, default, 2.5f);
                        dust.noGravity = true;
                    }

                    //电弧粒子
                    for (int i = 0; i < 30; i++) {
                        Vector2 vel = Main.rand.NextVector2Circular(12, 12);
                        Dust dust = Dust.NewDustDirect(collisionPoint, 1, 1, DustID.Electric, vel.X, vel.Y, 0, default, 1.8f);
                        dust.noGravity = true;
                    }
                }
            }

            //后退
            Vector2 retreatDir = (npc.Center - collisionPoint).SafeNormalize(Vector2.Zero);
            npc.velocity = retreatDir * (8f * (1f - progress));

            //后续波动粒子
            if (!VaultUtils.isServer && phaseTimer % 3 == 0) {
                float waveRadius = 50f + progress * 200f;
                int wavePoints = 12;
                for (int i = 0; i < wavePoints; i++) {
                    float angle = MathHelper.TwoPi / wavePoints * i + progress * MathHelper.Pi;
                    Vector2 wavePos = collisionPoint + angle.ToRotationVector2() * waveRadius;
                    int dustType = Main.rand.NextBool() ? DustID.SolarFlare : DustID.Vortex;
                    Dust dust = Dust.NewDustDirect(wavePos, 1, 1, dustType, 0, 0, 100, default, 1.3f * (1f - progress));
                    dust.noGravity = true;
                }
            }
        }

        /// <summary>
        /// 恢复阶段
        /// </summary>
        private void ExecuteRecoveryPhase(NPC npc, Player player) {
            //逐渐恢复
            npc.velocity *= 0.92f;
            FaceTarget(npc, player.Center);

            //残余粒子
            if (!VaultUtils.isServer && Timer % 5 == 0) {
                int dustType = Context.IsSpazmatism ? DustID.SolarFlare : DustID.Vortex;
                Dust dust = Dust.NewDustDirect(npc.Center + Main.rand.NextVector2Circular(20, 20), 1, 1, dustType, 0, -2, 100, default, 0.8f);
                dust.noGravity = true;
            }
        }

        private TwinsStateContext context => Context;
    }
}
