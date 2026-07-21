using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Rendering;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.States
{
    /// <summary>冲刺中，峰值后指数回落，受限大弧追踪</summary>
    [InnoVault.StateMachines.VaultState((int)DestroyerStateIndex.Dashing, typeof(DestroyerStateContext))]
    internal class DestroyerDashingState : DestroyerStateBase
    {
        public override string StateName => "Dashing";
        public override DestroyerStateIndex StateIndex => DestroyerStateIndex.Dashing;

        private const int DashDuration = 56;

        private int currentDashCount;
        private int maxDashCount;

        public DestroyerDashingState() : this(0, 3) {
        }

        public DestroyerDashingState(int dashCount, int maxCount) {
            currentDashCount = dashCount;
            maxDashCount = maxCount;
        }

        public override void OnEnter(DestroyerStateContext context) {
            base.OnEnter(context);
            context.SkipDefaultMovement = true;
            //启动帧闪雷，弱于俯冲
            MachineEffect.TriggerSkyFlash(context.Npc.Center, 0.7f);
        }

        public override IDestroyerState OnUpdate(DestroyerStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            //初段×1.02，后指数回巡航
            float cruiseSpeed = DestroyerDashPrepareState.DashSpeed(context);
            float speed = npc.velocity.Length();
            if (Timer < 8) {
                speed *= 1.02f;
            }
            else {
                speed = MathHelper.Lerp(speed, cruiseSpeed, 0.045f);
            }

            //受限转向，高速大弧
            float maxTurn = (context.IsEnraged ? 0.011f : 0.007f) + (context.IsDeathMode ? 0.003f : 0f);
            float heading = npc.velocity.ToRotation();
            float desired = (player.Center - npc.Center).ToRotation();
            heading = heading.AngleTowards(desired, maxTurn);

            npc.velocity = heading.ToRotationVector2() * speed;
            npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;

            //头下扬尘(客户端)
            if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                Dust dust = Dust.NewDustDirect(npc.position, npc.width, npc.height,
                    DustID.Smoke, 0, 0, 130, default, Main.rand.NextFloat(1.2f, 2f));
                dust.noGravity = true;
                dust.velocity = -npc.velocity * 0.18f + Main.rand.NextVector2Circular(2f, 2f);
            }

            Timer++;

            //冲过目标且远离则收尾
            bool passedTarget = Timer > 24
                && npc.Distance(player.Center) > 860f
                && Vector2.Dot(npc.velocity.SafeNormalize(Vector2.Zero),
                    (player.Center - npc.Center).SafeNormalize(Vector2.Zero)) < -0.2f;

            if (Timer >= DashDuration || passedTarget) {
                currentDashCount++;
                npc.netUpdate = true;
                //进刹车漂移
                return new DestroyerDashCooldownState(currentDashCount, maxDashCount);
            }

            return null;
        }

        public override void OnExit(DestroyerStateContext context) {
            base.OnExit(context);
            context.SkipDefaultMovement = false;
        }
    }

    /// <summary>冲刺冷却，硬刹漂移→回位连突或巡空</summary>
    [InnoVault.StateMachines.VaultState((int)DestroyerStateIndex.DashCooldown, typeof(DestroyerStateContext))]
    internal class DestroyerDashCooldownState : DestroyerStateBase
    {
        public override string StateName => "DashCooldown";
        public override DestroyerStateIndex StateIndex => DestroyerStateIndex.DashCooldown;

        private const int DriftTime = 22;

        private int currentDashCount;
        private int maxDashCount;
        private int driftSign;

        public DestroyerDashCooldownState() : this(0, 3) {
        }

        public DestroyerDashCooldownState(int dashCount, int maxCount) {
            currentDashCount = dashCount;
            maxDashCount = maxCount;
        }

        public override void OnEnter(DestroyerStateContext context) {
            base.OnEnter(context);
            context.SkipDefaultMovement = true;
            driftSign = 0;
            //刹车应力声
            SoundEngine.PlaySound(SoundID.NPCHit4 with { Pitch = -0.2f, Volume = 0.6f }, context.Npc.Center);
        }

        public override IDestroyerState OnUpdate(DestroyerStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            //漂移朝玩家侧回卷
            if (driftSign == 0) {
                float cross = Vector2.Dot(npc.velocity.RotatedBy(MathHelper.PiOver2), player.Center - npc.Center);
                driftSign = cross >= 0f ? 1 : -1;
            }

            if (Timer < DriftTime) {
                //硬刹三阶+向量旋转甩尾
                float spd = npc.velocity.Length();
                float brake = spd > 40f ? 0.92f : spd > 25f ? 0.94f : 0.96f;
                npc.velocity = npc.velocity.RotatedBy(driftSign * 0.05f) * brake;
                npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;

                if (!VaultUtils.isServer && Timer % 2 == 0) {
                    DestroyerMotionFX.SpawnBrakeSparks(npc);
                }
            }
            else {
                //漂移结束回玩家上方
                context.SkipDefaultMovement = false;
                FaceTarget(npc, player.Center, 0.05f);
                SetMovement(context, player.Center + new Vector2(0, -500), 9f, 0.4f);
            }

            int cooldownTime = (context.IsEnraged ? 40 : 55) - (context.IsDeathMode ? 8 : 0);
            Timer++;

            if (Timer >= cooldownTime) {
                if (currentDashCount >= maxDashCount) {
                    return new DestroyerPatrolState();
                }
                else {
                    return new DestroyerDashPrepareState(currentDashCount);
                }
            }

            return null;
        }

        public override void OnExit(DestroyerStateContext context) {
            base.OnExit(context);
            context.SkipDefaultMovement = false;
        }
    }
}
