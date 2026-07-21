using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Grip
{
    /// <summary>级联握把，5命中生节点，周期射追踪束</summary>
    internal sealed class CascadeGripModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Grip;
        //级联橙金
        public override Color TintColor => new(255, 190, 40);

        private int _hitCount;

        public override void Apply(ref ShootContext ctx) {
            ctx.DamageMul += -0.12f;
        }

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            if (beam.Projectile.owner != Main.myPlayer) return;
            if (beam.IsDerived) return;
            _hitCount++;
            if (_hitCount < 5) return;
            _hitCount = 0;
            SpawnNode(beam.Projectile, target.Center, damageDone);
        }

        private static void SpawnNode(Projectile source, Vector2 origin, int refDamage) {
            int dmg = Math.Max((int)(refDamage * 0.65f), 1);
            int idx = Projectile.NewProjectile(
                source.GetSource_FromThis(),
                origin, Vector2.Zero,
                ModContent.ProjectileType<CyberCascadeNodeProj>(),
                dmg, 0f, source.owner);
            _ = idx;
        }
    }
}
