using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>
    /// 弹道超频：给自己的一发弹幕加料。<br/>
    /// 做成即时而非持续，弹幕随时会消失，没有一个"到期还原"的落点
    /// </summary>
    internal class BallisticOverclock : QuickHackDef
    {
        private const float DamageMul = 1.6f;
        private const int PierceBonus = 2;
        private const int LifeBonus = 60 * 5;

        private static readonly Color Redline = new(255, 180, 60);

        public override void SetDefaults() {
            UploadTime = 60;
            RamCost = 3;
            Category = QuickHackCategory.Lethal;
            SupportedTargets = HackTargetKind.Projectile;
            UnlockedByDefault = false;
        }

        public override bool CanApplyTo(IHackTarget target) {
            if (!base.CanApplyTo(target)) return false;
            if (!HackTargets.TryProjectile(target, out Projectile projectile)) return false;
            //召唤物与哨兵会一直活着，即时协议又没有到期还原，
            //不拦就是同一只随从被反复加料，十次就是一百一十倍
            if (projectile.minion || projectile.sentry) return false;
            if (HackEffectProjectile.IsOverclocked(projectile)) return false;
            return projectile.friendly && !projectile.hostile && projectile.damage > 0;
        }

        public override bool CanApplyTo(IHackTarget target, Player caster) {
            if (!CanApplyTo(target)) return false;
            //只能超频自己的弹，不然队友的弹会被别人乱改
            return caster != null
                && HackTargets.TryProjectile(target, out Projectile projectile)
                && projectile.owner == caster.whoAmI;
        }

        public override bool OnApply(IHackTarget target, Player caster) {
            if (!HackTargets.TryProjectile(target, out Projectile projectile)) return false;

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                //服务端也要打标记，否则重复施放的拦截只存在于 owner 那台机器上
                HackEffectProjectile.MarkOverclocked(projectile);
                //联机里目标弹永远归客户端：服务端改完会被 owner 的周期同步盖回去，
                //而命中结算本来也只在 owner 端跑，那条路交给 OnReplicatedApply
                if (Main.netMode == NetmodeID.SinglePlayer) Overclock(projectile);
            }
            if (Main.netMode != NetmodeID.Server) EmitVisual(projectile);
            return true;
        }

        public override void OnReplicatedApply(IHackTarget target, int elapsed) {
            if (!HackTargets.TryProjectile(target, out Projectile projectile)) return;
            HackEffectProjectile.MarkOverclocked(projectile);
            if (projectile.owner == Main.myPlayer) {
                Overclock(projectile);
                //本机就是 owner，这是原版唯一认的推送路径
                projectile.netUpdate = true;
            }
            EmitVisual(projectile);
        }

        private static void Overclock(Projectile projectile) {
            projectile.damage = (int)(projectile.damage * DamageMul);
            //穿透 -1 是无限穿透，加上去反而会变成有限，别碰
            if (projectile.penetrate > 0) {
                projectile.penetrate += PierceBonus;
            }
            projectile.timeLeft = Math.Min(projectile.timeLeft + LifeBonus, 60 * 60 * 5);
        }

        private static void EmitVisual(Projectile projectile) {
            //顺着飞行方向拉出尾焰，别做成原地爆开的球
            Vector2 forward = projectile.velocity.SafeNormalize(Vector2.UnitX);
            for (int i = 0; i < 16; i++) {
                Vector2 vel = -forward.RotatedByRandom(0.45f) * Main.rand.NextFloat(1.5f, 5f);
                PRTLoader.NewParticle<PRT_SHPCThermalEmber>(projectile.Center, vel,
                    Redline, 0.9f)?.Configure(new Color(120, 30, 10), 26);
            }
        }
    }
}
