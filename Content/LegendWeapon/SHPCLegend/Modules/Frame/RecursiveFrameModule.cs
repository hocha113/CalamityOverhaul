using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Frame
{
    /// <summary>
    /// 递归机匣：追踪光束消亡时从玩家位置沿原方向重发一束伤害略低的副本
    /// 通过 OnBeamKill 钩子实现，派生标记 IsDerived 防止无限递归
    /// </summary>
    internal sealed class RecursiveFrameModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Frame;
        //递归暗金
        public override Color TintColor => new(200, 160, 40);

        public override void Apply(ref ShootContext ctx) {
            ctx.BeamLifeMul += -0.25f;
            ctx.DamageMul += 0.1f;
            ctx.ManaCostMul += 0.6f;
        }

        public override void OnBeamKill(CyberTraceBeamProj beam, int timeLeft) {
            if (beam.IsDerived || beam.Projectile.owner != Main.myPlayer) return;
            Player owner = Main.player[beam.Projectile.owner];
            if (owner == null || !owner.active) return;
            int dmg = Math.Max((int)(beam.Projectile.damage * 0.6f), 1);
            Vector2 dir = beam.Projectile.velocity.SafeNormalize(Vector2.UnitX);
            int idx = Projectile.NewProjectile(beam.Projectile.GetSource_FromThis(),
                owner.Center, dir * 14f,
                ModContent.ProjectileType<CyberTraceBeamProj>(),
                dmg, 0f, beam.Projectile.owner, ai0: Main.rand.Next(3));
            if (idx >= 0 && idx < Main.maxProjectiles) {
                Main.projectile[idx].ai[1] = beam.Projectile.ai[1];
                if (Main.projectile[idx].ModProjectile is CyberTraceBeamProj child) {
                    child.IsDerived = true;
                    child.LifeMul = 0.8f;
                    child.ExtraPierce = beam.ExtraPierce;
                    child.ChainCount = beam.ChainCount;
                    child.ChainRange = beam.ChainRange;
                    child.ExplodeOnHit = beam.ExplodeOnHit;
                    child.ExplodeRadius = beam.ExplodeRadius;
                }
            }
        }
    }
}
