using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core;
using CalamityOverhaul.Content.Projectiles.Boss.SkeletronPrime;
using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Retinazer
{
    /// <summary>
    /// 激光眼二阶段精准狙击状态
    /// 蓄力后发射扇形激光弹幕
    /// </summary>
    internal class RetinazerPrecisionSniperState : TwinsStateBase
    {
        public override string StateName => "RetinazerPrecisionSniper";

        private const int ChargeTime = 80;
        private const int RecoveryTime = 110;
        private const int MaxSniperCount = 3;

        private TwinsStateContext Context;
        private int sniperCount;

        public RetinazerPrecisionSniperState(int currentCount = 0) {
            sniperCount = currentCount;
        }

        public override void OnEnter(TwinsStateContext context) {
            base.OnEnter(context);
            Context = context;
        }

        public override ITwinsState OnUpdate(TwinsStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            //减速
            npc.velocity *= 0.9f;
            FaceTarget(npc, player.Center);

            //设置蓄力状态
            context.SetChargeState(2, Math.Min(Timer / (float)ChargeTime, 1f));

            Timer++;

            //蓄力阶段
            if (Timer < ChargeTime) {
                //产生聚集的激光粒子
                if (!VaultUtils.isServer && Timer % 2 == 0) {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    float dist = 100f - (Timer / (float)ChargeTime) * 60f;
                    Vector2 dustPos = npc.Center + angle.ToRotationVector2() * dist;
                    Dust dust = Dust.NewDustDirect(dustPos, 1, 1, DustID.PurpleTorch, 0, 0, 100, default, 1.8f);
                    dust.noGravity = true;
                    dust.velocity = (npc.Center - dustPos).SafeNormalize(Vector2.Zero) * 5f;
                }

                //警告线特效
                if (!VaultUtils.isServer && Timer % 10 == 0 && Timer > 20) {
                    Vector2 toPlayer = GetDirectionToTarget(context);
                    for (int i = 0; i < 8; i++) {
                        Vector2 dustPos = npc.Center + toPlayer * (50 + i * 40);
                        Dust dust = Dust.NewDustDirect(dustPos, 1, 1, DustID.RedTorch, 0, 0, 150, default, 1.2f);
                        dust.noGravity = true;
                        dust.velocity = Vector2.Zero;
                    }
                }
            }
            else if (Timer == ChargeTime) {
                //发射扇形激光
                context.ResetChargeState();

                if (!VaultUtils.isClient) {
                    Vector2 toPlayer = GetDirectionToTarget(context);
                    int projectileCount = 11;
                    float spreadAngle = MathHelper.ToRadians(50);
                    float baseSpeed = context.IsDeathMode ? 7f : 5f;

                    for (int i = 0; i < projectileCount; i++) {
                        float angle = MathHelper.Lerp(-spreadAngle / 2, spreadAngle / 2, i / (float)(projectileCount - 1));
                        Vector2 shootVel = toPlayer.RotatedBy(angle) * baseSpeed;
                        Projectile.NewProjectile(
                            npc.GetSource_FromAI(),
                            npc.Center,
                            shootVel,
                            ModContent.ProjectileType<DeadLaser>(),
                            50,
                            0f,
                            Main.myPlayer
                        );
                    }
                }

                SoundEngine.PlaySound(SoundID.Item33, npc.Center);

                //后坐力
                npc.velocity = -GetDirectionToTarget(context) * 12f;
            }

            //恢复阶段结束
            if (Timer >= RecoveryTime) {
                sniperCount++;

                if (sniperCount >= MaxSniperCount) {
                    //狙击次数用完，回到垂直弹幕
                    return new RetinazerVerticalBarrageState();
                }
                else {
                    //继续下一次狙击
                    return new RetinazerPrecisionSniperState(sniperCount);
                }
            }

            return null;
        }
    }
}
