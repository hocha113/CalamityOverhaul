using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Grip
{
    /// <summary>
    /// 哨戒握把：能量球飞行期间每20帧朝最近敌人自动发射一束追踪光束
    /// 通过 OnOrbFlyingAI 钩子持续部署，形成主球+伴随光束的双重覆盖
    /// </summary>
    internal sealed class SentinelGripModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Grip;
        //哨戒黄绿
        public override Color TintColor => new(150, 255, 80);

        private int _sentinelTimer;

        public override void Apply(ref ShootContext ctx) {
            ctx.OrbSpeedMul += -0.3f;
            ctx.ManaCostMul += 0.25f;
        }

        public override void OnOrbFlyingAI(CyberChargeOrbProj orb) {
            if (orb.Projectile.owner != Main.myPlayer) return;
            _sentinelTimer++;
            if (_sentinelTimer < 20) return;
            _sentinelTimer = 0;
            NPC target = orb.Projectile.Center.FindClosestNPC(500f, false, true);
            if (target == null) return;
            int dmg = Math.Max(orb.Projectile.damage / 4, 1);
            Vector2 dir = (target.Center - orb.Projectile.Center).SafeNormalize(Vector2.UnitX);
            int idx = Projectile.NewProjectile(orb.Projectile.GetSource_FromThis(),
                orb.Projectile.Center, dir * 14f,
                ModContent.ProjectileType<CyberTraceBeamProj>(),
                dmg, 0f, orb.Projectile.owner, ai0: Main.rand.Next(3));
            if (idx >= 0 && idx < Main.maxProjectiles) {
                Main.projectile[idx].ai[1] = 2.5f;
                if (Main.projectile[idx].ModProjectile is CyberTraceBeamProj beam) {
                    beam.IsDerived = true;
                    beam.LifeMul = 0.5f;
                }
            }
        }
    }
}
