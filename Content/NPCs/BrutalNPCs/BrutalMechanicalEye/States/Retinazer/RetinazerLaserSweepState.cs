using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core;
using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Retinazer
{
    /// <summary>
    /// 激光眼一阶段激光扫射状态
    /// 在玩家上方悬停并发射扫射激光
    /// </summary>
    internal class RetinazerLaserSweepState : TwinsStateBase
    {
        public override string StateName => "RetinazerLaserSweep";

        private const int ChargeTime = 50;
        private const int SweepDuration = 60;
        private const int TotalDuration = 115;

        private float MoveSpeed => Context.IsMachineRebellion ? 14f : 10f;

        private TwinsStateContext Context;

        public override void OnEnter(TwinsStateContext context) {
            base.OnEnter(context);
            Context = context;
        }

        public override ITwinsState OnUpdate(TwinsStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            //移动到玩家上方
            Vector2 sweepPos = player.Center + new Vector2(0, -400);
            MoveTo(npc, sweepPos, MoveSpeed * 0.6f, 0.1f);

            //蓄力阶段
            if (Timer < ChargeTime) {
                context.SetChargeState(4, Math.Min(Timer / (float)ChargeTime, 1f));
                FaceTarget(npc, player.Center);

                //产生聚集的激光粒子
                if (!VaultUtils.isServer && Timer % 3 == 0) {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 dustPos = npc.Center + angle.ToRotationVector2() * 70f;
                    Dust dust = Dust.NewDustDirect(dustPos, 1, 1, DustID.PurpleTorch, 0, 0, 100, default, 1.6f);
                    dust.noGravity = true;
                    dust.velocity = (npc.Center - dustPos).SafeNormalize(Vector2.Zero) * 4f;
                }
            }
            else {
                //扫射阶段
                context.ResetChargeState();

                float sweepTime = Timer - ChargeTime;
                float sweepProgress = sweepTime / (float)SweepDuration;
                float sweepAngle = MathHelper.Lerp(-MathHelper.PiOver4, MathHelper.PiOver4, sweepProgress);

                Vector2 baseDir = (player.Center - npc.Center).SafeNormalize(Vector2.UnitY);
                npc.rotation = baseDir.RotatedBy(sweepAngle).ToRotation() - MathHelper.PiOver2;

                //每8帧发射一发激光
                if ((int)sweepTime % 8 == 0 && !VaultUtils.isClient) {
                    Vector2 shootDir = baseDir.RotatedBy(sweepAngle);
                    Projectile.NewProjectile(
                        npc.GetSource_FromAI(),
                        npc.Center,
                        shootDir * 10f,
                        ProjectileID.DeathLaser,
                        20,
                        0f,
                        Main.myPlayer
                    );
                    SoundEngine.PlaySound(SoundID.Item12 with { Pitch = 0.3f }, npc.Center);
                }
            }

            Timer++;

            //状态结束
            if (Timer >= TotalDuration) {
                return new RetinazerHoverShootState();
            }

            return null;
        }
    }
}
