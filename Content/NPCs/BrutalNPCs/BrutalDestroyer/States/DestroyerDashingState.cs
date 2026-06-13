using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Rendering;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.States
{
    /// <summary>
    /// 冲刺中状态：冲量峰值后指数衰减回巡航冲刺速度，受限转向率弧线追踪。
    /// 高速时只能划大弧线，保证可躲的同时画出鞭击般的轨迹
    /// </summary>
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
            //冲刺启动帧天空闪雷（连冲每段各闪一次，亮度低于俯冲）
            MachineEffect.TriggerSkyFlash(context.Npc.Center, 0.7f);
        }

        public override IDestroyerState OnUpdate(DestroyerStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            //释放初段复利加速（×1.02/f，冲刺持续升级感），随后指数回落到巡航冲刺速度
            float cruiseSpeed = DestroyerDashPrepareState.DashSpeed(context);
            float speed = npc.velocity.Length();
            if (Timer < 8) {
                speed *= 1.02f;
            }
            else {
                speed = MathHelper.Lerp(speed, cruiseSpeed, 0.045f);
            }

            //受限转向率追踪：高速大弧线，无法原地急转，公平且有力量感
            float maxTurn = (context.IsEnraged ? 0.011f : 0.007f) + (context.IsDeathMode ? 0.003f : 0f);
            float heading = npc.velocity.ToRotation();
            float desired = (player.Center - npc.Center).ToRotation();
            heading = heading.AngleTowards(desired, maxTurn);

            npc.velocity = heading.ToRotationVector2() * speed;
            npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;

            //冲刺尾流：头部下方扬尘（仅客户端）
            if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                Dust dust = Dust.NewDustDirect(npc.position, npc.width, npc.height,
                    DustID.Smoke, 0, 0, 130, default, Main.rand.NextFloat(1.2f, 2f));
                dust.noGravity = true;
                dust.velocity = -npc.velocity * 0.18f + Main.rand.NextVector2Circular(2f, 2f);
            }

            Timer++;

            //冲过目标后提前收尾：与玩家距离拉开且正在远离，避免无意义的直线滞空
            bool passedTarget = Timer > 24
                && npc.Distance(player.Center) > 860f
                && Vector2.Dot(npc.velocity.SafeNormalize(Vector2.Zero),
                    (player.Center - npc.Center).SafeNormalize(Vector2.Zero)) < -0.2f;

            if (Timer >= DashDuration || passedTarget) {
                currentDashCount++;
                npc.netUpdate = true;
                //进入刹车漂移冷却
                return new DestroyerDashCooldownState(currentDashCount, maxDashCount);
            }

            return null;
        }

        public override void OnExit(DestroyerStateContext context) {
            base.OnExit(context);
            context.SkipDefaultMovement = false;
        }
    }

    /// <summary>
    /// 冲刺冷却状态：硬刹车漂移弧 + 金属应力火花，随后回到玩家上方，
    /// 决定继续连突还是回归巡空
    /// </summary>
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

            //漂移方向：朝玩家所在的一侧回卷
            if (driftSign == 0) {
                float cross = Vector2.Dot(npc.velocity.RotatedBy(MathHelper.PiOver2), player.Center - npc.Center);
                driftSign = cross >= 0f ? 1 : -1;
            }

            if (Timer < DriftTime) {
                //硬刹车漂移弧：三层阶梯刹车 + 速度向量旋转，重型机体的长弧甩尾停驻
                float spd = npc.velocity.Length();
                float brake = spd > 40f ? 0.92f : spd > 25f ? 0.94f : 0.96f;
                npc.velocity = npc.velocity.RotatedBy(driftSign * 0.05f) * brake;
                npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;

                if (!VaultUtils.isServer && Timer % 2 == 0) {
                    DestroyerMotionFX.SpawnBrakeSparks(npc);
                }
            }
            else {
                //漂移结束，交回常规转向模型，回到玩家上方整备
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
