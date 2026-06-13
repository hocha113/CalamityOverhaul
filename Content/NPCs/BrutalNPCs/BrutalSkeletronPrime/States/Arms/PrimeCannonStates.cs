using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core;
using CalamityOverhaul.Content.Projectiles.Boss.SkeletronPrime;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.States.Arms
{
    /// <summary>火箭炮点射：头侧悬浮单发；激光速射时收敛点射防火力叠加</summary>
    [InnoVault.StateMachines.VaultState((int)PrimeArmStateIndex.CannonBombard, typeof(PrimeArmStateContext))]
    internal class CannonBombardState : PrimeArmStateBase
    {
        public override string StateName => "CannonBombard";
        public override PrimeArmStateIndex StateIndex => PrimeArmStateIndex.CannonBombard;

        private float fireCooldown;
        private float modeTimer;

        public override PrimeArmStateBase OnUpdate(PrimeArmStateContext ctx) {
            NPC npc = ctx.Npc;
            Follow(ctx);
            SmoothAim(ctx, 0.2f);

            if (!VaultUtils.isClient && !ctx.DontAttack) {
                fireCooldown += 1f + ctx.MissingPartnerCount * PrimeDirector.MissingHeavyLimbChargeBonus;

                float interval = 120f;
                if (ctx.Death) {
                    interval -= 20;
                }
                if (ctx.MasterMode) {
                    interval -= 20;
                }
                if (ctx.BossRush) {
                    interval = 60;
                }

                if (fireCooldown >= interval) {
                    fireCooldown = 0f;
                    FireSingleRocket(ctx);
                    npc.TargetClosest();
                    npc.netUpdate = true;
                }
            }

            if (!VaultUtils.isClient && !ctx.DontAttack
                && HeadPrimeAI.GetActiveCommand(ctx.Head) == PrimeCommandKind.FireSuppression
                && Vector2.Distance(ctx.Npc.Center, ctx.Target.Center) >= 350f) {
                return new CannonMortarState();
            }

            Timer++;
            return EvaluateModeSwitch(ctx, wantSpreadWhenFree: true);
        }

        /// <summary>激光存活时跟其节奏（速射→点射，其余→散射）；激光阵亡后计时切换</summary>
        private PrimeArmStateBase EvaluateModeSwitch(PrimeArmStateContext ctx, bool wantSpreadWhenFree) {
            if (VaultUtils.isClient) {
                return null;
            }

            if (ctx.LaserAlive) {
                bool laserRapid = IsLaserRapidFiring();
                if (laserRapid) {
                    return null;//激光速射期间保持点射
                }
                if (wantSpreadWhenFree) {
                    return new CannonSpreadState();
                }
                return null;
            }

            //失去激光炮后自主切换
            modeTimer += 1f + ctx.MissingPartnerCount * PrimeDirector.MissingHeavyLimbChargeBonus;
            if (modeTimer >= (ctx.MasterMode ? 200f : 800f)) {
                return new CannonSpreadState();
            }
            return null;
        }

        internal static bool IsLaserRapidFiring() {
            int laser = CWRWorld.primeLaser;
            if (laser < 0 || laser >= Main.maxNPCs || !Main.npc[laser].active) {
                return false;
            }
            return (int)Main.npc[laser].ai[PrimeAiSlots.ArmStateSlot] == (int)PrimeArmStateIndex.LaserRapidFire;
        }

        /// <summary>火箭炮的头侧悬浮跟随</summary>
        internal static void Follow(PrimeArmStateContext ctx) {
            AnchoredFollow(ctx, -130f, -170f, 160f, 200f);
        }

        private void FireSingleRocket(PrimeArmStateContext ctx) {
            NPC npc = ctx.Npc;
            npc.TargetClosest();
            int damage = ScaleDamage(CWRRef.GetProjectileDamage(npc, ProjectileID.RocketSkeleton));

            Vector2 rocketVelocity = ctx.AimDirection * 10f;
            Vector2 spawnPos = npc.Center + ctx.AimDirection * 40f;

            //制导炮弹（带瞄准线预警）全难度统一使用，难度只影响预警时长
            int proj = Projectile.NewProjectile(npc.GetSource_FromAI(),
                spawnPos, rocketVelocity,
                ModContent.ProjectileType<PrimeCannonOnSpan>(), damage, 0f,
                Main.myPlayer, npc.whoAmI, npc.target, 0);
            Main.projectile[proj].timeLeft = (ctx.Death && ctx.MasterMode) || ctx.BossRush ? 60 : 80;

            ctx.ApplyRecoil(12f);
            SoundEngine.PlaySound(SoundID.Item62 with { Volume = 0.9f, Pitch = -0.2f }, npc.Center);
        }
    }

    /// <summary>火箭扇形齐射：长装填换 3~5 连覆盖</summary>
    [InnoVault.StateMachines.VaultState((int)PrimeArmStateIndex.CannonSpread, typeof(PrimeArmStateContext))]
    internal class CannonSpreadState : PrimeArmStateBase
    {
        public override string StateName => "CannonSpread";
        public override PrimeArmStateIndex StateIndex => PrimeArmStateIndex.CannonSpread;

        private float fireCooldown;
        private float modeTimer;

        public override PrimeArmStateBase OnUpdate(PrimeArmStateContext ctx) {
            NPC npc = ctx.Npc;
            CannonBombardState.Follow(ctx);
            SmoothAim(ctx, 0.2f);

            if (!VaultUtils.isClient && !ctx.DontAttack) {
                fireCooldown += 1f + ctx.MissingPartnerCount * PrimeDirector.MissingHeavyLimbChargeBonus * 0.5f;

                if (fireCooldown >= 180f) {
                    fireCooldown = 0f;
                    FireSpreadRockets(ctx);
                    npc.TargetClosest();
                    npc.netUpdate = true;
                }
            }

            Timer++;

            if (!VaultUtils.isClient) {
                if (ctx.LaserAlive) {
                    if (CannonBombardState.IsLaserRapidFiring()) {
                        return new CannonBombardState();
                    }
                }
                else {
                    modeTimer += 1f + ctx.MissingPartnerCount * PrimeDirector.MissingHeavyLimbChargeBonus * 0.5f;
                    float timeLimit = 120f + ctx.MissingPartnerCount * 90f;
                    if (modeTimer >= timeLimit) {
                        return new CannonBombardState();
                    }
                }
            }
            return null;
        }

        private void FireSpreadRockets(PrimeArmStateContext ctx) {
            NPC npc = ctx.Npc;
            npc.TargetClosest();
            int damage = ScaleDamage(CWRRef.GetProjectileDamage(npc, ProjectileID.RocketSkeleton));

            Vector2 baseVelocity = ctx.AimDirection * 10f;
            //制导炮弹全难度统一使用，难度只影响弹数与张角
            int numProj = ctx.BossRush ? 5 : (ctx.Death ? 4 : 3);
            float rotation = MathHelper.ToRadians(ctx.BossRush ? 15 : 9);

            for (int i = 0; i < numProj; i++) {
                float rotOffset = MathHelper.Lerp(-rotation, rotation, i / (float)(numProj - 1));
                Vector2 perturbedSpeed = baseVelocity.RotatedBy(rotOffset);
                Vector2 spawnPos = npc.Center + ctx.AimDirection * 40f;

                Projectile.NewProjectile(npc.GetSource_FromAI(),
                    spawnPos, perturbedSpeed,
                    ModContent.ProjectileType<PrimeCannonOnSpan>(), damage, 0f,
                    Main.myPlayer, npc.whoAmI, npc.target, rotOffset);
            }

            ctx.ApplyRecoil(18f);
            SoundEngine.PlaySound(SoundID.Item62 with { Volume = 1.0f }, npc.Center);
            SoundEngine.PlaySound(SoundID.DD2_ExplosiveTrapExplode with { Volume = 0.6f, Pitch = 0.4f }, npc.Center);
        }
    }

    /// <summary>抛物线迫击：落点环预告→必中环心火球；最小距 350px</summary>
    [InnoVault.StateMachines.VaultState((int)PrimeArmStateIndex.CannonMortar, typeof(PrimeArmStateContext))]
    internal class CannonMortarState : PrimeArmStateBase
    {
        public override string StateName => "CannonMortar";
        public override PrimeArmStateIndex StateIndex => PrimeArmStateIndex.CannonMortar;

        internal static int TelegraphFrames => 50;

        private Vector2 impactPoint;

        public override void OnEnter(PrimeArmStateContext ctx) {
            base.OnEnter(ctx);
            impactPoint = ctx.Target.Center + ctx.Target.velocity * 18f;
            if (!VaultUtils.isClient) {
                //预警环一直亮到弹着：充能填满的瞬间就是爆炸落地的瞬间
                PrimeTelegraphLine.SpawnRing(ctx.Npc, impactPoint, PrimeMortarShellProj.BlastDiameter / 2f,
                    TelegraphFrames + PrimeMortarShellProj.FlightFrames);
            }
        }

        public override PrimeArmStateBase OnUpdate(PrimeArmStateContext ctx) {
            CannonBombardState.Follow(ctx);

            //炮管压向高抛出膛角，姿态即弹道预告
            Vector2 launchDir = PrimeMortarShellProj.SolveLaunchVelocity(ctx.Npc.Center, impactPoint,
                PrimeMortarShellProj.FlightFrames).SafeNormalize(Vector2.UnitY);
            ctx.AimDirection = Vector2.Lerp(ctx.AimDirection, launchDir, 0.15f);
            ServoRotate(ctx.Npc, launchDir.ToRotation() - MathHelper.PiOver2, 0.12f);

            if (Timer == TelegraphFrames && !VaultUtils.isClient && !ctx.DontAttack) {
                FireMortar(ctx);
                ctx.ApplyRecoil(PrimeDirector.HeavyRecoil);
            }
            Timer++;
            if (Timer > TelegraphFrames + 20 && !VaultUtils.isClient) {
                return new CannonBombardState();
            }
            return null;
        }

        /// <summary>弹道学反解初速，必中预警环心</summary>
        private void FireMortar(PrimeArmStateContext ctx) {
            NPC npc = ctx.Npc;
            int damage = ScaleDamage((int)(CWRRef.GetProjectileDamage(npc, ProjectileID.RocketSkeleton) * 1.25f));
            Vector2 vel = PrimeMortarShellProj.SolveLaunchVelocity(npc.Center, impactPoint,
                PrimeMortarShellProj.FlightFrames);

            Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, vel,
                ModContent.ProjectileType<PrimeMortarShellProj>(), damage, 0f,
                Main.myPlayer, impactPoint.X, impactPoint.Y, PrimeMortarShellProj.FlightFrames);

            SoundEngine.PlaySound(SoundID.Item62 with { Volume = 1.05f, Pitch = -0.4f }, npc.Center);
            HeadPrimeAI.SpanFireLerterDustEffect(npc, 15);
        }
    }

    /// <summary>直射火箭幕</summary>
    [InnoVault.StateMachines.VaultState((int)PrimeArmStateIndex.CannonDirect, typeof(PrimeArmStateContext))]
    internal class CannonDirectState : PrimeArmStateBase
    {
        public override string StateName => "CannonDirect";
        public override PrimeArmStateIndex StateIndex => PrimeArmStateIndex.CannonDirect;

        public override PrimeArmStateBase OnUpdate(PrimeArmStateContext ctx) {
            CannonBombardState.Follow(ctx);
            SmoothAim(ctx, 0.18f);
            if (!VaultUtils.isClient && !ctx.DontAttack && Timer % 16 == 0 && Counter < 6) {
                int damage = ScaleDamage(CWRRef.GetProjectileDamage(ctx.Npc, ProjectileID.RocketSkeleton));
                Vector2 vel = ctx.AimDirection * 12f * MathHelper.Lerp(PrimeDirector.ProjectileWarmupStart, 1f, Counter / 5f);
                Projectile.NewProjectile(ctx.Npc.GetSource_FromAI(), ctx.Npc.Center + ctx.AimDirection * 40f, vel,
                    ModContent.ProjectileType<PrimeCannonOnSpan>(), damage, 0f,
                    Main.myPlayer, ctx.Npc.whoAmI, ctx.Npc.target, 0f);
                ctx.ApplyRecoil(PrimeDirector.FireRecoil);
                Counter++;
            }
            Timer++;
            if (Counter >= 6 && !VaultUtils.isClient) {
                return new CannonBombardState();
            }
            return null;
        }
    }
}
