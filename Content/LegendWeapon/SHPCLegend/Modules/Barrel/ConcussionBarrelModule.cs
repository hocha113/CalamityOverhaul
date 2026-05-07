using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel
{
    /// <summary>
    /// 震荡膛口：连续命中累计计数，每达到阈值在最近一次命中处释放一次微型震荡环
    /// 通过实例字段独立维护计数器，复用 <see cref="CyberDetonationProj"/> 表达冲击波
    /// </summary>
    internal sealed class ConcussionBarrelModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Barrel;
        //震荡浅金
        public override Color TintColor => new(255, 200, 90);

        private const int HitsPerPulse = 5;
        private int _hitCount;

        public override void Apply(ref ShootContext ctx) {
            ctx.DamageMul += 0.15f;
            ctx.AttackSpeedMul += -0.35f;
            ctx.SpreadMul += -0.15f;
        }

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            if (beam.Projectile.owner != Main.myPlayer) return;
            _hitCount++;
            if (_hitCount < HitsPerPulse) return;
            _hitCount = 0;
            int dmg = Math.Max((int)(beam.Projectile.damage * 0.55f), 1);
            int idx = Projectile.NewProjectile(beam.Projectile.GetSource_FromThis(),
                target.Center, Vector2.Zero,
                ModContent.ProjectileType<CyberDetonationProj>(),
                dmg, beam.Projectile.knockBack, beam.Projectile.owner,
                ai0: 0.15f);
            if (idx >= 0 && idx < Main.maxProjectiles) {
                //强制为小半径震荡环（70px），避免和大爆炸改件视觉混淆
                Main.projectile[idx].localAI[2] = 70f;
            }
        }
    }
}
