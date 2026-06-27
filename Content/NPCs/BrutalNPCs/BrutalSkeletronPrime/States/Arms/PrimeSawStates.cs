using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Projectiles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.States.Arms
{
    //电锯手设计准则：
    //1. 机体不整体自旋——旋转仅属锯片（ctx.SpinSpeed 驱动帧间隔+音效）    //2. 转向走 ServoRotate 最短弧步进    //3. 出招节拍：追踪 → 锁定拍 → 刚性突进
    /// <summary>电锯待机：炮塔缓跟踪，充能后连冲→环绕→钻击轮换</summary>
    [InnoVault.StateMachines.VaultState((int)PrimeArmStateIndex.SawIdle, typeof(PrimeArmStateContext))]
    internal class SawIdleState : PrimeArmStateBase
    {
        public override string StateName => "SawIdle";
        public override PrimeArmStateIndex StateIndex => PrimeArmStateIndex.SawIdle;

        public override PrimeArmStateBase OnUpdate(PrimeArmStateContext ctx) {
            NPC npc = ctx.Npc;
            npc.damage = 0;
            ctx.TargetSpinSpeed = 0.12f;

            Vector2 idleAnchor = ctx.Head.Center + new Vector2(-125f * ctx.Side, 290f);
            SpringMove(ctx, idleAnchor, 0.65f, stiffness: 0.16f, damping: 0.84f, maxSpeed: 30f);

            //炮塔缓速锁敌，锯头咬向玩家
            ServoAimAt(npc, ctx.Target.Center, 0.035f);

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

                if (HeadPrimeAI.GetActiveCommand(ctx.Head) == PrimeCommandKind.PhysicalAssault) {
                    return new SawGroundCutState();
                }

                int cycle = ctx.AttackCycle;
                ctx.AttackCycle = (cycle + 1) % 4;
                return cycle switch {
                    0 => new SawBoomerangState(),
                    1 => new SawSpinUpState(),
                    2 => new SawGroundCutState(),
                    _ => new SawDrillState(),
                };
            }
            return null;
        }
    }

    /// <summary>电锯狂转蓄势：侧翼逼近，尾段锁定拍后突进</summary>
    [InnoVault.StateMachines.VaultState((int)PrimeArmStateIndex.SawSpinUp, typeof(PrimeArmStateContext))]
    internal class SawSpinUpState : PrimeArmStateBase
    {
        public override string StateName => "SawSpinUp";
        public override PrimeArmStateIndex StateIndex => PrimeArmStateIndex.SawSpinUp;

        /// <summary>突进前的锁定定格帧数</summary>
        internal static int LockFrames => 8;

        private bool playedSpinSound;

        public override void OnEnter(PrimeArmStateContext ctx) {
            base.OnEnter(ctx);
            playedSpinSound = false;
        }

        private int SpinUpDuration(PrimeArmStateContext ctx) => ctx.Death ? 22 : (ctx.MasterMode ? 27 : 36);

        public override PrimeArmStateBase OnUpdate(PrimeArmStateContext ctx) {
            NPC npc = ctx.Npc;
            npc.damage = 0;
            ctx.TargetSpinSpeed = 1.0f;

            int duration = SpinUpDuration(ctx);
            bool locking = Timer >= duration - LockFrames;

            if (!locking) {
                //逼近至玩家近侧，锯头伺服追踪
                Vector2 dirToPlayer = npc.Center.DirectionTo(ctx.Target.Center);
                SpringMove(ctx, ctx.Target.Center - dirToPlayer * 200f, 1.1f, stiffness: 0.16f, damping: 0.84f, maxSpeed: 30f);
                ServoAimAt(npc, ctx.Target.Center, 0.09f);
            }
            else {
                //锁定拍：急停+朝向冻结，读招窗口
                Vector2 vel = ctx.SpringVelocity * 0.78f;
                ctx.SpringVelocity = vel;
                npc.velocity = vel;
            }

            if (!VaultUtils.isServer && Timer % 3 == 0) {
                Vector2 particlePos = npc.Center + Main.rand.NextVector2Circular(35, 35);
                Dust dust = Dust.NewDustDirect(particlePos, 1, 1, DustID.FireworkFountain_Red,
                    0, 0, 100, Color.Yellow * 0.8f, Main.rand.NextFloat(0.8f, 1.3f));
                dust.velocity = (npc.Center - particlePos).RotatedBy(MathHelper.PiOver2) * 0.15f;
                dust.noGravity = true;
            }

            if (Timer == 12 && !playedSpinSound && !VaultUtils.isServer) {
                playedSpinSound = true;
                SoundEngine.PlaySound(SoundID.Item22 with { Volume = 0.7f, Pitch = -0.2f }, npc.Center);
            }

            Timer++;
            if (Timer >= duration && !VaultUtils.isClient) {
                return new SawDashState();
            }
            return null;
        }
    }

    /// <summary>电锯冲刺连段：硬咬合突进全程锁朝向，同伴越少段数越多</summary>
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
            //突进瞬间硬咬合运动方向
            npc.rotation = velocity.ToRotation() - MathHelper.PiOver2;
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
            ctx.TargetSpinSpeed = 1.4f;
            npc.velocity = ctx.SpringVelocity;
            //突进全程朝向锁死：刚性直线锯切，绝不空中转体

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
            ctx.TargetSpinSpeed = 1.0f;

            //短促回正：急刹 + 伺服再锁定
            Vector2 vel = ctx.SpringVelocity * 0.85f;
            ctx.SpringVelocity = vel;
            npc.velocity = vel;
            ServoAimAt(npc, ctx.Target.Center, 0.14f);

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

    /// <summary>电锯环绕：绕玩家轴心收紧，锯头伺服咬圆心</summary>
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
            ctx.TargetSpinSpeed = 0.8f;

            orbitAngle += (ctx.MasterMode ? 0.12f : 0.09f) * (ctx.Death ? 1.5f : 1f);
            orbitRadius = MathHelper.Lerp(orbitRadius, 180f, 0.05f);

            Vector2 orbitTarget = ctx.Target.Center + orbitAngle.ToRotationVector2() * orbitRadius;
            SpringMove(ctx, orbitTarget, 1.4f, stiffness: 0.16f, damping: 0.84f, maxSpeed: 30f);

            //环绕时锯头始终咬住圆心的玩家
            ServoAimAt(npc, ctx.Target.Center, 0.18f);

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

    /// <summary>电锯钻击：预判追击落点，受击提前收势</summary>
    [InnoVault.StateMachines.VaultState((int)PrimeArmStateIndex.SawDrill, typeof(PrimeArmStateContext))]
    internal class SawDrillState : PrimeArmStateBase
    {
        public override string StateName => "SawDrill";
        public override PrimeArmStateIndex StateIndex => PrimeArmStateIndex.SawDrill;

        private float drillTimer;

        public override PrimeArmStateBase OnUpdate(PrimeArmStateContext ctx) {
            NPC npc = ctx.Npc;
            npc.damage = npc.defDamage;
            ctx.TargetSpinSpeed = 1.2f;

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

            //锯头伺服对齐前进方向
            if (velocity.LengthSquared() > 1f) {
                ServoRotate(npc, velocity.ToRotation() - MathHelper.PiOver2, 0.25f);
            }

            if (!VaultUtils.isServer && Timer % 2 == 0) {
                for (int i = 0; i < 2; i++) {
                    Vector2 particlePos = npc.Center + Main.rand.NextVector2Circular(25, 25);
                    Vector2 particleVel = velocity * 0.15f + Main.rand.NextVector2Circular(2, 2);
                    Dust dust = Dust.NewDustDirect(particlePos, 1, 1, DustID.FireworkFountain_Red,
                        particleVel.X, particleVel.Y, 100, Color.OrangeRed, Main.rand.NextFloat(1.1f, 1.7f));
                    dust.noGravity = true;
                }
            }

            //受击加速收势，玩家可打断
            drillTimer += npc.justHit ? 4f : 1f;
            Timer++;

            if ((drillTimer >= 480f || npc.Distance(ctx.Target.Center) > 1600f) && !VaultUtils.isClient) {
                return new SawRecoveryState();
            }
            return null;
        }
    }

    /// <summary>电锯收势归位：锯片降速回头部附近</summary>
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

            //伺服回正到自然垂悬
            ServoRotate(npc, 0f, 0.06f);

            Timer++;
            if (Timer > 30 && IdleAnchorDistance(ctx) < 400f && !VaultUtils.isClient) {
                return new SawIdleState();
            }
            return null;
        }
    }

    /// <summary>回旋掷锯：锯片飞出-折返</summary>
    [InnoVault.StateMachines.VaultState((int)PrimeArmStateIndex.SawBoomerang, typeof(PrimeArmStateContext))]
    internal class SawBoomerangState : PrimeArmStateBase
    {
        public override string StateName => "SawBoomerang";
        public override PrimeArmStateIndex StateIndex => PrimeArmStateIndex.SawBoomerang;

        private Vector2 launchDir;
        private bool returning;

        public override void OnEnter(PrimeArmStateContext ctx) {
            base.OnEnter(ctx);
            launchDir = (ctx.Target.Center - ctx.Npc.Center).SafeNormalize(Vector2.UnitY);
            returning = false;
        }

        public override PrimeArmStateBase OnUpdate(PrimeArmStateContext ctx) {
            NPC npc = ctx.Npc;
            ctx.TargetSpinSpeed = 0.35f;
            if (!returning) {
                npc.velocity = launchDir * 16f;
                npc.damage = npc.defDamage;
                if (Vector2.Distance(npc.Center, ctx.Target.Center) < 120f || Timer > 24) {
                    returning = true;
                    launchDir = -launchDir;
                    Timer = 0;
                }
            }
            else {
                npc.velocity = (ctx.Head.Center - npc.Center).SafeNormalize(Vector2.UnitY) * 14f;
                npc.damage = 0;
                if (Vector2.Distance(npc.Center, ctx.Head.Center) < 160f) {
                    return new SawRecoveryState();
                }
            }
            Timer++;
            return null;
        }
    }

    /// <summary>贴地锯切冲锋：地面火花线 telegraph</summary>
    [InnoVault.StateMachines.VaultState((int)PrimeArmStateIndex.SawGroundCut, typeof(PrimeArmStateContext))]
    internal class SawGroundCutState : PrimeArmStateBase
    {
        public override string StateName => "SawGroundCut";
        public override PrimeArmStateIndex StateIndex => PrimeArmStateIndex.SawGroundCut;

        public override void OnEnter(PrimeArmStateContext ctx) {
            base.OnEnter(ctx);
            if (!VaultUtils.isClient) {
                Vector2 dir = (ctx.Target.Center - ctx.Npc.Center).SafeNormalize(Vector2.UnitX);
                PrimeTelegraphLine.SpawnLine(ctx.Npc, ctx.Npc.Center, dir.ToRotation(), PrimeDirector.DashTelegraphFrames);
            }
        }

        public override PrimeArmStateBase OnUpdate(PrimeArmStateContext ctx) {
            NPC npc = ctx.Npc;
            ctx.TargetSpinSpeed = 0.4f;
            if (Timer < PrimeDirector.DashTelegraphFrames) {
                npc.damage = 0;
                ServoAimAt(npc, ctx.Target.Center, 0.08f);
            }
            else if (Timer == PrimeDirector.DashTelegraphFrames) {
                Vector2 dash = (ctx.Target.Center - npc.Center).SafeNormalize(Vector2.UnitX) * 18f;
                npc.velocity = dash;
                npc.damage = npc.defDamage * 2;
                ctx.ApplyRecoil(PrimeDirector.HeavyRecoil);
            }
            else {
                npc.velocity *= 0.88f;
            }

            Timer++;
            if (Timer > PrimeDirector.DashTelegraphFrames + 28 && !VaultUtils.isClient) {
                return new SawRecoveryState();
            }
            return null;
        }
    }
}
