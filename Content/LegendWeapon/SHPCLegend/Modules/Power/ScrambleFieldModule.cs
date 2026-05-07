using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Power
{
    /// <summary>
    /// 扰场核心：蓄力期间每隔40帧在球体周围触发微型冲击波
    /// 通过 OnOrbCharging 钩子生成削弱版 CyberDetonationProj，蓄力阶段持续范围输出
    /// </summary>
    internal sealed class ScrambleFieldModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Power;
        //扰场干扰绿
        public override Color TintColor => new(80, 255, 100);

        private int _scrambleTimer;

        public override void Apply(ref ShootContext ctx) {
            ctx.ChargeTimeMul += 0.25f;
            ctx.OrbExplosionRadiusMul += 0.5f;
            ctx.ManaCostMul += 0.4f;
        }

        public override void OnOrbCharging(CyberChargeOrbProj orb, Player owner) {
            if (orb.Projectile.owner != Main.myPlayer) return;
            _scrambleTimer++;
            if (_scrambleTimer < 40) return;
            _scrambleTimer = 0;
            Vector2 offset = Main.rand.NextVector2CircularEdge(65f, 65f);
            int dmg = Math.Max(orb.Projectile.damage / 5, 1);
            int idx = Projectile.NewProjectile(orb.Projectile.GetSource_FromThis(),
                orb.Projectile.Center + offset, Vector2.Zero,
                ModContent.ProjectileType<CyberDetonationProj>(),
                dmg, 0f, orb.Projectile.owner, ai0: 0.2f);
            if (idx >= 0 && idx < Main.maxProjectiles) {
                Main.projectile[idx].localAI[2] = 40f;
            }
        }
    }
}
