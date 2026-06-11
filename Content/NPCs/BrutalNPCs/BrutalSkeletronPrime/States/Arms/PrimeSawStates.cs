using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.States.Arms
{
    /// <summary>
    /// 电锯待机：锯片低速空转，弹簧物理悬浮在头部下侧，
    /// 充能满后按"连冲 → 环绕 → 钻击"的确定性序列出招
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)PrimeArmStateIndex.SawIdle, typeof(PrimeArmStateContext))]
    internal class SawIdleState : PrimeArmStateBase
    {
        public override string StateName => "SawIdle";
        public override PrimeArmStateIndex StateIndex => PrimeArmStateIndex.SawIdle;

        public override PrimeArmStateBase OnUpdate(PrimeArmStateContext ctx) {
            NPC npc = ctx.Npc;
            npc.damage = 0;
            ctx.TargetSpinSpeed = 0.05f;

            Vector2 idleAnchor = ctx.Head.Center + new Vector2(-125f * ctx.Side, 290f);
            SpringMove(ctx, idleAnchor, 0.65f, stiffness: 0.16f, damping: 0.84f, maxSpeed: 30f);

            Vector2 toTarget = idleAnchor - npc.Center;
            npc.rotation = MathHelper.Lerp(npc.rotation, toTarget.ToRotation() + MathHelper.PiOver2, 0.1f);

            //充能（失去同伴后加速）
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
                if (cycle == 0 || ctx.Death) {
                    return new SawSpinUpState();
                }
                if (cycle == 1) {
                    return new SawOrbitState();
                }
                return new SawDrillState();
            }
            return null;
        }
    }

    /// <summary>
    /// 电锯狂转蓄势：锯片急速旋转，逼近玩家侧翼蓄势，给出明确的冲刺预告
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)PrimeArmStateIndex.SawSpinUp, typeof(PrimeArmStateContext))]
    internal class SawSpinUpState : PrimeArmStateBase
    {
        public override string StateName => "SawSpinUp";
        public override PrimeArmStateIndex StateIndex => PrimeArmStateIndex.SawSpinUp;

        private bool playedSpinSound;

        public override void OnEnter(PrimeArmStateContext ctx) {
            base.OnEnter(ctx);
            playedSpinSound = false;
        }

        public override PrimeArmStateBase OnUpdate(PrimeArmStateContext ctx) {
            NPC npc = ctx.Npc;
            npc.damage = 0;
            ctx.TargetSpinSpeed = 0.8f;

            //逼近至玩家近侧
            Vector2 dirToPlayer = npc.Center.DirectionTo(ctx.Target.Center);
            SpringMove(ctx, ctx.Target.Center - dirToPlayer * 200f, 1.1f, stiffness: 0.16f, damping: 0.84f, maxSpeed: 30f);
            npc.rotation += ctx.SpinSpeed * 2f;

            if (!VaultUtils.isServer && Timer % 3 == 0) {
                Vector2 particlePos = npc.Center + Main.rand.NextVector2Circular(35, 35);
                Dust dust = Dust.NewDustDirect(particlePos, 1, 1, DustID.FireworkFountain_Red,
                    0, 0, 100, Color.Yellow * 0.8f, Main.rand.NextFloat(0.8f, 1.3f));
                dust.velocity = (npc.Center - particlePos).RotatedBy(MathHelper.PiOver2) * 0.15f;
                dust.noGravity = true;
            }

            if (Timer == 15 && !playedSpinSound && !VaultUtils.isServer) {
                playedSpinSound = true;
                SoundEngine.PlaySound(SoundID.Item22 with { Volume = 0.7f, Pitch = -0.2f }, npc.Center);
            }

            Timer++;
            int spinUpDuration = ctx.Death ? 20 : (ctx.MasterMode ? 25 : 35);
            if (Timer >= spinUpDuration && !VaultUtils.isClient) {
                return new SawDashState();
            }
            return null;
        }
    }

    /// <summary>
    /// 电锯冲刺连段：高速锯切突进，命中或飞远后短暂回正再次突进，
    /// 同伴越少连段越长
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)PrimeArmStateIndex.SawDash, typeof(PrimeArmStateContext))]
    internal class SawDashState : PrimeArmStateBase
    {
        public override string StateName => "SawDash";
        public override PrimeArmStateIndex StateIndex => PrimeArmStateIndex.SawDash;

        //0=突进 1=回正再瞄
        private int subPhase;
        private int phaseTimer;

        public override void OnEnter(PrimeArmStateContext ctx) {
            base.OnEnter(ctx);
            subPhase = 0;
            phaseTimer = 0;
            LaunchDash(ctx);
        }

        public override PrimeArmStateBase OnUpdate(PrimeArmStateContext ctx) {
            NPC npc = ctx.Npc;

            if (subPhase == 0) {
                UpdateDash(ctx);
            }
            else {
                UpdateReAim(ctx);
            }

            phaseTimer++;
            Timer++;

            if (Counter >= MaxDashes(ctx) && subPhase != 0 && !VaultUtils.isClient) {
                return new SawRecoveryState();
            }
            return null;
        }

        private int MaxDashes(PrimeArmStateContext ctx) {
            int maxDashes = 3 + ctx.MissingPartnerCount;
            if (ctx.Death) {
                maxDashes += 2;
            }
            return maxDashes;
        }

        private void LaunchDash(PrimeArmStateContext ctx) {
            NPC npc = ctx.Npc;
            float dashSpeed = (ctx.BossRush ? 27.5f : 22f) + ctx.MissingPartnerCount * 2f;
            if (ctx.Death) {
                dashSpeed *= 1.2f;
            }
            Vector2 velocity = npc.Center.DirectionTo(ctx.Target.Center) * dashSpeed;
            ctx.SpringVelocity = velocity;
            npc.velocity = velocity;
            if (!VaultUtils.isClient) {
                npc.netUpdate = true;
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.9f, Pitch = 0.4f }, npc.Center);
            }
        }

        private void UpdateDash(PrimeArmStateContext ctx) {
            NPC npc = ctx.Npc;
            npc.damage = npc.defDamage;
            ctx.TargetSpinSpeed = 1.2f;
            npc.velocity = ctx.SpringVelocity;
            npc.rotation = ctx.SpringVelocity.ToRotation() - MathHelper.PiOver2;

            //锯切尾迹
            if (!VaultUtils.isServer && phaseTimer % 2 == 0) {
                Vector2 trailPos = npc.Center - ctx.SpringVelocity.SafeNormalize(Vector2.Zero) * 40f;
                Dust dust = Dust.NewDustDirect(trailPos, 1, 1, DustID.FireworkFountain_Red,
                    -ctx.SpringVelocity.X * 0.2f, -ctx.SpringVelocity.Y * 0.2f, 100, Color.Cyan, Main.rand.NextFloat(1.2f, 2.0f));
                dust.noGravity = true;
                dust.fadeIn = 1.1f;
            }

            bool shouldEnd = npc.justHit || phaseTimer >= 50 || npc.Distance(ctx.Target.Center) > 1400f;
            if (shouldEnd) {
                Counter++;
                subPhase = 1;
                phaseTimer = 0;
            }
        }

        private void UpdateReAim(PrimeArmStateContext ctx) {
            NPC npc = ctx.Npc;
            npc.damage = 0;
            ctx.TargetSpinSpeed = 0.8f;

            //短促回正，再次锁定
            Vector2 vel = ctx.SpringVelocity * 0.88f;
            ctx.SpringVelocity = vel;
            npc.velocity = vel;
            npc.rotation += ctx.SpinSpeed * 2f;

            if (phaseTimer >= 16 && Counter < MaxDashes(ctx)) {
                if (!VaultUtils.isClient) {
                    npc.TargetClosest();
                }
                subPhase = 0;
                phaseTimer = 0;
                LaunchDash(ctx);
            }
        }
    }

    /// <summary>
    /// 电锯环绕绞杀：以玩家为轴心快速环绕收紧，封锁走位空间
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)PrimeArmStateIndex.SawOrbit, typeof(PrimeArmStateContext))]
    internal class SawOrbitState : PrimeArmStateBase
    {
        public override string StateName => "SawOrbit";
        public override PrimeArmStateIndex StateIndex => PrimeArmStateIndex.SawOrbit;

        private float orbitAngle;
        private float orbitRadius;

        public override void OnEnter(PrimeArmStateContext ctx) {
            base.OnEnter(ctx);
            orbitAngle = ctx.Npc.Center.AngleTo(ctx.Target.Center);
            orbitRadius = 280f;
        }

        public override PrimeArmStateBase OnUpdate(PrimeArmStateContext ctx) {
            NPC npc = ctx.Npc;
            npc.damage = npc.defDamage;
            ctx.TargetSpinSpeed = 0.5f;

            orbitAngle += (ctx.MasterMode ? 0.12f : 0.09f) * (ctx.Death ? 1.5f : 1f);
            orbitRadius = MathHelper.Lerp(orbitRadius, 180f, 0.05f);

            Vector2 orbitTarget = ctx.Target.Center + orbitAngle.ToRotationVector2() * orbitRadius;
            SpringMove(ctx, orbitTarget, 1.4f, stiffness: 0.16f, damping: 0.84f, maxSpeed: 30f);
            npc.rotation = ctx.SpringVelocity.ToRotation() - MathHelper.PiOver2;

            if (!VaultUtils.isServer) {
                if (Timer % 4 == 0) {
                    Vector2 particleVel = ctx.SpringVelocity.RotatedBy(MathHelper.PiOver2) * 0.3f;
                    Dust dust = Dust.NewDustDirect(npc.Center, npc.width, npc.height, DustID.SteampunkSteam,
                        particleVel.X, particleVel.Y, 100, default, Main.rand.NextFloat(1.0f, 1.6f));
                    dust.noGravity = true;
                }
                if (Timer % 60 == 0) {
                    SoundEngine.PlaySound(SoundID.Item22 with { Volume = 0.5f, Pitch = 0.1f }, npc.Center);
                }
            }

            Timer++;
            int orbitDuration = ctx.Death ? 120 : (ctx.MasterMode ? 180 : 240);
            if ((Timer >= orbitDuration || npc.justHit) && !VaultUtils.isClient) {
                return new SawRecoveryState();
            }
            return null;
        }
    }

    /// <summary>
    /// 电锯钻击追猎：持续预判追击玩家落点，受击会被打断节奏提前收势
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)PrimeArmStateIndex.SawDrill, typeof(PrimeArmStateContext))]
    internal class SawDrillState : PrimeArmStateBase
    {
        public override string StateName => "SawDrill";
        public override PrimeArmStateIndex StateIndex => PrimeArmStateIndex.SawDrill;

        private float drillTimer;

        public override PrimeArmStateBase OnUpdate(PrimeArmStateContext ctx) {
            NPC npc = ctx.Npc;
            npc.damage = npc.defDamage;
            ctx.TargetSpinSpeed = 1.0f;

            //预判追击
            Vector2 predictedPos = ctx.Target.Center + ctx.Target.velocity * 10f;
            float acceleration = ctx.BossRush ? 0.3f : (ctx.Death ? 0.1f : 0.08f);
            if (ctx.MasterMode) {
                acceleration *= 1.25f;
            }

            float drillSpeed = (ctx.BossRush ? 13.5f : 11f) + ctx.MissingPartnerCount * 1.5f;
            if (ctx.MasterMode) {
                drillSpeed *= 1.25f;
            }

            Vector2 targetVel = (predictedPos - npc.Center).SafeNormalize(Vector2.UnitX) * drillSpeed;
            Vector2 velocity = Vector2.Lerp(ctx.SpringVelocity, targetVel, acceleration);
            ctx.SpringVelocity = velocity;
            npc.velocity = velocity;
            npc.rotation = velocity.ToRotation() - MathHelper.PiOver2;

            if (!VaultUtils.isServer && Timer % 2 == 0) {
                for (int i = 0; i < 2; i++) {
                    Vector2 particlePos = npc.Center + Main.rand.NextVector2Circular(25, 25);
                    Vector2 particleVel = velocity * 0.15f + Main.rand.NextVector2Circular(2, 2);
                    Dust dust = Dust.NewDustDirect(particlePos, 1, 1, DustID.FireworkFountain_Red,
                        particleVel.X, particleVel.Y, 100, Color.OrangeRed, Main.rand.NextFloat(1.1f, 1.7f));
                    dust.noGravity = true;
                }
            }

            //受击加速收势——给玩家"打断它"的反制手段
            drillTimer += npc.justHit ? 4f : 1f;
            Timer++;

            if ((drillTimer >= 480f || npc.Distance(ctx.Target.Center) > 1600f) && !VaultUtils.isClient) {
                return new SawRecoveryState();
            }
            return null;
        }
    }

    /// <summary>
    /// 电锯收势归位：锯片减速，返回头部附近重新整备
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)PrimeArmStateIndex.SawRecovery, typeof(PrimeArmStateContext))]
    internal class SawRecoveryState : PrimeArmStateBase
    {
        public override string StateName => "SawRecovery";
        public override PrimeArmStateIndex StateIndex => PrimeArmStateIndex.SawRecovery;

        public override PrimeArmStateBase OnUpdate(PrimeArmStateContext ctx) {
            NPC npc = ctx.Npc;
            npc.damage = 0;
            ctx.TargetSpinSpeed = 0.1f;

            AnchoredFollow(ctx, 20f, -20f, -20f, 20f);
            ctx.SpringVelocity = npc.velocity;

            Vector2 toHead = ctx.Head.Center - npc.Center;
            npc.rotation = MathHelper.Lerp(npc.rotation, toHead.ToRotation() + MathHelper.PiOver2, 0.08f);

            Timer++;
            if (Timer > 30 && IdleAnchorDistance(ctx) < 400f && !VaultUtils.isClient) {
                return new SawIdleState();
            }
            return null;
        }
    }
}
