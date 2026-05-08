using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Grip
{
    /// <summary>
    /// 级联握把：累计命中5次后在命中点生成悬停级联节点
    /// <br/>节点持续约2秒，周期性向最近敌人猎杀式射出追踪光束（共5次）
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
            SpawnNode(beam.Projectile, target.Center, damageDone);
        }

        private static void SpawnNode(Projectile source, Vector2 origin, int refDamage) {
            int dmg = Math.Max((int)(refDamage * 0.65f), 1);
            int idx = Projectile.NewProjectile(
                source.GetSource_FromThis(),
                origin, Vector2.Zero,
                ModContent.ProjectileType<CyberCascadeNodeProj>(),
                dmg, 0f, source.owner);
            //节点弹幕 damage 已通过上面的 dmg 传入，无需额外字段写入
            _ = idx;
        }
    }
}
