using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Projectiles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.States.Arms
{
    //钳爪手设计准则：
    //1. 机体不整体打转——转向走 ServoRotate；rotation = θ - PiOver2（同死亡演出钳子 Actor）    //2. 突刺=液压活塞：回缩绷紧 → 硬咬合朝向 → 刚性直线，飞行不转体    //3. ctx.ClawOpen 驱动 2 帧贴图：蓄力/扑击张开，命中/待机闭合
    /// <summary>钳爪待机：头部下侧浮沉跟踪，充能后三连击→重锤确定性轮换</summary>
    [InnoVault.StateMachines.VaultState((int)PrimeArmStateIndex.ViceIdle, typeof(PrimeArmStateContext))]
    internal class ViceIdleState : PrimeArmStateBase
    {
        public override string StateName => "ViceIdle";
        public override PrimeArmStateIndex StateIndex => PrimeArmStateIndex.ViceIdle;

        public override PrimeArmStateBase OnUpdate(PrimeArmStateContext ctx) {
            NPC npc = ctx.Npc;
            npc.damage = 0;
            ctx.ClawOpen = false;

            //液压浮沉：锚点带确定性正弦起伏，呈现悬吊机械的呼吸感
            float bob = (float)System.Math.Sin((Main.GameUpdateCount + npc.whoAmI * 37) * 0.045f) * 10f;
            Vector2 idleAnchor = ctx.Head.Center + new Vector2(-150f * ctx.Side, 250f + bob);
            SpringMove(ctx, idleAnchor, 0.7f, stiffness: 0.18f, damping: 0.82f, maxSpeed: 28f);

            //钳口缓速咬向玩家方位
            ServoAimAt(npc, ctx.Target.Center, 0.03f);

            float chargeRate = ctx.MasterMode ? 2f : 1f;
            if (ctx.Death) {
                chargeRate *= PrimeDirector.DeathChargeMultiplier;
            }
            chargeRate += ctx.MissingPartnerCount * PrimeDirector.MissingLimbChargeBonus;
            ctx.ChargeTimer += chargeRate;

            int threshold = PrimeDirector.GetArmChargeThreshold(ctx.MasterMode, ctx.Death);
            if (ctx.ChargeTimer >= threshold && !VaultUtils.isClient && !ctx.DontAttack) {
                ctx.ChargeTimer = 0f;
                npc.TargetClosest();
                npc.netUpdate = true;

                if (HeadPrimeAI.GetActiveCommand(ctx.Head) == PrimeCommandKind.PhysicalAssault) {
                    return new ViceTripleLungeState();
                }

                int cycle = ctx.AttackCycle;
                ctx.AttackCycle = (cycle + 1) % 4;
                return cycle switch {
                    0 => new ViceTripleLungeState(),
                    1 => new ViceClapWaveState(),
                    2 => new ViceComboState(),
                    _ => new ViceWindUpState(),
                };
            }
            return null;
        }
    }

    /// <summary>钳爪后撤蓄力：活塞回缩、钳口张开锁定，尾段反向蹬缩后刚性突刺</summary>
    [InnoVault.StateMachines.VaultState((int)PrimeArmStateIndex.ViceWindUp, typeof(PrimeArmStateContext))]
    internal class ViceWindUpState : PrimeArmStateBase
    {
        public override string StateName => "ViceWindUp";
        public override PrimeArmStateIndex StateIndex => PrimeArmStateIndex.ViceWindUp;

        internal static float WindUpDistance => 280f;
        /// <summary>突刺前的活塞回缩帧数</summary>
        internal static int CoilFrames => 5;

        private int WindUpDuration(PrimeArmStateContext ctx) => ctx.Death ? 16 : (ctx.MasterMode ? 21 : 26);

        public override PrimeArmStateBase OnUpdate(PrimeArmStateContext ctx) {
            NPC npc = ctx.Npc;
            npc.damage = 0;
            ctx.ClawOpen = true;

            Vector2 directionToPlayer = npc.Center.DirectionTo(ctx.Target.Center);
            int duration = WindUpDuration(ctx);

            if (Timer < duration - CoilFrames) {
                //后撤到出手位，钳口伺服锁定
                Vector2 windUpPos = ctx.Target.Center - directionToPlayer * WindUpDistance;
                SpringMove(ctx, windUpPos, 1.3f, stiffness: 0.18f, damping: 0.82f, maxSpeed: 28f);
                ServoAimAt(npc, ctx.Target.Center, 0.1f);
            }
            else {
                //活塞回缩：反向蹬缩绷满压力，朝向冻结
                Vector2 vel = ctx.SpringVelocity * 0.7f - directionToPlayer * 1.6f;
                ctx.SpringVelocity = vel;
                npc.velocity = vel;
            }

            if (!VaultUtils.isServer) {
                if (Timer % 5 == 0) {
                    Vector2 particlePos = npc.Center + Main.rand.NextVector2Circular(40, 40);
                    Dust dust = Dust.NewDustDirect(particlePos, 1, 1, DustID.FireworkFountain_Red,
                        0, 0, 100, Color.Yellow, Main.rand.NextFloat(0.8f, 1.4f));
                    dust.velocity = (npc.Center - particlePos) * 0.1f;
                    dust.noGravity = true;
                }
                if (Timer == 10) {
                    SoundEngine.PlaySound(SoundID.Item61 with { Volume = 0.6f, Pitch = -0.3f }, npc.Center);
                }
            }

            Timer++;
            if (Timer >= duration && !VaultUtils.isClient) {
                return new ViceStrikeState();
            }
            return null;
        }
    }

    /// <summary>钳爪刚性突刺：硬咬合直线打出，命中钳口闭合+冲击波</summary>
    [InnoVault.StateMachines.VaultState((int)PrimeArmStateIndex.ViceStrike, typeof(PrimeArmStateContext))]
    internal class ViceStrikeState : PrimeArmStateBase
    {
        public override string StateName => "ViceStrike";
        public override PrimeArmStateIndex StateIndex => PrimeArmStateIndex.ViceStrike;

        private bool hasImpacted;

        public override void OnEnter(PrimeArmStateContext ctx) {
            base.OnEnter(ctx);
            hasImpacted = false;

            NPC npc = ctx.Npc;
            float chargeVelocity = (ctx.BossRush ? 20f : 16f) + ctx.MissingPartnerCount * 1.5f;
            if (ctx.Death) {
                chargeVelocity *= 1.2f;
            }
            Vector2 velocity = npc.Center.DirectionTo(ctx.Target.Center) * chargeVelocity;
            ctx.SpringVelocity = velocity;
            npc.velocity = velocity;
            //出手瞬间硬咬合朝向
            npc.rotation = velocity.ToRotation() - MathHelper.PiOver2;
            ctx.ClawOpen = true;

            if (!VaultUtils.isClient) {
                npc.netUpdate = true;
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.8f, Pitch = 0.2f }, npc.Center);
            }
        }

        public override PrimeArmStateBase OnUpdate(PrimeArmStateContext ctx) {
            NPC npc = ctx.Npc;
            npc.damage = npc.defDamage;

            npc.velocity = ctx.SpringVelocity * 0.95f;
            ctx.SpringVelocity = npc.velocity;
            //飞行全程朝向锁死：刚性直线突刺

            if (!VaultUtils.isServer && Timer % 2 == 0) {
                Vector2 trailPos = npc.Center + Main.rand.NextVector2Circular(30, 30);
                Dust dust = Dust.NewDustDirect(trailPos, 1, 1, DustID.FireworkFountain_Red,
                    -npc.velocity.X * 0.3f, -npc.velocity.Y * 0.3f, 100, Color.Cyan, Main.rand.NextFloat(1.0f, 1.8f));
                dust.noGravity = true;
            }

            float distanceToPlayer = npc.Distance(ctx.Target.Center);
            if (distanceToPlayer < 80f && !hasImpacted) {
                hasImpacted = true;
                OnImpact(ctx);
            }

            Timer++;
            if ((Timer >= 45 || distanceToPlayer > 1200f || npc.justHit) && !VaultUtils.isClient) {
                return new ViceRecoveryState();
            }
            return null;
        }

        private static void OnImpact(PrimeArmStateContext ctx) {
            ctx.ImpactIntensity = 8f;
            ctx.ClawOpen = false;//钳口轰然闭合
            if (VaultUtils.isServer) {
                return;
            }
            NPC npc = ctx.Npc;

            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1.2f, Pitch = -0.4f }, npc.Center);
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.8f }, npc.Center);

            for (int i = 0; i < 30; i++) {
                Vector2 particleVel = Main.rand.NextVector2Circular(8f, 8f);
                Dust dust = Dust.NewDustDirect(npc.Center - Vector2.One * 30, 60, 60,
                    Main.rand.Next(new int[] { DustID.FireworkFountain_Red, DustID.SteampunkSteam, DustID.Smoke }),
                    particleVel.X, particleVel.Y, 100, default, Main.rand.NextFloat(1.2f, 2.0f));
                dust.noGravity = true;
            }
            for (int i = 0; i < 20; i++) {
                float angle = MathHelper.TwoPi * i / 20f;
                Vector2 shockwaveVel = angle.ToRotationVector2() * Main.rand.NextFloat(4f, 9f);
                Dust dust = Dust.NewDustDirect(npc.Center, 1, 1, DustID.FireworkFountain_Red,
                    shockwaveVel.X, shockwaveVel.Y, 100, Color.Cyan, 1.5f);
                dust.noGravity = true;
                dust.fadeIn = 1.3f;
            }
        }
    }

    /// <summary>钳爪收势：减速回待机位</summary>
    [InnoVault.StateMachines.VaultState((int)PrimeArmStateIndex.ViceRecovery, typeof(PrimeArmStateContext))]
    internal class ViceRecoveryState : PrimeArmStateBase
    {
        public override string StateName => "ViceRecovery";
        public override PrimeArmStateIndex StateIndex => PrimeArmStateIndex.ViceRecovery;

        public override PrimeArmStateBase OnUpdate(PrimeArmStateContext ctx) {
            NPC npc = ctx.Npc;
            npc.damage = 0;
            ctx.ClawOpen = false;

            Vector2 returnAnchor = ctx.Head.Center + new Vector2(-180f * ctx.Side, 240f);
            SpringMove(ctx, returnAnchor, 0.9f, stiffness: 0.18f, damping: 0.82f, maxSpeed: 28f);

            //伺服回正到自然垂悬
            ServoRotate(npc, 0f, 0.08f);

            Timer++;
            int recoveryDuration = ctx.MasterMode ? 30 : 40;
            if (Timer >= recoveryDuration && !VaultUtils.isClient) {
                ctx.ChargeTimer = 0f;
                return new ViceIdleState();
            }
            return null;
        }
    }

    /// <summary>钳爪三连击：刺击→横扫→重锤，钳口随段开合</summary>
    [InnoVault.StateMachines.VaultState((int)PrimeArmStateIndex.ViceCombo, typeof(PrimeArmStateContext))]
    internal class ViceComboState : PrimeArmStateBase
    {
        public override string StateName => "ViceCombo";
        public override PrimeArmStateIndex StateIndex => PrimeArmStateIndex.ViceCombo;

        private int stageTimer;
        private bool stageLaunched;

        public override void OnEnter(PrimeArmStateContext ctx) {
            base.OnEnter(ctx);
            stageTimer = 0;
            stageLaunched = false;
        }

        public override PrimeArmStateBase OnUpdate(PrimeArmStateContext ctx) {
            switch (Counter) {
                case 0:
                    ExecuteJab(ctx);
                    break;
                case 1:
                    ExecuteSweep(ctx);
                    break;
                default:
                    ExecuteHeavy(ctx);
                    break;
            }

            stageTimer++;
            Timer++;

            int comboInterval = ctx.MasterMode ? 25 : 35;
            if (stageTimer >= comboInterval) {
                Counter++;
                stageTimer = 0;
                stageLaunched = false;
                if (Counter >= 3 && !VaultUtils.isClient) {
                    return new ViceRecoveryState();
                }
            }
            return null;
        }

        /// <summary>第一击：快速刺击，短锁定后直线打出</summary>
        private void ExecuteJab(PrimeArmStateContext ctx) {
            NPC npc = ctx.Npc;
            npc.damage = (int)(npc.defDamage * 0.8f);

            if (stageTimer < 10) {
                //就位 + 伺服锁定，钳口张开
                ctx.ClawOpen = true;
                Vector2 dirToPlayer = npc.Center.DirectionTo(ctx.Target.Center);
                SpringMove(ctx, ctx.Target.Center - dirToPlayer * 180f, 1.2f, stiffness: 0.18f, damping: 0.82f, maxSpeed: 28f);
                ServoAimAt(npc, ctx.Target.Center, 0.16f);
            }
            else {
                if (!stageLaunched) {
                    stageLaunched = true;
                    float speed = 18f + (ctx.Death ? 6f : 0f);
                    Vector2 velocity = npc.Center.DirectionTo(ctx.Target.Center) * speed;
                    ctx.SpringVelocity = velocity;
                    npc.rotation = velocity.ToRotation() - MathHelper.PiOver2;//硬咬合
                }
                //刚性刺出：朝向锁死
                npc.velocity = ctx.SpringVelocity;
                SpawnTrail(ctx, 3);
            }
        }

        /// <summary>第二击：弧线横扫，伺服跟随扫掠切线</summary>
        private void ExecuteSweep(PrimeArmStateContext ctx) {
            NPC npc = ctx.Npc;
            npc.damage = npc.defDamage;
            ctx.ClawOpen = true;

            Vector2 dirToPlayer = npc.Center.DirectionTo(ctx.Target.Center);
            float sweepAngle = MathHelper.Pi * 0.6f;
            float progress = stageTimer / 30f;

            Vector2 sweepDir = dirToPlayer.RotatedBy(-sweepAngle / 2f + sweepAngle * progress);
            SpringMove(ctx, ctx.Target.Center + sweepDir * 150f, 1.5f, stiffness: 0.18f, damping: 0.82f, maxSpeed: 28f);

            //钳口伺服咬住扫掠圆心的玩家
            ServoAimAt(npc, ctx.Target.Center, 0.2f);

            if (!VaultUtils.isServer && stageTimer % 2 == 0) {
                Dust dust = Dust.NewDustDirect(npc.Center, npc.width, npc.height, DustID.SteampunkSteam,
                    ctx.SpringVelocity.X * 0.2f, ctx.SpringVelocity.Y * 0.2f, 100, default, Main.rand.NextFloat(1.2f, 1.8f));
                dust.noGravity = true;
            }
        }

        /// <summary>第三击：后撤蓄压+重锤冲锋</summary>
        private void ExecuteHeavy(PrimeArmStateContext ctx) {
            NPC npc = ctx.Npc;
            npc.damage = (int)(npc.defDamage * 1.5f);

            if (stageTimer < 15) {
                //大幅后撤蓄压，钳口张到最大、伺服死锁
                ctx.ClawOpen = true;
                Vector2 dirToPlayer = npc.Center.DirectionTo(ctx.Target.Center);
                SpringMove(ctx, ctx.Target.Center - dirToPlayer * 320f, 1.0f, stiffness: 0.18f, damping: 0.82f, maxSpeed: 28f);
                ServoAimAt(npc, ctx.Target.Center, 0.14f);
            }
            else {
                if (!stageLaunched) {
                    stageLaunched = true;
                    float chargeSpeed = 28f + (ctx.Death ? 8f : 0f);
                    Vector2 velocity = npc.Center.DirectionTo(ctx.Target.Center) * chargeSpeed;
                    ctx.SpringVelocity = velocity;
                    npc.rotation = velocity.ToRotation() - MathHelper.PiOver2;//硬咬合
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1.0f, Pitch = -0.2f }, npc.Center);
                    }
                }
                //刚性重锤：朝向锁死
                npc.velocity = ctx.SpringVelocity * 0.96f;
                ctx.SpringVelocity = npc.velocity;
                if (!VaultUtils.isServer && stageTimer % 2 == 0) {
                    for (int i = 0; i < 2; i++) {
                        Dust dust = Dust.NewDustDirect(npc.Center - npc.velocity * 2f, 1, 1, DustID.Torch,
                            -npc.velocity.X * 0.4f, -npc.velocity.Y * 0.4f, 100, Color.OrangeRed, Main.rand.NextFloat(1.5f, 2.5f));
                        dust.noGravity = true;
                    }
                }
            }
        }

        private void SpawnTrail(PrimeArmStateContext ctx, int interval) {
            if (VaultUtils.isServer || stageTimer % interval != 0) {
                return;
            }
            NPC npc = ctx.Npc;
            Vector2 trailPos = npc.Center + Main.rand.NextVector2Circular(30, 30);
            Dust dust = Dust.NewDustDirect(trailPos, 1, 1, DustID.FireworkFountain_Red,
                -ctx.SpringVelocity.X * 0.3f, -ctx.SpringVelocity.Y * 0.3f, 100, Color.Cyan, Main.rand.NextFloat(1.0f, 1.8f));
            dust.noGravity = true;
        }
    }

    /// <summary>钳爪远程归队：过远折返头部</summary>
    [InnoVault.StateMachines.VaultState((int)PrimeArmStateIndex.ViceReturn, typeof(PrimeArmStateContext))]
    internal class ViceReturnState : PrimeArmStateBase
    {
        public override string StateName => "ViceReturn";
        public override PrimeArmStateIndex StateIndex => PrimeArmStateIndex.ViceReturn;

        public override PrimeArmStateBase OnUpdate(PrimeArmStateContext ctx) {
            NPC npc = ctx.Npc;
            npc.damage = 0;
            ctx.ClawOpen = false;

            AnchoredFollow(ctx, 20f, -20f, -20f, 20f);
            ctx.SpringVelocity = npc.velocity;

            //伺服回正到自然垂悬
            ServoRotate(npc, 0f, 0.08f);

            Timer++;
            if (Timer > 30 && IdleAnchorDistance(ctx) < 400f && !VaultUtils.isClient) {
                return new ViceIdleState();
            }
            return null;
        }
    }

    /// <summary>anticipation-snap 三连突刺</summary>
    [InnoVault.StateMachines.VaultState((int)PrimeArmStateIndex.ViceTripleLunge, typeof(PrimeArmStateContext))]
    internal class ViceTripleLungeState : PrimeArmStateBase
    {
        public override string StateName => "ViceTripleLunge";
        public override PrimeArmStateIndex StateIndex => PrimeArmStateIndex.ViceTripleLunge;

        private int phaseTimer;
        private int lungeIndex;
        private Vector2 lungeDir;

        public override PrimeArmStateBase OnUpdate(PrimeArmStateContext ctx) {
            NPC npc = ctx.Npc;
            ctx.ClawOpen = phaseTimer < 12;

            if (phaseTimer < 20) {
                npc.damage = 0;
                float t = phaseTimer / 20f;
                float ease = 1f - (float)System.Math.Pow(1f - t, 3);
                lungeDir = (ctx.Target.Center - npc.Center).SafeNormalize(Vector2.UnitY);
                npc.velocity = -lungeDir * ease * 4f;
            }
            else if (phaseTimer < 30) {
                npc.velocity = lungeDir * 22f;
                npc.damage = npc.defDamage * 2;
                ctx.ApplyRecoil(PrimeDirector.HeavyRecoil);
            }
            else {
                npc.velocity *= 0.8f;
                npc.damage = 0;
                if (phaseTimer >= 42) {
                    phaseTimer = 0;
                    lungeIndex++;
                }
            }

            phaseTimer++;
            Timer++;
            if (lungeIndex >= 3 && phaseTimer > 10 && !VaultUtils.isClient) {
                return new ViceRecoveryState();
            }
            return null;
        }
    }

    /// <summary>钳口闭合冲击波</summary>
    [InnoVault.StateMachines.VaultState((int)PrimeArmStateIndex.ViceClapWave, typeof(PrimeArmStateContext))]
    internal class ViceClapWaveState : PrimeArmStateBase
    {
        public override string StateName => "ViceClapWave";
        public override PrimeArmStateIndex StateIndex => PrimeArmStateIndex.ViceClapWave;

        public override PrimeArmStateBase OnUpdate(PrimeArmStateContext ctx) {
            NPC npc = ctx.Npc;
            SpringMove(ctx, ctx.Head.Center + new Vector2(-120f * ctx.Side, 220f), 0.8f);
            ctx.ClawOpen = Timer < 24;

            if (Timer == 24 && !VaultUtils.isClient && !ctx.DontAttack) {
                int damage = ScaleDamage(CWRRef.GetProjectileDamage(npc, ProjectileID.RocketSkeleton));
                for (int i = -2; i <= 2; i++) {
                    Vector2 vel = ctx.AimDirection.RotatedBy(i * 0.22f) * 8f;
                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center + vel * 30f, vel,
                        ModContent.ProjectileType<DeadLaser>(), damage, 0f, Main.myPlayer, 1f, 0f);
                }
                ctx.ApplyRecoil(PrimeDirector.HeavyRecoil);
                if (!VaultUtils.isServer) {
                    PrimeScreenEffects.PushShockRing(npc.Center, 0.6f, 320f, 16);
                }
            }

            Timer++;
            if (Timer > 50 && !VaultUtils.isClient) {
                return new ViceRecoveryState();
            }
            return null;
        }
    }
}
