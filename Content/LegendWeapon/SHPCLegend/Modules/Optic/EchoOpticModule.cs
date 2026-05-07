using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Optic
{
    /// <summary>
    /// 回响光学瞄具：追踪光束消亡时在消亡点释放3束向外扇射的强追踪副光束
    /// 通过 OnBeamKill 钩子实现，派生标记阻止递归
    /// </summary>
    internal sealed class EchoOpticModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Optic;
        //幽灵蓝紫
        public override Color TintColor => new(80, 150, 255);

        public override void Apply(ref ShootContext ctx) {
            ctx.BeamLifeMul += -0.30f;
            ctx.DamageMul += -0.15f;
            ctx.ManaCostMul += 0.25f;
        }

        public override void OnBeamKill(CyberTraceBeamProj beam, int timeLeft) {
            if (beam.IsDerived || beam.Projectile.owner != Main.myPlayer) return;
            int dmg = Math.Max(beam.Projectile.damage, 1);
            float baseAngle = beam.Projectile.velocity.ToRotation();
            for (int i = 0; i < 3; i++) {
                float ang = baseAngle + MathHelper.Lerp(-MathHelper.PiOver2, MathHelper.PiOver2, (float)i / 2);
                Vector2 vel = ang.ToRotationVector2() * 14f;
                int idx = Projectile.NewProjectile(beam.Projectile.GetSource_FromThis(),
                    beam.Projectile.Center, vel,
                    ModContent.ProjectileType<CyberTraceBeamProj>(),
                    dmg, 0f, beam.Projectile.owner, ai0: Main.rand.Next(3));
                if (idx >= 0 && idx < Main.maxProjectiles) {
                    Main.projectile[idx].ai[1] = 2.2f;
                    if (Main.projectile[idx].ModProjectile is CyberTraceBeamProj child) {
                        child.IsDerived = true;
                        child.LifeMul = 0.45f;
                    }
                }
            }
        }
    }
}
