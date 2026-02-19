using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core;
using CalamityOverhaul.Content.Projectiles.Boss.SkeletronPrime;
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

        private int ChargeTime => Context.IsMachineRebellion ? 60 : (Context.IsDeathMode ? 65 : 80);
        private int RecoveryTime => Context.IsMachineRebellion ? 90 : (Context.IsDeathMode ? 95 : 110);
        private int MaxSniperCount => Context.IsDeathMode ? 3 : 2;
        private int ProjectileCount => Context.IsMachineRebellion ? 15 : (Context.IsDeathMode ? 13 : 11);
        private float SpreadAngle => Context.IsDeathMode ? 60f : 50f;
        private float BaseSpeed => Context.IsDeathMode ? 7f : 5f;

        private TwinsStateContext Context;
        private int sniperCount;
        private int comboStep;

        public RetinazerPrecisionSniperState(int currentCount = 0, int currentComboStep = 0) {
            sniperCount = currentCount;
            comboStep = currentComboStep;
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
            npc.EntityToRot((player.Center - npc.Center).ToRotation() - MathHelper.PiOver2, 0.16f);

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
                    Vector2 toPlayer = (npc.rotation + MathHelper.PiOver2).ToRotationVector2();
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
                    Vector2 toPlayer = (npc.rotation + MathHelper.PiOver2).ToRotationVector2();
                    float spreadRad = MathHelper.ToRadians(SpreadAngle);

                    for (int i = 0; i < ProjectileCount; i++) {
                        float angle = MathHelper.Lerp(-spreadRad / 2, spreadRad / 2, i / (float)(ProjectileCount - 1));
                        Vector2 shootVel = toPlayer.RotatedBy(angle) * BaseSpeed;
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
                npc.velocity = -(npc.rotation + MathHelper.PiOver2).ToRotationVector2() * 12f;
            }

            //恢复阶段结束
            if (Timer >= RecoveryTime) {
                sniperCount++;

                //独眼模式下切换到狂暴状态
                if (context.IsSoloRageMode) {
                    return new RetinazerSoloRageState();
                }

                if (sniperCount >= MaxSniperCount) {
                    //狙击次数用完，回到垂直弹幕继续套路循环
                    return new RetinazerVerticalBarrageState(comboStep);
                }
                else {
                    //继续下一次狙击
                    return new RetinazerPrecisionSniperState(sniperCount, comboStep);
                }
            }

            return null;
        }
    }
}
