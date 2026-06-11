using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Rendering;
using CalamityOverhaul.Content.Projectiles.Boss.Destroyer;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.States
{
    /// <summary>
    /// 冲刺蓄力状态：S形盘蛇后撤蓄势 → 充能波尾部涌向头部 → 冲量释放 + 音爆。
    /// 后撤的弧线由航向自然画出，配合体节跟随形成"盘蛇压缩弹簧"的预备动作
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)DestroyerStateIndex.DashPrepare, typeof(DestroyerStateContext))]
    internal class DestroyerDashPrepareState : DestroyerStateBase
    {
        public override string StateName => "DashPrepare";
        public override DestroyerStateIndex StateIndex => DestroyerStateIndex.DashPrepare;

        private int ChargeTime(DestroyerStateContext ctx)
            => (ctx.IsEnraged ? 38 : 52) - (ctx.IsDeathMode ? 6 : 0);
        internal static float DashSpeed(DestroyerStateContext ctx)
            => (ctx.IsEnraged ? 55f : 42f) + (ctx.IsDeathMode ? 4f : 0f);
        private int MaxDashCount(DestroyerStateContext ctx) => ctx.IsEnraged ? 5 : 3;

        private int currentDashCount;
        private Vector2 dashDirection;

        public DestroyerDashPrepareState() : this(0) {
        }

        public DestroyerDashPrepareState(int dashCount) {
            currentDashCount = dashCount;
        }

        public override void OnEnter(DestroyerStateContext context) {
            base.OnEnter(context);
            context.SkipDefaultMovement = true;
            //机械应力声：液压关节锁止蓄力
            SoundEngine.PlaySound(SoundID.NPCHit4 with { Pitch = -0.5f, Volume = 0.7f }, context.Npc.Center);
        }

        public override IDestroyerState OnUpdate(DestroyerStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            int chargeTime = ChargeTime(context);
            float progress = Math.Min(Timer / (float)chargeTime, 1f);

            //对准
            dashDirection = (player.Center - npc.Center).SafeNormalize(Vector2.UnitY);
            FaceTarget(npc, player.Center, 0.18f);

            //S形盘蛇后撤：沿反方向退开并左右摆动，身体随头部压缩出蓄势弯
            float coil = (float)Math.Sin(progress * MathHelper.Pi);
            Vector2 back = -dashDirection;
            Vector2 lateral = dashDirection.RotatedBy(MathHelper.PiOver2)
                * (float)Math.Sin(progress * MathHelper.TwoPi) * 0.65f;
            Vector2 desired = (back + lateral).SafeNormalize(Vector2.Zero) * (9f + 9f * coil);
            npc.velocity = Vector2.Lerp(npc.velocity, desired, 0.12f);

            //蓄力进度 + 瞄准线（DestroyerRenderHelper本地绘制）
            context.SetChargeState(1, progress);
            context.DashDirection = dashDirection;

            //充能波从尾部涌向头部：能量向头部汇聚
            DestroyerChargeWave.Push(npc.whoAmI, 1f - progress, 0.22f, 0.35f + 0.65f * progress);

            //蓄力粒子（仅客户端）
            if (!VaultUtils.isServer && Timer % 3 == 0) {
                for (int i = 0; i < (int)(progress * 5) + 1; i++) {
                    Vector2 dustPos = npc.Center + Main.rand.NextVector2Circular(46, 46);
                    Dust dust = Dust.NewDustDirect(dustPos, 1, 1,
                        DustID.FireworkFountain_Red, 0, 0, 100, default, 1.5f + progress);
                    dust.noGravity = true;
                    dust.velocity = (npc.Center - dustPos).SafeNormalize(Vector2.Zero) * (2f + progress * 4f);
                }
            }

            //蓄力后期震动（仅客户端视觉效果，不影响实际位置）
            if (progress > 0.65f && !VaultUtils.isServer) {
                float shakeMagnitude = (progress - 0.65f) * 5f;
                npc.position += Main.rand.NextVector2Circular(shakeMagnitude, shakeMagnitude);
            }

            Timer++;

            if (Timer >= chargeTime) {
                //冲量释放：峰值速度高于巡航冲刺速度，由DashingState指数衰减回落
                context.ResetChargeState();
                npc.velocity = dashDirection * (DashSpeed(context) * 1.3f);
                npc.netUpdate = true;

                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.2f }, npc.Center);
                    DestroyerMotionFX.SpawnDashBurst(npc.Center, dashDirection);
                    DestroyerMotionFX.CameraPunch(npc.Center, 6f, 14, "DestroyerDash");
                }
                //音爆扭曲环 + 热浪尾流（服务端生成保证多人可见）
                if (!VaultUtils.isClient) {
                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center + dashDirection * 60f,
                        Vector2.Zero, ModContent.ProjectileType<DestroyerShockwave>(), 0, 0f, Main.myPlayer, 0);
                    DestroyerHeatWakeProj.EnsureForHead(npc);
                }

                return new DestroyerDashingState(currentDashCount, MaxDashCount(context));
            }

            return null;
        }

        public override void OnExit(DestroyerStateContext context) {
            base.OnExit(context);
            context.SkipDefaultMovement = false;
        }
    }
}
