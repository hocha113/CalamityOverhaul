using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.Rendering;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.States
{
    /// <summary>潮汐冲刺·蓄力：锁线预告+迟滞后撤，可预读的暴力直线前摇</summary>
    [InnoVault.StateMachines.VaultState((int)FishronStateIndex.TidalDashPrepare, typeof(FishronStateContext))]
    internal class FishronTidalDashPrepareState : FishronStateBase
    {
        public override string StateName => "TidalDashPrepare";
        public override FishronStateIndex StateIndex => FishronStateIndex.TidalDashPrepare;

        private static int ChargeTime(FishronStateContext ctx) {
            int t = ctx.Phase == 3 ? 24 : ctx.Phase == 2 ? 28 : 34;
            return t - (ctx.IsDeathMode ? 4 : 0);
        }

        internal static float DashSpeed(FishronStateContext ctx) {
            float s = ctx.Phase == 3 ? 58f : ctx.Phase == 2 ? 52f : 45f;
            if (ctx.IsDeathMode) {
                s += 4f;
            }
            if (ctx.IsLandEnraged) {
                s += 6f;
            }
            return s;
        }

        internal static int MaxDashCount(FishronStateContext ctx) => ctx.Phase >= 2 ? 3 : 2;

        private int currentDashCount;
        private Vector2 dashDirection;
        private bool telegraphSpawned;

        public FishronTidalDashPrepareState() : this(0) {
        }

        public FishronTidalDashPrepareState(int dashCount) {
            currentDashCount = dashCount;
        }

        public override void OnEnter(FishronStateContext context) {
            base.OnEnter(context);
            context.SkipDefaultMovement = true;
            telegraphSpawned = false;
            //鳍张开的挤水声
            SoundEngine.PlaySound(SoundID.NPCHit14 with { Pitch = -0.5f, Volume = 0.7f, MaxInstances = 3 }, context.Npc.Center);
        }

        public override IFishronState OnUpdate(FishronStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            int chargeTime = ChargeTime(context);
            float progress = Math.Min(Timer / (float)chargeTime, 1f);

            //蓄力预警线（服务端生成，锚定本体）
            if (!telegraphSpawned) {
                telegraphSpawned = true;
                if (!VaultUtils.isClient) {
                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center,
                        (player.Center - npc.Center).SafeNormalize(Vector2.UnitX),
                        ModContent.ProjectileType<FishronTelegraph>(), 0, 0f, Main.myPlayer,
                        npc.whoAmI, player.whoAmI, FishronTelegraph.PackParams(0, chargeTime));
                }
            }

            //蓄力期跟瞄，预告线锁定的同一帧冻结方向，锁线即承诺，绝不再变
            if (Timer < chargeTime - FishronTelegraph.LockTime || dashDirection == Vector2.Zero) {
                dashDirection = (player.Center - npc.Center).SafeNormalize(Vector2.UnitY);
            }
            FaceBody(npc, npc.Center + dashDirection * 100f, MathHelper.Lerp(0.24f, 0.02f, progress));

            //迟滞后撤：pow(t,8) 末段猛吸
            float reel = (float)Math.Pow(progress, 8) * 26f;
            Vector2 desired = -dashDirection * (1.4f + reel);
            npc.velocity = Vector2.Lerp(npc.velocity, desired, 0.24f);

            context.SetChargeState(1, progress);
            context.DashDirection = dashDirection;

            //末段咬合定帧
            if (Timer > chargeTime - 10) {
                context.FrameCommand = 1;
            }

            //内聚水汽，72% 截断留死寂
            if (!VaultUtils.isServer && Timer % 2 == 0) {
                FishronMotionFX.SpawnChargeGatherFX(npc.Center, progress);
            }

            Timer++;

            if (Timer >= chargeTime) {
                //一帧写满冲量，Dashing 里回落
                context.ResetChargeState();
                npc.velocity = dashDirection * (DashSpeed(context) * 1.18f);
                npc.netUpdate = true;

                FishronMotionFX.SpawnDashBurst(npc.Center, dashDirection, 1f);
                SoundEngine.PlaySound(SoundID.Zombie20 with { Volume = 0.95f, Pitch = 0.15f, MaxInstances = 3 }, npc.Center);

                return new FishronTidalDashingState(currentDashCount, MaxDashCount(context));
            }

            return null;
        }

        public override void OnExit(FishronStateContext context) {
            base.OnExit(context);
            context.SkipDefaultMovement = false;
        }
    }

    /// <summary>潮汐冲刺·冲刺中：近零转向的暴力直线，复合加速</summary>
    [InnoVault.StateMachines.VaultState((int)FishronStateIndex.TidalDashing, typeof(FishronStateContext))]
    internal class FishronTidalDashingState : FishronStateBase
    {
        public override string StateName => "TidalDashing";
        public override FishronStateIndex StateIndex => FishronStateIndex.TidalDashing;

        internal const int DashDuration = 24;

        private int currentDashCount;
        private int maxDashCount;

        public FishronTidalDashingState() : this(0, 2) {
        }

        public FishronTidalDashingState(int dashCount, int maxCount) {
            currentDashCount = dashCount;
            maxDashCount = maxCount;
        }

        public override void OnEnter(FishronStateContext context) {
            base.OnEnter(context);
            context.SkipDefaultMovement = true;
        }

        public override IFishronState OnUpdate(FishronStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            //初段复合加速，其后指数回巡航
            float cruise = FishronTidalDashPrepareState.DashSpeed(context);
            float speed = npc.velocity.Length();
            if (Timer < 10) {
                speed *= 1.012f;
            }
            else {
                speed = MathHelper.Lerp(speed, cruise, 0.06f);
            }

            //近零转向：直线就是宣言（区别于变轨欺诈冲刺）
            float maxTurn = 0.0045f;
            float heading = npc.velocity.ToRotation();
            float desired = (player.Center - npc.Center).ToRotation();
            heading = heading.AngleTowards(desired, maxTurn);
            npc.velocity = heading.ToRotationVector2() * speed;

            AimBodyAlongVelocity(npc);
            context.FrameCommand = 2;

            //高速甩水
            if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                FishronMotionFX.SpawnSprayCone(
                    npc.Center + Main.rand.NextVector2Circular(34f, 26f),
                    -npc.velocity.SafeNormalize(Vector2.UnitY), 1, 3f, 8f, 0.6f, 0.9f);
            }

            Timer++;

            //冲过身且在远离则提前收束
            bool passedTarget = Timer > 12
                && npc.Distance(player.Center) > 640f
                && Vector2.Dot(npc.velocity.SafeNormalize(Vector2.Zero),
                    (player.Center - npc.Center).SafeNormalize(Vector2.Zero)) < -0.2f;

            if (Timer >= DashDuration || passedTarget) {
                currentDashCount++;
                npc.netUpdate = true;
                return new FishronTidalDashCooldownState(currentDashCount, maxDashCount);
            }

            return null;
        }

        public override void OnExit(FishronStateContext context) {
            base.OnExit(context);
            context.SkipDefaultMovement = false;
        }
    }

    /// <summary>潮汐冲刺·收势：硬刹回卷，接连突或退回悬停</summary>
    [InnoVault.StateMachines.VaultState((int)FishronStateIndex.TidalDashCooldown, typeof(FishronStateContext))]
    internal class FishronTidalDashCooldownState : FishronStateBase
    {
        public override string StateName => "TidalDashCooldown";
        public override FishronStateIndex StateIndex => FishronStateIndex.TidalDashCooldown;

        private const int BrakeTime = 14;

        private int currentDashCount;
        private int maxDashCount;
        private int curlSign;

        public FishronTidalDashCooldownState() : this(1, 2) {
        }

        public FishronTidalDashCooldownState(int dashCount, int maxCount) {
            currentDashCount = dashCount;
            maxDashCount = maxCount;
        }

        public override void OnEnter(FishronStateContext context) {
            base.OnEnter(context);
            context.SkipDefaultMovement = true;
            curlSign = 0;
        }

        public override IFishronState OnUpdate(FishronStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            //回卷方向偏向玩家一侧
            if (curlSign == 0) {
                float cross = Vector2.Dot(npc.velocity.RotatedBy(MathHelper.PiOver2), player.Center - npc.Center);
                curlSign = cross >= 0f ? 1 : -1;
            }

            //甩尾放鲨：二阶段起，收势瞬间从尾后甩出鲨鱼龙咬向玩家落位，
            //逼走位不逼脸——比冲刺慢得多，且吃全场容量顶
            if ((int)Timer == 2 && context.Phase >= 2) {
                SoundEngine.PlaySound(SoundID.Zombie9 with { Volume = 0.7f, Pitch = 0.2f, MaxInstances = 3 }, npc.Center);
                if (!VaultUtils.isClient) {
                    int count = context.Phase >= 3 ? 2 : 1;
                    for (int i = 0; i < count; i++) {
                        Vector2 aim = (player.Center + player.velocity * 14f - npc.Center)
                            .SafeNormalize(Vector2.UnitY).RotatedBy((i - (count - 1) * 0.5f) * 0.3f);
                        FishronSharkronStrafeState.TryLaunchSharkron(npc, npc.Center, aim, 15f);
                    }
                }
            }

            if (Timer < BrakeTime) {
                //硬刹三阶 + 向量旋转甩尾
                float spd = npc.velocity.Length();
                float brake = spd > 40f ? 0.9f : spd > 24f ? 0.93f : 0.96f;
                npc.velocity = npc.velocity.RotatedBy(curlSign * 0.06f) * brake;
                AimBodyAlongVelocity(npc);

                if (!VaultUtils.isServer && Timer % 2 == 0) {
                    FishronMotionFX.SpawnBrakeSpray(npc);
                }
            }
            else {
                context.SkipDefaultMovement = false;
                SetMovement(context, player.Center + new Vector2(0, -320f), 8f, 0.4f);
            }

            int cooldown = (context.Phase >= 2 ? 26 : 34) - (context.IsDeathMode ? 5 : 0);
            Timer++;

            if (Timer >= cooldown) {
                if (currentDashCount >= maxDashCount) {
                    return new FishronHoverState();
                }
                return new FishronTidalDashPrepareState(currentDashCount);
            }

            return null;
        }

        public override void OnExit(FishronStateContext context) {
            base.OnExit(context);
            context.SkipDefaultMovement = false;
        }
    }
}
