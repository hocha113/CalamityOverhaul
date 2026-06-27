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
    /// <summary>冲刺蓄力：S形后撤→充能波尾推头→冲量释放+音爆</summary>
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

            //转向率随蓄力衰减，后撤同时锁线
            dashDirection = (player.Center - npc.Center).SafeNormalize(Vector2.UnitY);
            FaceTarget(npc, player.Center, MathHelper.Lerp(0.26f, 0.05f, progress));

            //迟滞后撤：前70%近乎悬停（轻微漂离），pow(t,8)末段猛然吸气式后缩
            float reel = (float)Math.Pow(progress, 8) * 30f;
            Vector2 desired = -dashDirection * (1.6f + reel);
            npc.velocity = Vector2.Lerp(npc.velocity, desired, 0.22f);

            //蓄力进度 + 瞄准线（DestroyerRenderHelper本地绘制）
            context.SetChargeState(1, progress);
            context.DashDirection = dashDirection;

            //下颚：蓄力期弹性张开威吓，释放前12帧猛然咬合
            context.JawCommand = Timer > chargeTime - 12 ? 2 : 1;

            //充能波从尾部涌向头部：能量向头部汇聚
            DestroyerChargeWave.Push(npc.whoAmI, 1f - progress, 0.22f, 0.35f + 0.65f * progress);

            //72%进度硬切粒子/震动，临爆静默
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
                //冲量释放：峰值速度高于巡航冲刺速度，由DashingState指数衰减回落
                context.ResetChargeState();
                npc.velocity = dashDirection * (DashSpeed(context) * 1.3f);
                npc.netUpdate = true;

                if (!VaultUtils.isServer) {
                    //ForceRoar：连突间隔(~1.3s)短于Roar采样时长(~2s)，普通Roar因IgnoreNew上限会整声丢失
                    SoundEngine.PlaySound(SoundID.ForceRoar with { Pitch = 0.2f }, npc.Center);
                    DestroyerMotionFX.SpawnDashBurst(npc.Center, dashDirection);
                    DestroyerMotionFX.CameraPunch(npc.Center, 6f, 14, "DestroyerDash", dashDirection);
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
