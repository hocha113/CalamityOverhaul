using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.States.Arms
{
    /// <summary>
    /// 钳爪待机：弹簧物理悬浮在头部下侧，充能满后按
    /// "三连击 → 蓄力重锤 → 蓄力重锤"的确定性序列出招（死亡模式只出重锤）
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)PrimeArmStateIndex.ViceIdle, typeof(PrimeArmStateContext))]
    internal class ViceIdleState : PrimeArmStateBase
    {
        public override string StateName => "ViceIdle";
        public override PrimeArmStateIndex StateIndex => PrimeArmStateIndex.ViceIdle;

        public override PrimeArmStateBase OnUpdate(PrimeArmStateContext ctx) {
            NPC npc = ctx.Npc;
            npc.damage = 0;

            Vector2 idleAnchor = ctx.Head.Center + new Vector2(-150f * ctx.Side, 250f);
            SpringMove(ctx, idleAnchor, 0.7f, stiffness: 0.18f, damping: 0.82f, maxSpeed: 28f);

            Vector2 toTarget = idleAnchor - npc.Center;
            npc.rotation = toTarget.ToRotation() + MathHelper.PiOver2;

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

                int cycle = ctx.AttackCycle;
                ctx.AttackCycle = (cycle + 1) % 3;
                if (cycle == 0 && !ctx.Death) {
                    return new ViceComboState();
                }
                return new ViceWindUpState();
            }
            return null;
        }
    }

    /// <summary>
    /// 钳爪后撤蓄力：拉开距离绷紧机械臂，蓄势完成后猛扑
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)PrimeArmStateIndex.ViceWindUp, typeof(PrimeArmStateContext))]
    internal class ViceWindUpState : PrimeArmStateBase
    {
        public override string StateName => "ViceWindUp";
        public override PrimeArmStateIndex StateIndex => PrimeArmStateIndex.ViceWindUp;

        private const float WindUpDistance = 280f;

        public override PrimeArmStateBase OnUpdate(PrimeArmStateContext ctx) {
            NPC npc = ctx.Npc;
            npc.damage = 0;

            Vector2 directionToPlayer = npc.Center.DirectionTo(ctx.Target.Center);
            Vector2 windUpPos = ctx.Target.Center - directionToPlayer * WindUpDistance;
            SpringMove(ctx, windUpPos, 1.3f, stiffness: 0.18f, damping: 0.82f, maxSpeed: 28f);
            npc.rotation = directionToPlayer.ToRotation() + MathHelper.PiOver2;

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
            int windUpDuration = ctx.Death ? 15 : (ctx.MasterMode ? 20 : 25);
            if (Timer >= windUpDuration && !VaultUtils.isClient) {
                return new ViceStrikeState();
            }
            return null;
        }
    }

    /// <summary>
    /// 钳爪猛扑：直线高速突刺，命中瞬间迸发冲击波反馈
    /// </summary>
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
            npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;

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

    /// <summary>
    /// 钳爪收势：突刺减速回稳，返回待机位重新装填
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)PrimeArmStateIndex.ViceRecovery, typeof(PrimeArmStateContext))]
    internal class ViceRecoveryState : PrimeArmStateBase
    {
        public override string StateName => "ViceRecovery";
        public override PrimeArmStateIndex StateIndex => PrimeArmStateIndex.ViceRecovery;

        public override PrimeArmStateBase OnUpdate(PrimeArmStateContext ctx) {
            NPC npc = ctx.Npc;
            npc.damage = 0;

            Vector2 returnAnchor = ctx.Head.Center + new Vector2(-180f * ctx.Side, 240f);
            SpringMove(ctx, returnAnchor, 0.9f, stiffness: 0.18f, damping: 0.82f, maxSpeed: 28f);

            Vector2 toTarget = returnAnchor - npc.Center;
            float targetRot = toTarget.ToRotation() + MathHelper.PiOver2;
            npc.rotation = MathHelper.Lerp(npc.rotation, targetRot, 0.15f);

            Timer++;
            int recoveryDuration = ctx.MasterMode ? 30 : 40;
            if (Timer >= recoveryDuration && !VaultUtils.isClient) {
                ctx.ChargeTimer = 0f;
                return new ViceIdleState();
            }
            return null;
        }
    }

    /// <summary>
    /// 钳爪三连击：刺击 → 横扫 → 重锤的递进连段，
    /// 每一击的蓄力与节奏都不同，压迫感层层加码
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)PrimeArmStateIndex.ViceCombo, typeof(PrimeArmStateContext))]
    internal class ViceComboState : PrimeArmStateBase
    {
        public override string StateName => "ViceCombo";
        public override PrimeArmStateIndex StateIndex => PrimeArmStateIndex.ViceCombo;

        private int stageTimer;

        public override void OnEnter(PrimeArmStateContext ctx) {
            base.OnEnter(ctx);
            stageTimer = 0;
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
                if (Counter >= 3 && !VaultUtils.isClient) {
                    return new ViceRecoveryState();
                }
            }
            return null;
        }

        /// <summary>第一击：快速刺击</summary>
        private void ExecuteJab(PrimeArmStateContext ctx) {
            NPC npc = ctx.Npc;
            npc.damage = (int)(npc.defDamage * 0.8f);

            Vector2 dirToPlayer = npc.Center.DirectionTo(ctx.Target.Center);
            if (stageTimer < 10) {
                SpringMove(ctx, ctx.Target.Center - dirToPlayer * 180f, 1.2f, stiffness: 0.18f, damping: 0.82f, maxSpeed: 28f);
            }
            else {
                float speed = 18f + (ctx.Death ? 6f : 0f);
                Vector2 velocity = dirToPlayer * speed;
                ctx.SpringVelocity = velocity;
                npc.velocity = velocity;
                SpawnTrail(ctx, 3);
            }
            npc.rotation = dirToPlayer.ToRotation() + MathHelper.PiOver2;
        }

        /// <summary>第二击：弧线横扫</summary>
        private void ExecuteSweep(PrimeArmStateContext ctx) {
            NPC npc = ctx.Npc;
            npc.damage = npc.defDamage;

            Vector2 dirToPlayer = npc.Center.DirectionTo(ctx.Target.Center);
            float sweepAngle = MathHelper.Pi * 0.6f;
            float progress = stageTimer / 30f;

            Vector2 sweepDir = dirToPlayer.RotatedBy(-sweepAngle / 2f + sweepAngle * progress);
            SpringMove(ctx, ctx.Target.Center + sweepDir * 150f, 1.5f, stiffness: 0.18f, damping: 0.82f, maxSpeed: 28f);
            npc.rotation = sweepDir.ToRotation() + MathHelper.PiOver2;

            if (!VaultUtils.isServer && stageTimer % 2 == 0) {
                Dust dust = Dust.NewDustDirect(npc.Center, npc.width, npc.height, DustID.SteampunkSteam,
                    ctx.SpringVelocity.X * 0.2f, ctx.SpringVelocity.Y * 0.2f, 100, default, Main.rand.NextFloat(1.2f, 1.8f));
                dust.noGravity = true;
            }
        }

        /// <summary>第三击：大幅后撤 + 重锤冲锋</summary>
        private void ExecuteHeavy(PrimeArmStateContext ctx) {
            NPC npc = ctx.Npc;
            npc.damage = (int)(npc.defDamage * 1.5f);

            Vector2 dirToPlayer = npc.Center.DirectionTo(ctx.Target.Center);
            if (stageTimer < 15) {
                SpringMove(ctx, ctx.Target.Center - dirToPlayer * 320f, 1.0f, stiffness: 0.18f, damping: 0.82f, maxSpeed: 28f);
            }
            else if (stageTimer == 15) {
                float chargeSpeed = 28f + (ctx.Death ? 8f : 0f);
                Vector2 velocity = dirToPlayer * chargeSpeed;
                ctx.SpringVelocity = velocity;
                npc.velocity = velocity;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1.0f, Pitch = -0.2f }, npc.Center);
                }
            }
            else {
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
            npc.rotation = dirToPlayer.ToRotation() + MathHelper.PiOver2;
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

    /// <summary>
    /// 钳爪远程归队：飞得太远时全速折返头部
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)PrimeArmStateIndex.ViceReturn, typeof(PrimeArmStateContext))]
    internal class ViceReturnState : PrimeArmStateBase
    {
        public override string StateName => "ViceReturn";
        public override PrimeArmStateIndex StateIndex => PrimeArmStateIndex.ViceReturn;

        public override PrimeArmStateBase OnUpdate(PrimeArmStateContext ctx) {
            NPC npc = ctx.Npc;
            npc.damage = 0;

            AnchoredFollow(ctx, 20f, -20f, -20f, 20f);
            ctx.SpringVelocity = npc.velocity;

            Vector2 toHead = ctx.Head.Center - npc.Center;
            npc.rotation = toHead.ToRotation() + MathHelper.PiOver2;

            Timer++;
            if (Timer > 30 && IdleAnchorDistance(ctx) < 400f && !VaultUtils.isClient) {
                return new ViceIdleState();
            }
            return null;
        }
    }
}
