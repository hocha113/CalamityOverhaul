using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Projectiles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.States.Arms
{
    //钳爪准则
    //转向ServoRotate，rotation=θ-PiOver2
    //突刺=液压活塞；ClawOpen驱动帧
    /// <summary>钳爪待机</summary>
    [InnoVault.StateMachines.VaultState((int)PrimeArmStateIndex.ViceIdle, typeof(PrimeArmStateContext))]
    internal class ViceIdleState : PrimeArmStateBase
    {
        public override string StateName => "ViceIdle";
        public override PrimeArmStateIndex StateIndex => PrimeArmStateIndex.ViceIdle;

        public override PrimeArmStateBase OnUpdate(PrimeArmStateContext ctx) {
            NPC npc = ctx.Npc;
            npc.damage = 0;
            ctx.ClawOpen = false;

            //液压浮沉
            float bob = (float)System.Math.Sin((Main.GameUpdateCount + npc.whoAmI * 37) * 0.045f) * 10f;
            Vector2 idleAnchor = ctx.Head.Center + new Vector2(-150f * ctx.Side, 250f + bob);
            SpringMove(ctx, idleAnchor, 0.7f, stiffness: 0.18f, damping: 0.82f, maxSpeed: 28f);

            //钳口咬向玩家
            ServoAimAt(npc, ctx.Target.Center, 0.03f);

            float chargeRate = ctx.MasterMode ? 2f : 1f;
            if (ctx.Asura) {
                chargeRate *= PrimeDirector.DeathChargeMultiplier;
            }
            chargeRate += ctx.MissingPartnerCount * PrimeDirector.MissingLimbChargeBonus;
            ctx.ChargeTimer += chargeRate;

            int threshold = PrimeDirector.GetArmChargeThreshold(ctx.MasterMode, ctx.Asura);
            if (ctx.ChargeTimer >= threshold && !VaultUtils.isClient && !ctx.DontAttack) {
                ctx.ChargeTimer = 0f;
                npc.TargetClosest();
                npc.netUpdate = true;

                if (HeadPrimeAI.GetActiveCommand(ctx.Head) == PrimeCommandKind.PhysicalAssault) {
                    //投技就绪时把指令突进升格为处刑突进（服务端判定）
                    if (HeadPrimeAI.ViceExecutionReady(ctx.Head, ctx.Target)) {
                        return new ViceExecutionLungeState();
                    }
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

    /// <summary>钳爪后撤蓄力</summary>
    [InnoVault.StateMachines.VaultState((int)PrimeArmStateIndex.ViceWindUp, typeof(PrimeArmStateContext))]
    internal class ViceWindUpState : PrimeArmStateBase
    {
        public override string StateName => "ViceWindUp";
        public override PrimeArmStateIndex StateIndex => PrimeArmStateIndex.ViceWindUp;

        internal static float WindUpDistance => 280f;
        /// <summary>活塞回缩帧</summary>
        internal static int CoilFrames => 5;

        private int WindUpDuration(PrimeArmStateContext ctx) => ctx.Asura ? 16 : (ctx.MasterMode ? 21 : 26);

        public override PrimeArmStateBase OnUpdate(PrimeArmStateContext ctx) {
            NPC npc = ctx.Npc;
            npc.damage = 0;
            ctx.ClawOpen = true;

            Vector2 directionToPlayer = npc.Center.DirectionTo(ctx.Target.Center);
            int duration = WindUpDuration(ctx);

            if (Timer < duration - CoilFrames) {
                //后撤出手位
                Vector2 windUpPos = ctx.Target.Center - directionToPlayer * WindUpDistance;
                SpringMove(ctx, windUpPos, 1.3f, stiffness: 0.18f, damping: 0.82f, maxSpeed: 28f);
                ServoAimAt(npc, ctx.Target.Center, 0.1f);
            }
            else {
                //活塞回缩
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

    /// <summary>钳爪刚性突刺</summary>
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
            if (ctx.Asura) {
                chargeVelocity *= 1.2f;
            }
            Vector2 velocity = npc.Center.DirectionTo(ctx.Target.Center) * chargeVelocity;
            ctx.SpringVelocity = velocity;
            npc.velocity = velocity;
            //硬咬合朝向
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
            //朝向锁死

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

    /// <summary>钳爪收势</summary>
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

            //伺服回正
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

    /// <summary>钳爪三连击</summary>
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

        /// <summary>第一击刺击</summary>
        private void ExecuteJab(PrimeArmStateContext ctx) {
            NPC npc = ctx.Npc;
            npc.damage = (int)(npc.defDamage * 0.8f);

            if (stageTimer < 10) {
                //就位锁定
                ctx.ClawOpen = true;
                Vector2 dirToPlayer = npc.Center.DirectionTo(ctx.Target.Center);
                SpringMove(ctx, ctx.Target.Center - dirToPlayer * 180f, 1.2f, stiffness: 0.18f, damping: 0.82f, maxSpeed: 28f);
                ServoAimAt(npc, ctx.Target.Center, 0.16f);
            }
            else {
                if (!stageLaunched) {
                    stageLaunched = true;
                    float speed = 18f + (ctx.Asura ? 6f : 0f);
                    Vector2 velocity = npc.Center.DirectionTo(ctx.Target.Center) * speed;
                    ctx.SpringVelocity = velocity;
                    npc.rotation = velocity.ToRotation() - MathHelper.PiOver2;//硬咬合
                }
                //刚性刺出
                npc.velocity = ctx.SpringVelocity;
                SpawnTrail(ctx, 3);
            }
        }

        /// <summary>第二击横扫</summary>
        private void ExecuteSweep(PrimeArmStateContext ctx) {
            NPC npc = ctx.Npc;
            npc.damage = npc.defDamage;
            ctx.ClawOpen = true;

            Vector2 dirToPlayer = npc.Center.DirectionTo(ctx.Target.Center);
            float sweepAngle = MathHelper.Pi * 0.6f;
            float progress = stageTimer / 30f;

            Vector2 sweepDir = dirToPlayer.RotatedBy(-sweepAngle / 2f + sweepAngle * progress);
            SpringMove(ctx, ctx.Target.Center + sweepDir * 150f, 1.5f, stiffness: 0.18f, damping: 0.82f, maxSpeed: 28f);

            //咬扫掠圆心
            ServoAimAt(npc, ctx.Target.Center, 0.2f);

            if (!VaultUtils.isServer && stageTimer % 2 == 0) {
                Dust dust = Dust.NewDustDirect(npc.Center, npc.width, npc.height, DustID.SteampunkSteam,
                    ctx.SpringVelocity.X * 0.2f, ctx.SpringVelocity.Y * 0.2f, 100, default, Main.rand.NextFloat(1.2f, 1.8f));
                dust.noGravity = true;
            }
        }

        /// <summary>第三击重锤</summary>
        private void ExecuteHeavy(PrimeArmStateContext ctx) {
            NPC npc = ctx.Npc;
            npc.damage = (int)(npc.defDamage * 1.5f);

            if (stageTimer < 15) {
                //后撤蓄压
                ctx.ClawOpen = true;
                Vector2 dirToPlayer = npc.Center.DirectionTo(ctx.Target.Center);
                SpringMove(ctx, ctx.Target.Center - dirToPlayer * 320f, 1.0f, stiffness: 0.18f, damping: 0.82f, maxSpeed: 28f);
                ServoAimAt(npc, ctx.Target.Center, 0.14f);
            }
            else {
                if (!stageLaunched) {
                    stageLaunched = true;
                    float chargeSpeed = 28f + (ctx.Asura ? 8f : 0f);
                    Vector2 velocity = npc.Center.DirectionTo(ctx.Target.Center) * chargeSpeed;
                    ctx.SpringVelocity = velocity;
                    npc.rotation = velocity.ToRotation() - MathHelper.PiOver2;//硬咬合
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1.0f, Pitch = -0.2f }, npc.Center);
                    }
                }
                //刚性重锤
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

    /// <summary>钳爪远程归队</summary>
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

            //伺服回正
            ServoRotate(npc, 0f, 0.08f);

            Timer++;
            if (Timer > 30 && IdleAnchorDistance(ctx) < 400f && !VaultUtils.isClient) {
                return new ViceIdleState();
            }
            return null;
        }
    }

    /// <summary>anticipation-snap三连</summary>
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

    /// <summary>
    /// 处刑突进（投技起手）：45帧专属前摇（钳口大开+红色蓄能汇聚+警报）→ 16帧预判直线突刺 →
    /// 命中锁定目标即交由头部切入投技演出；空振则合钳硬直留惩罚窗口。
    /// 全程无接触伤害，抓取判定与可见突刺精确对齐
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)PrimeArmStateIndex.ViceExecutionLunge, typeof(PrimeArmStateContext))]
    internal class ViceExecutionLungeState : PrimeArmStateBase
    {
        public override string StateName => "ViceExecutionLunge";
        public override PrimeArmStateIndex StateIndex => PrimeArmStateIndex.ViceExecutionLunge;

        internal const int TelegraphFrames = 45;
        internal const int DashFrames = 16;
        internal const int WhiffFrames = 26;

        private Vector2 lungeDir;
        private bool grabConfirmed;

        public override void OnEnter(PrimeArmStateContext ctx) {
            base.OnEnter(ctx);
            lungeDir = Vector2.Zero;
            grabConfirmed = false;
        }

        public override PrimeArmStateBase OnUpdate(PrimeArmStateContext ctx) {
            NPC npc = ctx.Npc;
            //全程无接触伤害：命中即转投技，空振即惩罚
            npc.damage = 0;

            if (Timer < TelegraphFrames) {
                UpdateTelegraph(ctx);
            }
            else if (Timer < TelegraphFrames + DashFrames) {
                PrimeArmStateBase next = UpdateDash(ctx);
                if (next != null) {
                    return next;
                }
            }
            else {
                UpdateWhiff(ctx);
            }

            Timer++;
            //空振硬直结束或超时兜底
            if (Timer >= TelegraphFrames + DashFrames + WhiffFrames && !VaultUtils.isClient) {
                return new ViceRecoveryState();
            }
            return null;
        }

        /// <summary>专属前摇：后撤出手位+蓄能汇聚+警报，末8帧活塞回缩</summary>
        private void UpdateTelegraph(PrimeArmStateContext ctx) {
            NPC npc = ctx.Npc;
            ctx.ClawOpen = true;
            Vector2 dirToPlayer = npc.Center.DirectionTo(ctx.Target.Center);

            if (Timer < TelegraphFrames - 8) {
                Vector2 windUpPos = ctx.Target.Center - dirToPlayer * 300f;
                SpringMove(ctx, windUpPos, 1.15f, stiffness: 0.18f, damping: 0.82f, maxSpeed: 28f);
                ServoAimAt(npc, ctx.Target.Center, 0.12f);
            }
            else {
                //活塞回缩，爆发前反向蓄压
                Vector2 vel = ctx.SpringVelocity * 0.7f - dirToPlayer * 1.8f;
                ctx.SpringVelocity = vel;
                npc.velocity = vel;
            }

            if (VaultUtils.isServer) {
                return;
            }

            //红色蓄能向钳口汇聚
            float charge = Timer / (float)TelegraphFrames;
            if (Timer % 3 == 0) {
                Vector2 jaw = npc.Center + ctx.AimDirection * 40f;
                Vector2 pos = jaw + Main.rand.NextVector2Circular(52f * (1f - charge * 0.5f), 52f * (1f - charge * 0.5f));
                Dust dust = Dust.NewDustDirect(pos, 1, 1, DustID.FireworkFountain_Red,
                    0, 0, 100, Color.Red, Main.rand.NextFloat(0.9f, 1.6f));
                dust.velocity = (jaw - pos) * 0.14f;
                dust.noGravity = true;
            }
            Lighting.AddLight(npc.Center, new Vector3(0.9f, 0.15f, 0.1f) * charge);

            //液压蓄压与双响警报
            if (Timer == 6) {
                SoundEngine.PlaySound(SoundID.Item61 with { Volume = 0.8f, Pitch = -0.5f }, npc.Center);
            }
            if (Timer == 24 || Timer == 36) {
                SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.9f, Pitch = Timer == 24 ? -0.1f : 0.25f }, npc.Center);
            }
        }

        /// <summary>预判直线突刺，命中锁定目标即开投技</summary>
        private PrimeArmStateBase UpdateDash(PrimeArmStateContext ctx) {
            NPC npc = ctx.Npc;
            ctx.ClawOpen = true;

            if (lungeDir == Vector2.Zero) {
                //出手瞬间锁死预判弹道，突刺全程不转向
                Vector2 predict = ctx.Target.Center + ctx.Target.velocity * 9f;
                lungeDir = npc.Center.DirectionTo(predict);
                float speed = ctx.Asura ? 28f : 26f;
                ctx.SpringVelocity = lungeDir * speed;
                npc.rotation = lungeDir.ToRotation() - MathHelper.PiOver2;
                if (!VaultUtils.isClient) {
                    npc.netUpdate = true;
                }
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item71 with { Volume = 1f, Pitch = 0.1f }, npc.Center);
                }
            }
            npc.velocity = ctx.SpringVelocity;

            //突刺尾迹
            if (!VaultUtils.isServer && Timer % 2 == 0) {
                Vector2 trailPos = npc.Center + Main.rand.NextVector2Circular(26f, 26f);
                Dust dust = Dust.NewDustDirect(trailPos, 1, 1, DustID.FireworkFountain_Red,
                    -npc.velocity.X * 0.3f, -npc.velocity.Y * 0.3f, 100, Color.Red, Main.rand.NextFloat(1.1f, 1.8f));
                dust.noGravity = true;
            }

            //抓取判定：服务端，仅锁定目标，窗口与突刺帧精确对齐
            if (!VaultUtils.isClient && !grabConfirmed && npc.Hitbox.Intersects(ctx.Target.Hitbox)) {
                HeadPrimeAI headAI = ctx.Head.GetOverride<HeadPrimeAI>();
                if (headAI != null && headAI.TryBeginViceExecution(ctx.Target.whoAmI, npc)) {
                    grabConfirmed = true;
                    ctx.ClawOpen = false;
                    ctx.ImpactIntensity = 10f;
                    //编排层下帧起接管钳臂
                    return new ViceRecoveryState();
                }
            }
            return null;
        }

        /// <summary>空振：空钳轰合+微坠硬直，惩罚窗口</summary>
        private void UpdateWhiff(PrimeArmStateContext ctx) {
            NPC npc = ctx.Npc;

            if (Timer == TelegraphFrames + DashFrames) {
                ctx.ClawOpen = false;
                ctx.ImpactIntensity = 6f;
                if (!VaultUtils.isServer) {
                    //空钳 CLANK
                    SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 1f, Pitch = 0.3f }, npc.Center);
                    for (int i = 0; i < 10; i++) {
                        Dust dust = Dust.NewDustDirect(npc.Center + ctx.AimDirection * 40f, 1, 1,
                            DustID.FireworkFountain_Red, 0, 0, 100, Color.Yellow, Main.rand.NextFloat(0.8f, 1.3f));
                        dust.velocity = Main.rand.NextVector2Circular(4f, 4f);
                        dust.noGravity = true;
                    }
                }
                //空振短冷却后再试
                if (!VaultUtils.isClient) {
                    HeadPrimeAI headAI = ctx.Head.GetOverride<HeadPrimeAI>();
                    if (headAI != null && headAI.viceExecutionCooldown < ViceExecutionLungeState.WhiffCooldownFrames) {
                        headAI.viceExecutionCooldown = ViceExecutionLungeState.WhiffCooldownFrames;
                    }
                }
            }

            //微坠硬直
            npc.velocity *= 0.85f;
            npc.velocity.Y += 0.12f;
            ctx.SpringVelocity = npc.velocity;
        }

        /// <summary>空振冷却帧</summary>
        internal const int WhiffCooldownFrames = 600;
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
