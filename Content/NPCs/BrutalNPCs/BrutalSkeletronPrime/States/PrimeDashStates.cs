using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core;
using CalamityOverhaul.Content.Projectiles.Boss.SkeletronPrime;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.States
{
    /// <summary>
    /// 旋转冲撞：late-snap 后仰蓄势 → 锁定瞄准（预警线与实际弹道一致）→
    /// 单帧设速全速贯穿（横跨大半屏，约 60+ 格）→ 硬刹收势，3~5 段连冲。
    /// 接触伤害严格绑定速度门槛：只有真正高速时才有伤害。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)PrimeStateIndex.SpinDash, typeof(PrimeStateContext))]
    internal class PrimeSpinDashState : PrimeStateBase
    {
        public override string StateName => "SpinDash";
        public override PrimeStateIndex StateIndex => PrimeStateIndex.SpinDash;

        /// <summary>全速贯穿帧数（速度不衰减；30f × 32px ≈ 960px ≈ 60格）</summary>
        internal static int DashActive => 30;
        /// <summary>硬刹收势帧数</summary>
        internal static int BrakeFrames => 14;
        /// <summary>蓄势末段锁定瞄准的提前量（锁定后玩家走位不再被跟踪）</summary>
        internal static int AimLockLead => 14;
        /// <summary>基础冲刺速度 px/帧（大师模式）</summary>
        internal static float DashSpeedMaster => 36f;
        /// <summary>基础冲刺速度 px/帧（普通/专家）</summary>
        internal static float DashSpeedNormal => 32f;
        /// <summary>死亡模式追加速度</summary>
        internal static float DashSpeedDeathBonus => 3f;
        /// <summary>Boss急速模式速度倍率</summary>
        internal static float DashSpeedBossRushMult => 1.2f;

        //0=蓄势 1=突进 2=硬刹
        private int cyclePhase;
        private int phaseTimer;
        private Vector2 lockedAim = Vector2.UnitY;

        public override void OnEnter(PrimeStateContext context) {
            base.OnEnter(context);
            cyclePhase = 0;
            phaseTimer = 0;
        }

        public override IPrimeState OnUpdate(PrimeStateContext context) {
            NPC npc = context.Npc;
            context.FrameMode = 1;

            switch (cyclePhase) {
                case 0: UpdateTelegraph(context); break;
                case 1: UpdateDash(context); break;
                default: UpdateBrake(context); break;
            }

            phaseTimer++;
            Timer++;

            int maxDashes = 3 + (context.DeathMode ? 1 : 0) + (context.BossRush ? 1 : 0);
            if (Counter >= maxDashes && cyclePhase != 1 && !VaultUtils.isClient) {
                npc.damage = npc.defDamage;
                npc.defense = npc.defDefense;
                return new PrimeCommandSequenceState();
            }
            return null;
        }

        private void UpdateTelegraph(PrimeStateContext context) {
            NPC npc = context.Npc;
            npc.damage = 0;
            int telegraph = PrimeDirector.DashTelegraphFrames;

            //锁定前持续预测跟踪；锁定后瞄准冻结，预警线即真实弹道
            if (phaseTimer < telegraph - AimLockLead) {
                lockedAim = (context.Target.Center + context.Target.velocity * 8f - npc.Center)
                    .SafeNormalize(Vector2.UnitY);
            }
            else if (phaseTimer == telegraph - AimLockLead && !VaultUtils.isClient) {
                PrimeTelegraphLine.SpawnLine(npc, npc.Center, lockedAim.ToRotation(), AimLockLead);
            }
            context.DashDirection = lockedAim;

            //late-snap 蓄势曲线：前段几乎不动，末段急速后仰拉满
            float t = phaseTimer / (float)telegraph;
            float windup = (float)System.Math.Pow(t, 8);
            context.SetChargeState(1, windup);
            npc.velocity = Vector2.Lerp(npc.velocity, -lockedAim * (3f + windup * 6f), 0.14f);
            npc.rotation = npc.rotation.AngleLerp(lockedAim.X * 0.35f, 0.18f);

            if (phaseTimer == 4 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.ForceRoar with { Volume = Counter == 0 ? 1f : 0.65f }, npc.Center);
            }
            if (phaseTimer >= telegraph) {
                LaunchDash(context);
            }
        }

        private void LaunchDash(PrimeStateContext context) {
            NPC npc = context.Npc;
            cyclePhase = 1;
            phaseTimer = 0;
            context.ResetChargeState();

            float speed = Main.masterMode ? DashSpeedMaster : DashSpeedNormal;
            if (context.DeathMode) {
                speed += DashSpeedDeathBonus;
            }
            if (context.BossRush) {
                speed *= DashSpeedBossRushMult;
            }
            npc.velocity = lockedAim * speed;

            if (!VaultUtils.isClient) {
                npc.netUpdate = true;
            }
            if (!VaultUtils.isServer) {
                PrimeScreenEffects.PushHeatWake(npc.Center, npc.velocity.ToRotation(), 1f);
                SoundEngine.PlaySound("CalamityMod/Sounds/Custom/ExoMechs/AresEnraged".GetSound() with { Pitch = 1.18f, Volume = 0.75f }, npc.Center);
            }
        }

        private void UpdateDash(PrimeStateContext context) {
            NPC npc = context.Npc;
            float speed = npc.velocity.Length();
            //接触伤害严格绑定冲刺速度
            npc.damage = speed > PrimeDirector.DashContactSpeedThreshold ? npc.defDamage * 2 : 0;
            npc.defense = (int)(npc.defDefense * 1.25f);
            SpinRotation(npc, 0.34f);
            //全速保持：贯穿段不衰减，硬刹只发生在收势段

            if (!VaultUtils.isServer) {
                PrimeScreenEffects.PushHeatWake(npc.Center, npc.velocity.ToRotation(),
                    MathHelper.Clamp(speed / 30f, 0.3f, 1f));
                if (phaseTimer % 3 == 0) {
                    Dust dust = Dust.NewDustDirect(npc.position, npc.width, npc.height,
                        DustID.FireworkFountain_Red, -npc.velocity.X * 0.15f, -npc.velocity.Y * 0.15f,
                        100, Color.OrangeRed, Main.rand.NextFloat(1.2f, 1.8f));
                    dust.noGravity = true;
                }
            }

            if (phaseTimer >= DashActive) {
                cyclePhase = 2;
                phaseTimer = 0;
                Counter++;
            }
        }

        private void UpdateBrake(PrimeStateContext context) {
            NPC npc = context.Npc;
            float speed = npc.velocity.Length();
            //硬刹首帧仍有速度伤害，速度跌破门槛自动失伤——伤害窗口与视觉冲势完全一致
            npc.damage = speed > PrimeDirector.DashContactSpeedThreshold ? npc.defDamage * 2 : 0;
            npc.velocity *= 0.62f;
            SpinRotation(npc, 0.18f);

            int maxDashes = 3 + (context.DeathMode ? 1 : 0) + (context.BossRush ? 1 : 0);
            if (phaseTimer >= BrakeFrames && Counter < maxDashes) {
                cyclePhase = 0;
                phaseTimer = 0;
                if (!VaultUtils.isClient) {
                    npc.TargetClosest();
                }
            }
        }
    }

    /// <summary>
    /// 狂暴闪现贯穿：预警（末段锁定，预警线画在真实贯穿路径上）→ 闪现至玩家远侧
    /// → 直线全速贯穿（穿过玩家身后约 25 格）→ 越界即再闪现，三连无回程死时间。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)PrimeStateIndex.RageDash, typeof(PrimeStateContext))]
    internal class PrimeRageDashState : PrimeStateBase
    {
        public override string StateName => "RageDash";
        public override PrimeStateIndex StateIndex => PrimeStateIndex.RageDash;

        /// <summary>预警末段锁定提前量</summary>
        internal static int AimLockLead => 14;
        /// <summary>闪现至玩家远侧的距离</summary>
        internal static float FlashDistance => 600f;
        /// <summary>贯穿速度 px/帧（大师模式）</summary>
        internal static float DashSpeedMaster => 34f;
        /// <summary>贯穿速度 px/帧（普通/专家）</summary>
        internal static float DashSpeedNormal => 30f;
        /// <summary>越界判定距离（超过即进入下一段闪现）</summary>
        internal static float OutOfBoundsDistance => 1150f;
        /// <summary>单段贯穿最长帧数</summary>
        internal static int MaxDashFrames => 34;

        private int phase;
        private int phaseTimer;
        private Vector2 dashDir = Vector2.UnitY;
        private Vector2 flashFrom;

        public override void OnEnter(PrimeStateContext context) {
            base.OnEnter(context);
            phase = 0;
            phaseTimer = 0;
        }

        public override IPrimeState OnUpdate(PrimeStateContext context) {
            NPC npc = context.Npc;
            context.FrameMode = 2;

            switch (phase) {
                case 0: Telegraph(context); break;
                case 1: FlashReposition(context); break;
                default: LineDash(context); break;
            }

            phaseTimer++;
            Timer++;

            int maxHits = 3 + (context.DeathMode ? 1 : 0);
            if (Counter >= maxHits && phase == 2 && phaseTimer > 6 && !VaultUtils.isClient) {
                return new PrimeRageConnectorState();
            }
            return null;
        }

        private void Telegraph(PrimeStateContext context) {
            NPC npc = context.Npc;
            npc.damage = 0;
            int telegraph = PrimeDirector.DashTelegraphFrames;

            //锁定前跟踪；锁定后预警线画在闪现后的真实贯穿路径上
            if (phaseTimer < telegraph - AimLockLead) {
                dashDir = DirectionToTarget(context);
            }
            else if (phaseTimer == telegraph - AimLockLead && !VaultUtils.isClient) {
                Vector2 flashPoint = context.Target.Center - dashDir * FlashDistance;
                PrimeTelegraphLine.SpawnLine(npc, flashPoint, dashDir.ToRotation(), AimLockLead);
            }

            context.SetChargeState(1, phaseTimer / (float)telegraph);
            npc.velocity *= 0.9f;

            if (phaseTimer >= telegraph) {
                phase = 1;
                phaseTimer = 0;
            }
        }

        private void FlashReposition(PrimeStateContext context) {
            NPC npc = context.Npc;
            Player target = context.Target;
            flashFrom = npc.Center;
            npc.Center = target.Center - dashDir * FlashDistance;
            npc.velocity = Vector2.Zero;
            context.ResetChargeState();

            if (!VaultUtils.isServer) {
                for (int i = 0; i < 8; i++) {
                    Dust dust = Dust.NewDustDirect(flashFrom, 1, 1, DustID.Electric, 0, 0, 100, Color.Cyan, 1.6f);
                    dust.noGravity = true;
                }
                SoundEngine.PlaySound(SoundID.Item8 with { Pitch = 0.4f, Volume = 0.7f }, npc.Center);
            }

            phase = 2;
            phaseTimer = 0;
        }

        private void LineDash(PrimeStateContext context) {
            NPC npc = context.Npc;
            float speed = Main.masterMode ? DashSpeedMaster : DashSpeedNormal;
            if (context.BossRush) {
                speed *= 1.2f;
            }
            npc.velocity = dashDir * speed;
            float vel = npc.velocity.Length();
            npc.damage = vel > PrimeDirector.DashContactSpeedThreshold ? npc.defDamage * 2 : 0;
            SpinRotation(npc, 0.42f);

            if (!VaultUtils.isServer) {
                PrimeScreenEffects.PushHeatWake(npc.Center, npc.velocity.ToRotation(), 1f);
            }

            bool outOfBounds = Vector2.Distance(npc.Center, context.Target.Center) > OutOfBoundsDistance
                || phaseTimer > MaxDashFrames;
            if (outOfBounds) {
                Counter++;
                phase = 0;
                phaseTimer = 0;
            }
        }
    }
}
