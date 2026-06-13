using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core;
using CalamityOverhaul.Content.Projectiles.Boss.MechanicalEye;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Spazmatism
{
    /// <summary>
    /// 魔焰眼一阶段火焰漩涡状态
    /// 在玩家上方悬停并释放环形火焰弹幕
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)TwinsStateIndex.SpazmatismFireVortex, typeof(TwinsStateContext))]
    internal class SpazmatismFireVortexState : TwinsStateBase
    {
        public override string StateName => "SpazmatismFireVortex";
        public override TwinsStateIndex StateIndex => TwinsStateIndex.SpazmatismFireVortex;

        private int ChargeTime => Context.IsDeathMode ? 45 : 60;
        private int TotalDuration => Context.IsDeathMode ? 70 : 90;

        private float MoveSpeed => Context.IsDeathMode ? 14f : 12f;
        private int BulletCount => Context.IsDeathMode ? 10 : 8;
        private float BulletSpeed => Context.IsDeathMode ? 7f : 6f;

        private TwinsStateContext Context;
        private int comboStep;

        public SpazmatismFireVortexState() : this(0) {
        }

        public SpazmatismFireVortexState(int currentComboStep) {
            comboStep = currentComboStep;
        }

        public override void OnEnter(TwinsStateContext context) {
            base.OnEnter(context);
            Context = context;
        }

        public override ITwinsState OnUpdate(TwinsStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            //弹簧悬停到玩家上方
            Vector2 hoverPos = player.Center + new Vector2(0, -300) + TwinsMotion.BreathingOffset(seed: 0.6f, 8f);
            TwinsMotion.SpringHover(npc, hoverPos, 0.016f, 0.085f);
            FaceTarget(npc, player.Center);

            //设置蓄力状态
            context.SetChargeState(3, Math.Min(Timer / (float)ChargeTime, 1f));

            //蓄力阶段
            if (Timer < ChargeTime) {
                float progress = Timer / (float)ChargeTime;
                //能量内聚粒子
                if (Timer % 2 == 0) {
                    TwinsMotion.ChargeGatherFX(npc.Center, true, progress, 95f);
                }
                //末段绷紧颤抖
                if (progress > 0.8f && !VaultUtils.isServer) {
                    npc.position += Main.rand.NextVector2Circular(1.4f, 1.4f);
                }
            }
            else if (Timer == ChargeTime) {
                //释放双层环形火焰弹幕
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item45, npc.Center);
                    //爆发冲击环
                    PRTLoader.NewParticle<PRT_DWave>(npc.Center, Vector2.Zero, TwinsMotion.SpazColor, 0.22f)?
                        .Configure(Vector2.One, 0f, 1.25f, 16);
                    for (int i = 0; i < 14; i++) {
                        PRTLoader.NewParticle<PRT_TwinsSpark>(npc.Center,
                            VaultUtils.RandVr(4, 10), Color.White, Main.rand.NextFloat(1.2f, 2f))?.Configure(18, 1);
                    }
                    TwinsMotion.Shake(npc.Center, 4f, 9);
                }
                if (!VaultUtils.isClient) {
                    //外环:标准速度
                    for (int i = 0; i < BulletCount; i++) {
                        float bulletAngle = MathHelper.TwoPi / BulletCount * i;
                        Vector2 vel = bulletAngle.ToRotationVector2() * BulletSpeed;
                        Projectile.NewProjectile(
                            npc.GetSource_FromAI(),
                            npc.Center,
                            vel,
                            ModContent.ProjectileType<Fireball>(),
                            28,
                            0f,
                            Main.myPlayer
                        );
                    }
                    //内环:半速错位，形成双层波纹
                    int innerCount = BulletCount / 2;
                    for (int i = 0; i < innerCount; i++) {
                        float bulletAngle = MathHelper.TwoPi / innerCount * i + MathHelper.Pi / BulletCount;
                        Vector2 vel = bulletAngle.ToRotationVector2() * BulletSpeed * 0.55f;
                        Projectile.NewProjectile(
                            npc.GetSource_FromAI(),
                            npc.Center,
                            vel,
                            ModContent.ProjectileType<Fireball>(),
                            25,
                            0f,
                            Main.myPlayer
                        );
                    }
                }
                //释放后坐:整体下沉缓冲
                npc.velocity += Main.rand.NextVector2Unit() * 3f;
                context.ResetChargeState();
            }

            Timer++;

            //状态结束，回到悬停射击继续套路循环
            if (Timer >= TotalDuration) {
                return new SpazmatismHoverShootState(comboStep);
            }

            return null;
        }
    }
}
