using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Stock
{
    /// <summary>弹射枪托，球发射瞬间扇形 3 束追踪副光束</summary>
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
            //中轴±60° Y字三束
            float baseAngle = orb.Projectile.rotation;
            float[] offsets = { -MathHelper.Pi / 3f, 0f, MathHelper.Pi / 3f };
            for (int i = 0; i < 3; i++) {
                float ang = baseAngle + offsets[i];
                Vector2 vel = ang.ToRotationVector2() * 12f;
                //ai1 追踪倍率走生成参数，生成包在 NewProjectile 内部即发出，后置赋值到不了远端
                int idx = Projectile.NewProjectile(orb.Projectile.GetSource_FromThis(),
                    orb.Projectile.Center, vel,
                    ModContent.ProjectileType<CyberTraceBeamProj>(),
                    dmg, 0f, orb.Projectile.owner, ai0: Main.rand.Next(3), ai1: 2f);
                if (idx >= 0 && idx < Main.maxProjectiles
                    && Main.projectile[idx].ModProjectile is CyberTraceBeamProj beam) {
                    //owner 本地字段，共享光束无 SendExtraAI 通道
                    beam.IsDerived = true;
                    beam.LifeMul = 0.6f;
                }
            }
        }
    }
}
