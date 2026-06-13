using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core;
using CalamityOverhaul.Content.Projectiles.Boss.MechanicalEye;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Retinazer
{
    /// <summary>精准狙击：蓄力后扇形激光齐射</summary>
    [InnoVault.StateMachines.VaultState((int)TwinsStateIndex.RetinazerPrecisionSniper, typeof(TwinsStateContext))]
    internal class RetinazerPrecisionSniperState : TwinsStateBase
    {
        public override string StateName => "RetinazerPrecisionSniper";
        public override TwinsStateIndex StateIndex => TwinsStateIndex.RetinazerPrecisionSniper;

        private int ChargeTime => Context.IsDeathMode ? 70 : 85;
        private int RecoveryTime => Context.IsDeathMode ? 105 : 120;
        private int MaxSniperCount => Context.IsDeathMode ? 2 : 2;
        private int ProjectileCount => Context.IsDeathMode ? 9 : 8;
        private float SpreadAngle => Context.IsDeathMode ? 60f : 50f;
        private float BaseSpeed => Context.IsDeathMode ? 7f : 5f;

        private TwinsStateContext Context;
        private int sniperCount;
        private int comboStep;

        public RetinazerPrecisionSniperState() : this(0, 0) {
        }

        public RetinazerPrecisionSniperState(int currentCount, int currentComboStep = 0) {
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
                float progress = Timer / (float)ChargeTime;

                //能量内聚
                if (Timer % 2 == 0) {
                    TwinsMotion.ChargeGatherFX(npc.Center, false, progress, 100f);
                }

                //末段绷紧颤抖
                if (progress > 0.75f && !VaultUtils.isServer) {
                    npc.position += Main.rand.NextVector2Circular(1.8f, 1.8f);
                }

                //警告线特效
                if (!VaultUtils.isServer && Timer % 10 == 0 && Timer > 20) {
                    Vector2 toPlayer = (npc.rotation + MathHelper.PiOver2).ToRotationVector2();
                    for (int i = 0; i < 8; i++) {
                        PRTLoader.NewParticle<PRT_TwinsSpark>(npc.Center + toPlayer * (60 + i * 44),
                            toPlayer * 1.5f, Color.White, 0.9f)?.Configure(14, 0);
                    }
                }
            }
            else if (Timer == ChargeTime) {
                //发射扇形强化激光
                context.ResetChargeState();

                if (!VaultUtils.isClient) {
                    Vector2 toPlayer = (npc.rotation + MathHelper.PiOver2).ToRotationVector2();
                    float spreadRad = MathHelper.ToRadians(SpreadAngle);

                    for (int i = 0; i < ProjectileCount; i++) {
                        float angle = MathHelper.Lerp(-spreadRad / 2, spreadRad / 2, i / (float)(ProjectileCount - 1));
                        Vector2 shootVel = toPlayer.RotatedBy(angle) * BaseSpeed;
                        Projectile.NewProjectile(
                            npc.GetSource_FromAI(),
                            npc.Center + toPlayer * 40f,
                            shootVel,
                            ModContent.ProjectileType<RetinazerLaser>(),
                            38,
                            0f,
                            Main.myPlayer,
                            0f,
                            1f//强化弹标记
                        );
                    }
                }

                SoundEngine.PlaySound(SoundID.Item33, npc.Center);

                //强力后坐:机体猛地向后弹开+音爆余波
                Vector2 muzzleDir = (npc.rotation + MathHelper.PiOver2).ToRotationVector2();
                npc.velocity = -muzzleDir * 14f;
                Context.PushDashVisuals(0.7f, 0.8f);
                if (!VaultUtils.isServer) {
                    PRTLoader.NewParticle<PRT_DWave>(npc.Center + muzzleDir * 40f, muzzleDir * 2f, TwinsMotion.RetinColor, 0.18f)?
                        .Configure(new Vector2(1.4f, 0.6f), muzzleDir.ToRotation() + MathHelper.PiOver2, 0.85f, 13);
                    TwinsMotion.Shake(npc.Center, 4.5f, 9);
                }
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
