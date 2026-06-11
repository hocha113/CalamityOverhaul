using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core;
using CalamityOverhaul.Content.Projectiles.Boss.SkeletronPrime;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.States.Arms
{
    /// <summary>
    /// 火箭炮点射：跟随头部右侧悬浮，稳定节奏单发轰炸。
    /// 与激光炮联动——激光炮速射时火箭炮收敛为点射，避免火力叠加失衡
    /// </summary>
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

            Timer++;
            return EvaluateModeSwitch(ctx, wantSpreadWhenFree: true);
        }

        /// <summary>
        /// 模式联动：激光炮存活时跟随其节奏（激光速射 → 点射，其余 → 散射）；
        /// 激光炮阵亡后按计时自主切换
        /// </summary>
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

    /// <summary>
    /// 火箭炮扇形齐射：更长的装填换来一次三~五连的扇形火力覆盖，后坐力猛烈
    /// </summary>
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
}
