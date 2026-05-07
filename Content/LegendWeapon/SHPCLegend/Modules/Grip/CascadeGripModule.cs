using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Grip
{
    /// <summary>
    /// 级联握把：累计命中5次后触发节点爆发，在命中点向8方向辐射伤害光束
    /// 计数跨所有光束共享，打出节奏感
    /// </summary>
    internal sealed class CascadeGripModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Grip;
        //级联橙金
        public override Color TintColor => new(255, 190, 40);

        private int _hitCount;

        public override void Apply(ref ShootContext ctx) {
            ctx.DamageMul += -0.1f;
        }

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            if (beam.Projectile.owner != Main.myPlayer) return;
            if (beam.IsDerived) return;
            _hitCount++;
            if (_hitCount < 5) return;
            _hitCount = 0;
            SpawnCascade(beam.Projectile, target.Center, damageDone);
        }

        private static void SpawnCascade(Projectile source, Vector2 origin, int refDamage) {
            int dmg = Math.Max((int)(refDamage * 0.55f), 1);
            const int rays = 8;
            for (int i = 0; i < rays; i++) {
                float angle = MathHelper.TwoPi * i / rays;
                Vector2 vel = angle.ToRotationVector2() * 13f;
                int idx = Projectile.NewProjectile(
                    source.GetSource_FromThis(),
                    origin, vel,
                    ModContent.ProjectileType<CyberTraceBeamProj>(),
                    dmg, 0f, source.owner, ai0: Main.rand.Next(3));
                if (idx >= 0 && idx < Main.maxProjectiles
                    && Main.projectile[idx].ModProjectile is CyberTraceBeamProj ray) {
                    ray.IsDerived = true;
                    ray.LifeMul = 0.4f;
                }
            }
        }
    }
}
