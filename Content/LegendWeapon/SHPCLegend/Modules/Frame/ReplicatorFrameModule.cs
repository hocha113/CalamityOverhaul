using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Frame
{
    /// <summary>
    /// 数据复制机匣：光束消亡时在原位生成一束短命的低伤回响光束
    /// 与 RecursiveFrame 区别：副本就地生成、寿命极短、永不递归
    /// </summary>
    internal sealed class ReplicatorFrameModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Frame;
        //复制蓝绿青
        public override Color TintColor => new(60, 220, 230);

        public override void Apply(ref ShootContext ctx) {
            ctx.BeamLifeMul += -0.3f;
            ctx.DamageMul += 0.1f;
            ctx.ManaCostMul += 0.20f;
        }

        public override void OnBeamKill(CyberTraceBeamProj beam, int timeLeft) {
            if (beam.IsDerived || beam.Projectile.owner != Main.myPlayer) return;
            int dmg = Math.Max((int)(beam.Projectile.damage * 0.55f), 1);
            //回响沿原方向再走一段，初速略低
            Vector2 vel = beam.Projectile.velocity.SafeNormalize(Vector2.UnitX) * 12f;
            int idx = Projectile.NewProjectile(beam.Projectile.GetSource_FromThis(),
                beam.Projectile.Center, vel,
                ModContent.ProjectileType<CyberTraceBeamProj>(),
                dmg, 0f, beam.Projectile.owner,
                ai0: (int)beam.Projectile.ai[0]);
            if (idx >= 0 && idx < Main.maxProjectiles) {
                Main.projectile[idx].ai[1] = MathHelper.Max(beam.Projectile.ai[1], 1.5f);
                if (Main.projectile[idx].ModProjectile is CyberTraceBeamProj echo) {
                    echo.IsDerived = true;
                    echo.LifeMul = 0.30f;
                    echo.SpeedMul = beam.SpeedMul;
                    echo.ExtraPierce = beam.ExtraPierce;
                }
            }
        }
    }
}
