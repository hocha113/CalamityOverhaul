using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core;
using CalamityOverhaul.Content.Projectiles.Boss.SkeletronPrime;
using CalamityOverhaul.OtherMods.InfernumMode;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.States.Arms
{
    /// <summary>激光炮蓄势：头侧悬浮跟踪，充能后热射线横扫→三连→蓄力→速射轮换</summary>
    [InnoVault.StateMachines.VaultState((int)PrimeArmStateIndex.LaserAim, typeof(PrimeArmStateContext))]
    internal class LaserAimState : PrimeArmStateBase
    {
        public override string StateName => "LaserAim";
        public override PrimeArmStateIndex StateIndex => PrimeArmStateIndex.LaserAim;

        public override PrimeArmStateBase OnUpdate(PrimeArmStateContext ctx) {
            ctx.ChargeGlow = 0f;
            Follow(ctx);
            SmoothAim(ctx, 0.08f);

            //充能（两端确定性自增，失去同伴后加速）
            float chargeRate = 1f + ctx.MissingPartnerCount * PrimeDirector.MissingHeavyLimbChargeBonus;
            ctx.ChargeTimer += chargeRate;

            int threshold = ctx.MasterMode ? 120 : 300;
            if (ctx.ChargeTimer >= threshold && !VaultUtils.isClient && !ctx.DontAttack) {
                ctx.ChargeTimer = 0f;
                ctx.Npc.TargetClosest();
                ctx.Npc.netUpdate = true;

                //全难度共享完整出招轮换，死亡模式只通过充能速度与弹速体现强度
                PrimeCommandKind cmd = HeadPrimeAI.GetActiveCommand(ctx.Head);
                if (cmd == PrimeCommandKind.FireSuppression) {
                    return new LaserSweepState();
                }

                int cycle = ctx.AttackCycle;
                ctx.AttackCycle = (cycle + 1) % 4;
                return cycle switch {
                    0 => new LaserSweepState(),
                    1 => new LaserTriShotState(),
                    2 => new LaserChargedShotState(),
                    _ => new LaserRapidFireState(),
                };
            }
            return null;
        }

        /// <summary>头侧悬浮跟随（含臂侧修正）</summary>
        internal static void Follow(PrimeArmStateContext ctx) {
            int side = ctx.Side;
            AnchoredFollow(ctx, -80f, -120f, -200f * side, -160f * side);
        }
    }

    /// <summary>激光速射：追踪连射死亡激光，逐发后坐加速</summary>
    [InnoVault.StateMachines.VaultState((int)PrimeArmStateIndex.LaserRapidFire, typeof(PrimeArmStateContext))]
    internal class LaserRapidFireState : PrimeArmStateBase
    {
        public override string StateName => "LaserRapidFire";
        public override PrimeArmStateIndex StateIndex => PrimeArmStateIndex.LaserRapidFire;

        private int fireCooldown;

        public override PrimeArmStateBase OnUpdate(PrimeArmStateContext ctx) {
            NPC npc = ctx.Npc;
            LaserAimState.Follow(ctx);
            SmoothAim(ctx, 0.15f);

            if (!VaultUtils.isClient && !ctx.DontAttack) {
                int interval = ctx.MasterMode ? 28 : 38;
                interval -= ctx.MissingPartnerCount * 4;
                if (ctx.BossRush) {
                    interval = 10;
                }
                interval = Math.Max(interval, 8);

                if (++fireCooldown >= interval) {
                    fireCooldown = 0;
                    FireLaser(ctx);
                    Counter++;
                    npc.velocity -= ctx.AimDirection * 2f;
                }
            }

            Timer++;
            int duration = ctx.MasterMode ? 180 : 240;
            if (Timer >= duration && !VaultUtils.isClient) {
                return new LaserAimState();
            }
            return null;
        }

        private void FireLaser(PrimeArmStateContext ctx) {
            NPC npc = ctx.Npc;
            npc.TargetClosest();
            //定制激光全难度统一使用，死亡模式弹速放缓（DeadLaser 自带加速，慢出膛更具压迫层次）
            float laserSpeed = (ctx.BossRush ? 5f : 4f) * (1f + Counter * 0.1f) * (ctx.Death ? 0.65f : 0.8f);
            int type = ModContent.ProjectileType<DeadLaser>();
            int damage = ScaleDamage(CWRRef.GetProjectileDamage(npc, type));

            HeadPrimeAI.SpanFireLerterDustEffect(npc, 3);

            Vector2 laserVelocity = ctx.AimDirection * laserSpeed;
            Vector2 spawnPos = npc.Center + ctx.AimDirection * 100f;

            Projectile.NewProjectile(npc.GetSource_FromAI(), spawnPos, laserVelocity, type, damage, 0f, Main.myPlayer, 1f, 0f);
        }
    }

    /// <summary>激光蓄力重炮：锁定汇聚后轰出贯穿热射线</summary>
    [InnoVault.StateMachines.VaultState((int)PrimeArmStateIndex.LaserChargedShot, typeof(PrimeArmStateContext))]
    internal class LaserChargedShotState : PrimeArmStateBase
    {
        public override string StateName => "LaserChargedShot";
        public override PrimeArmStateIndex StateIndex => PrimeArmStateIndex.LaserChargedShot;

        internal static float ChargeTime => 45f;
        /// <summary>开火后的保持帧数（覆盖光束 10+42+12 的完整生命）</summary>
        internal static int HoldTime => 75;
        private float chargeProgress;
        private bool hasFired;

        public override void OnEnter(PrimeArmStateContext ctx) {
            base.OnEnter(ctx);
            chargeProgress = 0f;
            hasFired = false;
        }

        public override PrimeArmStateBase OnUpdate(PrimeArmStateContext ctx) {
            NPC npc = ctx.Npc;
            LaserAimState.Follow(ctx);

            if (chargeProgress < ChargeTime) {
                chargeProgress += 1f + (ctx.Death ? 0.5f : 0f);
                ctx.ChargeGlow = chargeProgress / ChargeTime;

                //蓄力前段仍允许缓慢追瞄，临射击前彻底锁死，给出反应窗口
                if (chargeProgress < ChargeTime * 0.7f) {
                    SmoothAim(ctx, 0.06f);
                }

                if (!VaultUtils.isServer) {
                    if (Timer % 3 == 0) {
                        SpawnChargeParticles(ctx);
                    }
                    if (Timer == 1) {
                        SoundEngine.PlaySound(SoundID.Item13 with { Volume = 0.7f, Pitch = -0.4f }, npc.Center);
                    }
                    if ((int)chargeProgress == (int)(ChargeTime * 0.7f)) {
                        SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.8f }, npc.Center);
                    }
                }
            }
            else {
                if (!hasFired) {
                    hasFired = true;
                    if (!VaultUtils.isClient) {
                        FireHeatRay(ctx);
                        ctx.ApplyRecoil(18f);
                    }
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.Item33 with { Volume = 1.2f, Pitch = -0.3f }, npc.Center);
                    }
                }

                chargeProgress++;
                ctx.ChargeGlow = MathHelper.Clamp(2f - (chargeProgress - ChargeTime) / 30f, 0f, 1f);

                //光束持续期的反推：重武器的质量反馈
                if (chargeProgress < ChargeTime + 52) {
                    npc.velocity -= ctx.AimDirection * 0.25f;
                }

                if (chargeProgress >= ChargeTime + HoldTime && !VaultUtils.isClient) {
                    return new LaserAimState();
                }
            }

            Timer++;
            return null;
        }

        public override void OnExit(PrimeArmStateContext ctx) {
            ctx.ChargeGlow = 0f;
        }

        /// <summary>蓄力兑现为贯穿热射线，非普攻弹幕</summary>
        private static void FireHeatRay(PrimeArmStateContext ctx) {
            NPC npc = ctx.Npc;
            int damage = ScaleDamage((int)(CWRRef.GetProjectileDamage(npc, ProjectileID.DeathLaser) * 1.1f));
            float aimAngle = ctx.AimDirection.ToRotation();

            Projectile.NewProjectile(npc.GetSource_FromAI(),
                npc.Center + ctx.AimDirection * PrimeArmHeatRayProj.MuzzleOffset, Vector2.Zero,
                ModContent.ProjectileType<PrimeArmHeatRayProj>(), damage, 0f, Main.myPlayer,
                npc.whoAmI, aimAngle, 0f);

            HeadPrimeAI.SpanFireLerterDustEffect(npc, 33);

            for (int i = 0; i < 50; i++) {
                Vector2 particleVel = Main.rand.NextVector2Circular(10f, 10f);
                Dust dust = Dust.NewDustDirect(npc.Center, 1, 1, DustID.FireworkFountain_Red,
                    particleVel.X, particleVel.Y, 100, Color.OrangeRed, Main.rand.NextFloat(1.5f, 2.5f));
                dust.noGravity = true;
                dust.fadeIn = 1.5f;
            }
        }

        private void SpawnChargeParticles(PrimeArmStateContext ctx) {
            NPC npc = ctx.Npc;
            float intensity = chargeProgress / ChargeTime;
            int particleCount = (int)(intensity * 3f) + 1;

            for (int i = 0; i < particleCount; i++) {
                Vector2 muzzle = npc.Center + ctx.AimDirection * 80f;
                Vector2 particlePos = muzzle + Main.rand.NextVector2Circular(30 * intensity, 30 * intensity);
                Vector2 particleVel = (muzzle - particlePos) * 0.15f;

                Color particleColor = Color.Lerp(Color.Yellow, Color.Cyan, intensity);
                Dust dust = Dust.NewDustDirect(particlePos, 1, 1, DustID.FireworkFountain_Red,
                    particleVel.X, particleVel.Y, 100, particleColor, Main.rand.NextFloat(1.0f, 1.8f) * intensity);
                dust.noGravity = true;
                dust.fadeIn = 1.2f;
            }
        }
    }

    /// <summary>激光环弹幕：炮体自旋放环；Death/Infernum 叠加锁定扇面</summary>
    [InnoVault.StateMachines.VaultState((int)PrimeArmStateIndex.LaserRing, typeof(PrimeArmStateContext))]
    internal class LaserRingState : PrimeArmStateBase
    {
        public override string StateName => "LaserRing";
        public override PrimeArmStateIndex StateIndex => PrimeArmStateIndex.LaserRing;

        private float rotationSpeed;
        private int fireCooldown;

        public override void OnEnter(PrimeArmStateContext ctx) {
            base.OnEnter(ctx);
            rotationSpeed = 0f;
            fireCooldown = 0;
        }

        public override PrimeArmStateBase OnUpdate(PrimeArmStateContext ctx) {
            NPC npc = ctx.Npc;
            LaserAimState.Follow(ctx);

            //自旋扫场
            float scanSpeed = (ctx.MasterMode ? 0.04f : 0.03f) * (ctx.Death ? 1.5f : 1f);
            rotationSpeed = MathHelper.Lerp(rotationSpeed, scanSpeed, 0.1f);
            npc.rotation += rotationSpeed * ctx.Side;

            if (!VaultUtils.isClient && !ctx.DontAttack) {
                float rate = 1f + ctx.MissingPartnerCount * 0.5f;
                fireCooldown += (int)rate;
                if (fireCooldown >= 90) {
                    fireCooldown = 0;
                    npc.TargetClosest();
                    FireLaserRing(ctx);
                    ctx.AttackCycle++;
                    SoundEngine.PlaySound(SoundID.Item12 with { Volume = 0.9f, Pitch = 0.2f }, npc.Center);
                }
            }

            Timer++;
            float timeLimit = 135f + ctx.MissingPartnerCount * 90f;
            if (Timer >= timeLimit && !VaultUtils.isClient) {
                return new LaserAimState();
            }
            return null;
        }

        private void FireLaserRing(PrimeArmStateContext ctx) {
            NPC npc = ctx.Npc;
            //全向激光环全难度统一释放，难度只影响环密度
            int totalProjectiles = ctx.BossRush ? 22 : (ctx.Death ? 16 : (ctx.MasterMode ? 13 : 10));
            float radians = MathHelper.TwoPi / totalProjectiles;
            int ringType = ProjectileID.DeathLaser;
            int ringDamage = ScaleDamage(CWRRef.GetProjectileDamage(npc, ringType));

            float velocity = 3f;
            double angleA = radians * 0.5;
            double angleB = MathHelper.ToRadians(90f) - angleA;
            float laserVelocityX = (float)(velocity * Math.Sin(angleA) / Math.Sin(angleB));
            bool normalRotation = ctx.AttackCycle % 2 == 0;
            Vector2 spinningPoint = normalRotation ? new Vector2(0f, -velocity) : new Vector2(-laserVelocityX, -velocity);

            for (int k = 0; k < totalProjectiles; k++) {
                Vector2 fireDirection = spinningPoint.RotatedBy(radians * k);
                int proj = Projectile.NewProjectile(npc.GetSource_FromAI(),
                    npc.Center + fireDirection.SafeNormalize(Vector2.UnitY) * 100f,
                    fireDirection, ringType, ringDamage, 0f, Main.myPlayer, 1f, 0f);
                Main.projectile[proj].timeLeft = 900;
            }

            //Death/Infernum 环上追加锁定扇面，数值叠加强度
            if (ctx.Death || InfernumRef.InfernumModeOpenState) {
                int fanType = ModContent.ProjectileType<DeadLaser>();
                int fanDamage = ScaleDamage(CWRRef.GetProjectileDamage(npc, fanType));
                Vector2 toTarget = npc.Center.To(ctx.Target.Center).UnitVector();
                int fanCount = InfernumRef.InfernumModeOpenState ? 5 : 3;
                for (int i = 0; i < fanCount; i++) {
                    int index = i - fanCount / 2;
                    Vector2 ver = toTarget.RotatedBy(index * 0.12f) * 3;
                    Projectile.NewProjectile(npc.GetSource_FromAI(),
                        npc.Center + ver.SafeNormalize(Vector2.UnitY) * 100f,
                        ver, fanType, fanDamage, 0f, Main.myPlayer, 1f, 0f);
                }
                HeadPrimeAI.SpanFireLerterDustEffect(npc, 33);
            }
        }
    }

    /// <summary>热射线横扫：90帧扇形预警 → 光束沿扇形匀速扫过</summary>
    [InnoVault.StateMachines.VaultState((int)PrimeArmStateIndex.LaserSweep, typeof(PrimeArmStateContext))]
    internal class LaserSweepState : PrimeArmStateBase
    {
        public override string StateName => "LaserSweep";
        public override PrimeArmStateIndex StateIndex => PrimeArmStateIndex.LaserSweep;

        internal static float SweepHalfArc => 0.42f;

        private float sweepStart;
        private bool fired;

        public override void OnEnter(PrimeArmStateContext ctx) {
            base.OnEnter(ctx);
            sweepStart = (ctx.Target.Center - ctx.Npc.Center).ToRotation();
            fired = false;
            if (!VaultUtils.isClient) {
                PrimeTelegraphLine.SpawnFan(ctx.Npc, ctx.Npc.Center, sweepStart, SweepHalfArc, PrimeDirector.BeamTelegraphFrames, true);
            }
        }

        public override PrimeArmStateBase OnUpdate(PrimeArmStateContext ctx) {
            LaserAimState.Follow(ctx);

            if (Timer < PrimeDirector.BeamTelegraphFrames) {
                ctx.ChargeGlow = Timer / (float)PrimeDirector.BeamTelegraphFrames;
                //预警期炮口缓缓压到扫射起始角，蓄势姿态与扇形预告对齐
                ServoRotate(ctx.Npc, sweepStart - SweepHalfArc - MathHelper.PiOver2, 0.06f);
            }
            else {
                //扫射期：炮体跟随光束当前角度转动（确定性公式，两端一致）
                float sweepSpeed = SweepHalfArc * 2f / PrimeArmHeatRayProj.SweepSustain;
                float sweepT = MathHelper.Clamp(Timer - PrimeDirector.BeamTelegraphFrames - 10,
                    0, PrimeArmHeatRayProj.SweepSustain);
                float beamAngle = sweepStart - SweepHalfArc + sweepSpeed * sweepT;
                ServoRotate(ctx.Npc, beamAngle - MathHelper.PiOver2, 0.1f);
                ctx.AimDirection = beamAngle.ToRotationVector2();
                ctx.ChargeGlow = MathHelper.Clamp(1.4f - (Timer - PrimeDirector.BeamTelegraphFrames) / 60f, 0f, 1f);
            }

            if (Timer >= PrimeDirector.BeamTelegraphFrames && !fired && !VaultUtils.isClient && !ctx.DontAttack) {
                fired = true;
                FireSweepBeam(ctx);
                ctx.ApplyRecoil(PrimeDirector.HeavyRecoil);
            }

            Timer++;
            if (Timer >= PrimeDirector.BeamTelegraphFrames + 100 && !VaultUtils.isClient) {
                return new LaserAimState();
            }
            return null;
        }

        public override void OnExit(PrimeArmStateContext ctx) {
            ctx.ChargeGlow = 0f;
        }

        /// <summary>预警兑现为扇形扫过的热射线</summary>
        private void FireSweepBeam(PrimeArmStateContext ctx) {
            NPC npc = ctx.Npc;
            int damage = ScaleDamage((int)(CWRRef.GetProjectileDamage(npc, ProjectileID.DeathLaser) * 1.05f));
            float startAngle = sweepStart - SweepHalfArc;
            float sweepSpeed = SweepHalfArc * 2f / PrimeArmHeatRayProj.SweepSustain;

            Projectile.NewProjectile(npc.GetSource_FromAI(),
                npc.Center + startAngle.ToRotationVector2() * PrimeArmHeatRayProj.MuzzleOffset, Vector2.Zero,
                ModContent.ProjectileType<PrimeArmHeatRayProj>(), damage, 0f, Main.myPlayer,
                npc.whoAmI, startAngle, sweepSpeed);

            HeadPrimeAI.SpanFireLerterDustEffect(npc, 20);
        }
    }

    /// <summary>三连预判点射</summary>
    [InnoVault.StateMachines.VaultState((int)PrimeArmStateIndex.LaserTriShot, typeof(PrimeArmStateContext))]
    internal class LaserTriShotState : PrimeArmStateBase
    {
        public override string StateName => "LaserTriShot";
        public override PrimeArmStateIndex StateIndex => PrimeArmStateIndex.LaserTriShot;

        public override PrimeArmStateBase OnUpdate(PrimeArmStateContext ctx) {
            LaserAimState.Follow(ctx);
            SmoothAim(ctx, 0.12f);

            if (!VaultUtils.isClient && !ctx.DontAttack && Timer % 28 == 0 && Counter < 3) {
                Vector2 predict = ctx.Target.Center + ctx.Target.velocity * (12f + Counter * 4f);
                ctx.AimDirection = (predict - ctx.Npc.Center).SafeNormalize(Vector2.UnitY);
                FireShot(ctx);
                ctx.ApplyRecoil(PrimeDirector.FireRecoil);
                Counter++;
            }

            Timer++;
            if (Counter >= 3 && Timer > 40 && !VaultUtils.isClient) {
                return new LaserAimState();
            }
            return null;
        }

        private static void FireShot(PrimeArmStateContext ctx) {
            int type = ModContent.ProjectileType<DeadLaser>();
            int damage = ScaleDamage(CWRRef.GetProjectileDamage(ctx.Npc, type));
            Vector2 vel = ctx.AimDirection * 5.5f;
            Projectile.NewProjectile(ctx.Npc.GetSource_FromAI(), ctx.Npc.Center + ctx.AimDirection * 90f, vel,
                type, damage, 0f, Main.myPlayer, 1f, 0f);
        }
    }
}
