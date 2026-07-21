using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Rendering;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.States
{
    /// <summary>冲刺蓄力，S后撤→尾推头→释放+音爆</summary>
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
            //液压锁止应力声
            SoundEngine.PlaySound(SoundID.NPCHit4 with { Pitch = -0.5f, Volume = 0.7f }, context.Npc.Center);
        }

        public override IDestroyerState OnUpdate(DestroyerStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            int chargeTime = ChargeTime(context);
            float progress = Math.Min(Timer / (float)chargeTime, 1f);

            //蓄力降转向，后撤锁线
            dashDirection = (player.Center - npc.Center).SafeNormalize(Vector2.UnitY);
            FaceTarget(npc, player.Center, MathHelper.Lerp(0.26f, 0.05f, progress));

            //迟滞后撤，pow(t,8)末段急缩
            float reel = (float)Math.Pow(progress, 8) * 30f;
            Vector2 desired = -dashDirection * (1.6f + reel);
            npc.velocity = Vector2.Lerp(npc.velocity, desired, 0.22f);

            //蓄力进度+瞄准线
            context.SetChargeState(1, progress);
            context.DashDirection = dashDirection;

            //蓄力张口，释放前12f咬合
            context.JawCommand = Timer > chargeTime - 12 ? 2 : 1;

            //充能波尾→头
            DestroyerChargeWave.Push(npc.whoAmI, 1f - progress, 0.22f, 0.35f + 0.65f * progress);

            //72%硬切粒子，临爆静默
            if (progress < 0.72f && !VaultUtils.isServer) {
                if (Timer % 3 == 0) {
                    for (int i = 0; i < (int)(progress * 5) + 1; i++) {
                        Vector2 dustPos = npc.Center + Main.rand.NextVector2Circular(46, 46);
                        Dust dust = Dust.NewDustDirect(dustPos, 1, 1,
                            DustID.FireworkFountain_Red, 0, 0, 100, default, 1.5f + progress);
                        dust.noGravity = true;
                        dust.velocity = (npc.Center - dustPos).SafeNormalize(Vector2.Zero) * (2f + progress * 4f);
                    }
                }
                if (progress > 0.4f) {
                    float shakeMagnitude = (progress - 0.4f) * 5f;
                    npc.position += Main.rand.NextVector2Circular(shakeMagnitude, shakeMagnitude);
                }
            }

            Timer++;

            if (Timer >= chargeTime) {
                //冲量释放，DashingState衰减
                context.ResetChargeState();
                npc.velocity = dashDirection * (DashSpeed(context) * 1.3f);
                npc.netUpdate = true;

                if (!VaultUtils.isServer) {
                    //ForceRoar，间隔短于采样会被IgnoreNew吞
                    SoundEngine.PlaySound(SoundID.ForceRoar with { Pitch = 0.2f }, npc.Center);
                    DestroyerMotionFX.SpawnDashBurst(npc.Center, dashDirection);
                    DestroyerMotionFX.CameraPunch(npc.Center, 6f, 14, "DestroyerDash", dashDirection);
                }
                //音爆+热浪，服务端生成
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
