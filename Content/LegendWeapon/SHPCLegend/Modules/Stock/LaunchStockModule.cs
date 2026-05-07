using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Stock
{
    /// <summary>
    /// 弹射枪托：能量球发射瞬间额外喷出3束扇形追踪副光束
    /// 通过 OnOrbLaunched 钩子在状态切换为飞行的瞬间执行，与主球形成饱和覆盖
    /// </summary>
    internal sealed class LaunchStockModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Stock;
        //发射橙
        public override Color TintColor => new(255, 150, 60);

        public override void Apply(ref ShootContext ctx) {
            ctx.OrbSpeedMul += 0.30f;
            ctx.ManaCostMul += 0.25f;
        }

        public override void OnOrbLaunched(CyberChargeOrbProj orb) {
            if (orb.Projectile.owner != Main.myPlayer) return;
            int dmg = Math.Max(orb.Projectile.damage / 3, 1);
            //以能量球飞行方向为中轴，左偏60°/正向/右偏60°形成Y字
            float baseAngle = orb.Projectile.rotation;
            float[] offsets = { -MathHelper.Pi / 3f, 0f, MathHelper.Pi / 3f };
            for (int i = 0; i < 3; i++) {
                float ang = baseAngle + offsets[i];
                Vector2 vel = ang.ToRotationVector2() * 12f;
                int idx = Projectile.NewProjectile(orb.Projectile.GetSource_FromThis(),
                    orb.Projectile.Center, vel,
                    ModContent.ProjectileType<CyberTraceBeamProj>(),
                    dmg, 0f, orb.Projectile.owner, ai0: Main.rand.Next(3));
                if (idx >= 0 && idx < Main.maxProjectiles) {
                    Main.projectile[idx].ai[1] = 2f;
                    if (Main.projectile[idx].ModProjectile is CyberTraceBeamProj beam) {
                        beam.IsDerived = true;
                        beam.LifeMul = 0.6f;
                    }
                }
            }
        }
    }
}
