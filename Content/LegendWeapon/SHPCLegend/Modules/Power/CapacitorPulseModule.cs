using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Power
{
    /// <summary>
    /// 脉冲电容：蓄力期间每隔固定帧数从能量球身上释放一次微型脉冲爆破
    /// 用实例计时器避免依赖 SHPCPlayer 等共享状态，仅本机弹幕拥有者侧执行 spawn
    /// </summary>
    internal sealed class CapacitorPulseModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Power;
        //电容亮金
        public override Color TintColor => new(255, 230, 80);

        private const int PulseInterval = 32;
        private int _timer;

        public override void Apply(ref ShootContext ctx) {
            ctx.ChargeTimeMul += 0.1f;
            ctx.OrbExplosionRadiusMul += 0.20f;
            ctx.ManaCostMul += 0.3f;
        }

        public override void OnOrbCharging(CyberChargeOrbProj orb, Player owner) {
            _timer++;
            if (_timer < PulseInterval) return;
            _timer = 0;

            if (Main.netMode != Terraria.ID.NetmodeID.Server) {
                //球面快速喷发的电容粒子环
                Vector2 center = orb.Projectile.Center;
                int count = 14;
                for (int i = 0; i < count; i++) {
                    float angle = MathHelper.TwoPi * i / count;
                    Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(4f, 7f);
                    PRTLoader.AddParticle(new PRT_CyberSquare(
                        center, vel,
                        new Color(255, 240, 120), new Color(255, 180, 30),
                        Main.rand.NextFloat(0.7f, 1.4f), Main.rand.Next(15, 24)));
                }
            }

            if (orb.Projectile.owner != Main.myPlayer) return;
            int dmg = Math.Max(orb.Projectile.damage / 4, 1);
            int idx = Projectile.NewProjectile(orb.Projectile.GetSource_FromThis(),
                orb.Projectile.Center, Vector2.Zero,
                ModContent.ProjectileType<CyberDetonationProj>(),
                dmg, 0f, orb.Projectile.owner, ai0: 0.1f);
            if (idx >= 0 && idx < Main.maxProjectiles) {
                Main.projectile[idx].localAI[2] = 90f;
            }
        }
    }
}
