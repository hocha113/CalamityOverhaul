using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.States
{
    /// <summary>
    /// 旋转冲撞基类：蓄势 → 突进 → 滑行 的三拍连段。
    /// 蓄势期机体后仰泛红给出明确预警，突进期高速旋转撕裂空气，
    /// 滑行期短暂减速重新索敌，张弛分明。
    /// </summary>
    internal abstract class PrimeDashStateBase : PrimeStateBase
    {
        protected abstract int MaxDashes(PrimeStateContext ctx);
        protected abstract int FirstTelegraph { get; }
        protected abstract int NextTelegraph { get; }
        protected abstract int DashFrames { get; }
        protected abstract int DriftFrames { get; }
        protected abstract float DashSpeed(PrimeStateContext ctx);
        protected abstract IPrimeState NextState();

        //0=蓄势 1=突进 2=滑行
        private int cyclePhase;
        private int phaseTimer;

        public override void OnEnter(PrimeStateContext context) {
            base.OnEnter(context);
            cyclePhase = 0;
            phaseTimer = 0;
        }

        public override IPrimeState OnUpdate(PrimeStateContext context) {
            NPC npc = context.Npc;
            context.FrameMode = 1;

            switch (cyclePhase) {
                case 0:
                    UpdateTelegraph(context);
                    break;
                case 1:
                    UpdateDash(context);
                    break;
                default:
                    UpdateDrift(context);
                    break;
            }

            phaseTimer++;
            Timer++;

            //连段结束，收势返回
            if (Counter >= MaxDashes(context) && cyclePhase != 1) {
                npc.damage = npc.defDamage;
                npc.defense = npc.defDefense;
                if (!VaultUtils.isClient) {
                    npc.TargetClosest();
                    return NextState();
                }
            }
            return null;
        }

        private int CurrentTelegraph => Counter == 0 ? FirstTelegraph : NextTelegraph;

        private void UpdateTelegraph(PrimeStateContext context) {
            NPC npc = context.Npc;
            npc.damage = 0;

            //锁定预测落点：玩家当前位置 + 速度补偿
            Vector2 aim = (context.Target.Center + context.Target.velocity * 8f - npc.Center).SafeNormalize(Vector2.UnitY);
            context.DashDirection = aim;
            context.SetChargeState(1, phaseTimer / (float)CurrentTelegraph);

            //后仰蓄势：缓缓向反方向退开，弹簧拉满的观感
            npc.velocity = Vector2.Lerp(npc.velocity, -aim * 3.5f, 0.1f);
            npc.rotation = npc.rotation.AngleLerp(aim.X * 0.4f, 0.2f);

            if (phaseTimer == 2 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.ForceRoar with { Volume = Counter == 0 ? 1f : 0.6f }, npc.Center);
            }

            if (phaseTimer >= CurrentTelegraph) {
                LaunchDash(context, aim);
            }
        }

        private void LaunchDash(PrimeStateContext context, Vector2 aim) {
            NPC npc = context.Npc;
            cyclePhase = 1;
            phaseTimer = 0;
            context.ResetChargeState();

            npc.velocity = aim * DashSpeed(context);
            context.DashDirection = aim;
            if (!VaultUtils.isClient) {
                npc.netUpdate = true;
            }
            if (!VaultUtils.isServer) {
                SoundStyle sound = "CalamityMod/Sounds/Custom/ExoMechs/AresEnraged".GetSound();
                SoundEngine.PlaySound(sound with { Pitch = 1.18f, Volume = 0.75f }, npc.Center);
            }
        }

        private void UpdateDash(PrimeStateContext context) {
            NPC npc = context.Npc;
            npc.damage = npc.defDamage * 2;
            npc.defense = (int)(npc.defDefense * 1.25f);
            SpinRotation(npc, 0.34f);

            //突进尾迹火花
            if (!VaultUtils.isServer && phaseTimer % 3 == 0) {
                Dust dust = Dust.NewDustDirect(npc.position, npc.width, npc.height,
                    DustID.FireworkFountain_Red, -npc.velocity.X * 0.15f, -npc.velocity.Y * 0.15f,
                    100, Color.OrangeRed, Main.rand.NextFloat(1.2f, 1.8f));
                dust.noGravity = true;
            }

            if (phaseTimer >= DashFrames) {
                cyclePhase = 2;
                phaseTimer = 0;
                Counter++;
            }
        }

        private void UpdateDrift(PrimeStateContext context) {
            NPC npc = context.Npc;
            npc.damage = npc.defDamage;
            npc.velocity *= 0.9f;
            SpinRotation(npc, 0.18f);

            if (phaseTimer >= DriftFrames && Counter < MaxDashes(context)) {
                cyclePhase = 0;
                phaseTimer = 0;
                if (!VaultUtils.isClient) {
                    npc.TargetClosest();
                }
            }
        }
    }

    /// <summary>
    /// 武装阶段旋转冲撞：三段式连冲，结束后回到指挥悬停
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)PrimeStateIndex.SpinDash, typeof(PrimeStateContext))]
    internal class PrimeSpinDashState : PrimeDashStateBase
    {
        public override string StateName => "SpinDash";
        public override PrimeStateIndex StateIndex => PrimeStateIndex.SpinDash;

        protected override int MaxDashes(PrimeStateContext ctx) {
            int dashes = 3;
            if (ctx.DeathMode) {
                dashes++;
            }
            if (ctx.BossRush) {
                dashes++;
            }
            return dashes;
        }

        protected override int FirstTelegraph => 40;
        protected override int NextTelegraph => 24;
        protected override int DashFrames => 34;
        protected override int DriftFrames => 14;

        protected override float DashSpeed(PrimeStateContext ctx) {
            float speed = Main.masterMode ? 17f : 14.5f;
            if (ctx.DeathMode) {
                speed += 2f;
            }
            if (ctx.BossRush) {
                speed *= 1.3f;
            }
            return speed;
        }

        protected override IPrimeState NextState() => new PrimeCommandHoverState();
    }

    /// <summary>
    /// 狂暴阶段冲撞：更短的预警、更快的速度、更多的连段，结束后回到狂暴悬停
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)PrimeStateIndex.RageDash, typeof(PrimeStateContext))]
    internal class PrimeRageDashState : PrimeDashStateBase
    {
        public override string StateName => "RageDash";
        public override PrimeStateIndex StateIndex => PrimeStateIndex.RageDash;

        protected override int MaxDashes(PrimeStateContext ctx) {
            int dashes = 4;
            if (ctx.DeathMode) {
                dashes++;
            }
            if (ctx.BossRush) {
                dashes++;
            }
            return dashes;
        }

        protected override int FirstTelegraph => 28;
        protected override int NextTelegraph => 18;
        protected override int DashFrames => 30;
        protected override int DriftFrames => 10;

        protected override float DashSpeed(PrimeStateContext ctx) {
            float speed = Main.masterMode ? 20f : 17.5f;
            if (ctx.DeathMode) {
                speed += 2.5f;
            }
            if (ctx.BossRush) {
                speed *= 1.3f;
            }
            return speed;
        }

        protected override IPrimeState NextState() => new PrimeRageHoverState();
    }
}
