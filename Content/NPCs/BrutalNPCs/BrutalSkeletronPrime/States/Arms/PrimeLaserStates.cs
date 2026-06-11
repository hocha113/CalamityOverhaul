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
    /// <summary>
    /// 激光炮蓄势瞄准：跟随头部左侧悬浮，缓慢锁定玩家充能，
    /// 充能满后按"蓄力炮 → 激光环 → 速射"的确定性序列出招
    /// </summary>
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

                int cycle = ctx.AttackCycle;
                ctx.AttackCycle = (cycle + 1) % 3;
                if (cycle == 0 || ctx.Death) {
                    return new LaserChargedShotState();
                }
                if (cycle == 1) {
                    return new LaserRingState();
                }
                return new LaserRapidFireState();
            }
            return null;
        }

        /// <summary>激光炮的头侧悬浮跟随（带臂侧修正）</summary>
        internal static void Follow(PrimeArmStateContext ctx) {
            int side = ctx.Side;
            AnchoredFollow(ctx, -80f, -120f, -200f * side, -160f * side);
        }
    }

    /// <summary>
    /// 激光炮速射：快速追踪连射死亡激光，每发带后坐力，越打越快
    /// </summary>
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
            float laserSpeed = (ctx.BossRush ? 5f : 4f) * (1f + Counter * 0.1f);
            int type = ProjectileID.DeathLaser;
            int damage = ScaleDamage(CWRRef.GetProjectileDamage(npc, type));

            HeadPrimeAI.SpanFireLerterDustEffect(npc, 3);

            Vector2 laserVelocity = ctx.AimDirection * laserSpeed;
            Vector2 spawnPos = npc.Center + ctx.AimDirection * 100f;

            if (ctx.Death) {
                type = ModContent.ProjectileType<DeadLaser>();
                damage = ScaleDamage(CWRRef.GetProjectileDamage(npc, type));
                laserVelocity *= 0.65f;
            }

            Projectile.NewProjectile(npc.GetSource_FromAI(), spawnPos, laserVelocity, type, damage, 0f, Main.myPlayer, 1f, 0f);
        }
    }

    /// <summary>
    /// 激光炮蓄力重炮：锁定 → 充能汇聚 → 轰出高速主炮（高难附带扇形散射），
    /// 充能进度通过发光层与粒子完全可读
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)PrimeArmStateIndex.LaserChargedShot, typeof(PrimeArmStateContext))]
    internal class LaserChargedShotState : PrimeArmStateBase
    {
        public override string StateName => "LaserChargedShot";
        public override PrimeArmStateIndex StateIndex => PrimeArmStateIndex.LaserChargedShot;

        private const float ChargeTime = 45f;
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
                        FireChargedLaser(ctx);
                        ctx.ApplyRecoil(12f);
                    }
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.Item33 with { Volume = 1.2f, Pitch = -0.3f }, npc.Center);
                    }
                }

                chargeProgress++;
                ctx.ChargeGlow = MathHelper.Clamp(2f - (chargeProgress - ChargeTime) / 15f, 0f, 1f);

                if (chargeProgress >= ChargeTime + 30 && !VaultUtils.isClient) {
                    return new LaserAimState();
                }
            }

            Timer++;
            return null;
        }

        public override void OnExit(PrimeArmStateContext ctx) {
            ctx.ChargeGlow = 0f;
        }

        private void FireChargedLaser(PrimeArmStateContext ctx) {
            NPC npc = ctx.Npc;
            int type = ctx.Death ? ModContent.ProjectileType<DeadLaser>() : ProjectileID.DeathLaser;
            int damage = ScaleDamage(npc.defDamage / 2);

            float laserSpeed = ctx.Death ? 12f : 15f;
            Vector2 laserVelocity = ctx.AimDirection * laserSpeed;
            Vector2 spawnPos = npc.Center + ctx.AimDirection * 100f;

            //主炮
            Projectile.NewProjectile(npc.GetSource_FromAI(), spawnPos, laserVelocity, type, damage, 0f, Main.myPlayer, 1f, 0f);

            //高难附带扇形散射
            if (ctx.MasterMode || ctx.Death) {
                for (int i = -2; i <= 2; i++) {
                    if (i == 0) {
                        continue;
                    }
                    Vector2 spreadVel = laserVelocity.RotatedBy(i * 0.12f) * 0.8f;
                    Projectile.NewProjectile(npc.GetSource_FromAI(), spawnPos, spreadVel, type, damage, 0f, Main.myPlayer, 1f, 0f);
                }
            }

            HeadPrimeAI.SpanFireLerterDustEffect(npc, 33);

            for (int i = 0; i < 50; i++) {
                Vector2 particleVel = Main.rand.NextVector2Circular(10f, 10f);
                Dust dust = Dust.NewDustDirect(npc.Center, 1, 1, DustID.FireworkFountain_Red,
                    particleVel.X, particleVel.Y, 100, Color.Cyan, Main.rand.NextFloat(1.5f, 2.5f));
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

    /// <summary>
    /// 激光环弹幕：炮体自旋扫场，周期性放出全向激光环（高难为锁定扇面），
    /// 同伴越少持续越久
    /// </summary>
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

            if (!VaultUtils.isClient && !ctx.DontAttack && HeadPrimeAI.setPosingStarmCount == 0) {
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
            int totalProjectiles = ctx.BossRush ? 22 : (ctx.MasterMode ? 13 : 10);
            float radians = MathHelper.TwoPi / totalProjectiles;
            int type = ProjectileID.DeathLaser;
            int damage = ScaleDamage(CWRRef.GetProjectileDamage(npc, type));

            float velocity = 3f;
            double angleA = radians * 0.5;
            double angleB = MathHelper.ToRadians(90f) - angleA;
            float laserVelocityX = (float)(velocity * Math.Sin(angleA) / Math.Sin(angleB));
            bool normalRotation = ctx.AttackCycle % 2 == 0;
            Vector2 spinningPoint = normalRotation ? new Vector2(0f, -velocity) : new Vector2(-laserVelocityX, -velocity);

            if (ctx.Death) {
                totalProjectiles = ctx.BossRush ? 12 : 6;
                radians = MathHelper.TwoPi / totalProjectiles;

                if (InfernumRef.InfernumModeOpenState) {
                    for (int j = 0; j < 5; j++) {
                        for (int k = 0; k < totalProjectiles; k++) {
                            float speedMode = (ctx.BossRush ? 1.7f : 1.55f) + j * (ctx.BossRush ? 0.35f : 0.3f);
                            Vector2 fireDirection = spinningPoint.RotatedBy(radians * k);
                            Projectile.NewProjectile(npc.GetSource_FromAI(),
                                npc.Center + fireDirection.SafeNormalize(Vector2.UnitY) * 100f,
                                fireDirection * speedMode, ModContent.ProjectileType<DeadLaser>(),
                                damage, 0f, Main.myPlayer, 1f, 0f);
                        }
                    }
                }
                else {
                    Vector2 toTarget = npc.Center.To(ctx.Target.Center).UnitVector();
                    for (int i = 0; i < 3; i++) {
                        int index = i - 1;
                        Vector2 fireDirection = spinningPoint.RotatedBy(index * 0.12f);
                        Vector2 ver = toTarget.RotatedBy(index * 0.12f) * 3;
                        Projectile.NewProjectile(npc.GetSource_FromAI(),
                            npc.Center + fireDirection.SafeNormalize(Vector2.UnitY) * 100f,
                            ver, ModContent.ProjectileType<DeadLaser>(), damage, 0f, Main.myPlayer, 1f, 0f);
                    }
                }
                HeadPrimeAI.SpanFireLerterDustEffect(npc, 33);
            }
            else {
                for (int k = 0; k < totalProjectiles; k++) {
                    Vector2 fireDirection = spinningPoint.RotatedBy(radians * k);
                    int proj = Projectile.NewProjectile(npc.GetSource_FromAI(),
                        npc.Center + fireDirection.SafeNormalize(Vector2.UnitY) * 100f,
                        fireDirection, type, damage, 0f, Main.myPlayer, 1f, 0f);
                    Main.projectile[proj].timeLeft = 900;
                }
            }
        }
    }
}
