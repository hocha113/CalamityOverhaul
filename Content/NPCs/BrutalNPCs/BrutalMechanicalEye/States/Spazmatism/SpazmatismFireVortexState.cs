using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core;
using CalamityOverhaul.Content.Projectiles.Boss.MechanicalEye;
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
    internal class SpazmatismFireVortexState : TwinsStateBase
    {
        public override string StateName => "SpazmatismFireVortex";
        public override TwinsStateIndex StateIndex => TwinsStateIndex.SpazmatismFireVortex;

        private int ChargeTime => Context.IsMachineRebellion ? 40 : (Context.IsDeathMode ? 45 : 60);
        private int TotalDuration => Context.IsMachineRebellion ? 60 : (Context.IsDeathMode ? 70 : 90);

        private float MoveSpeed => Context.IsMachineRebellion ? 16f : (Context.IsDeathMode ? 14f : 12f);
        private int BulletCount => Context.IsMachineRebellion ? 12 : (Context.IsDeathMode ? 10 : 8);
        private float BulletSpeed => Context.IsDeathMode ? 7f : 6f;

        private TwinsStateContext Context;
        private int comboStep;

        public SpazmatismFireVortexState(int currentComboStep = 0) {
            comboStep = currentComboStep;
        }

        public override void OnEnter(TwinsStateContext context) {
            base.OnEnter(context);
            Context = context;
        }

        public override ITwinsState OnUpdate(TwinsStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            //移动到玩家上方
            Vector2 hoverPos = player.Center + new Vector2(0, -300);
            MoveTo(npc, hoverPos, MoveSpeed * 0.8f, 0.08f);
            FaceTarget(npc, player.Center);

            //设置蓄力状态
            context.SetChargeState(3, Math.Min(Timer / (float)ChargeTime, 1f));

            //蓄力阶段
            if (Timer < ChargeTime) {
                //产生聚集的火焰粒子
                if (!VaultUtils.isServer && Timer % 3 == 0) {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 dustPos = npc.Center + angle.ToRotationVector2() * 80f;
                    Dust dust = Dust.NewDustDirect(dustPos, 1, 1, DustID.SolarFlare, 0, 0, 100, default, 1.8f);
                    dust.noGravity = true;
                    dust.velocity = (npc.Center - dustPos).SafeNormalize(Vector2.Zero) * 4f;
                }
            }
            else if (Timer == ChargeTime) {
                //释放环形火焰弹幕
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item45, npc.Center);
                }
                if (!VaultUtils.isClient) {
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
                }
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
