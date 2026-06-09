using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Stock
{
    /// <summary>
    /// 轻量枪托（世界吞噬者）：游走甩枪。玩家移动越快，左键束与飞行中的能量球越容易甩出侧滑副束，
    /// 横向扫射长体敌人时分节追击。副束复用派生光束（带递归保护），不改主弹幕 AI。
    /// </summary>
    internal sealed class LightStockModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Stock;
        //碳纤维浅青
        public override Color TintColor => new(160, 240, 240);

        private const float FastThreshold = 5f;
        private float _speed;

        public override void Apply(ref ShootContext ctx) {
            ctx.AttackSpeedMul += 0.35f;
            ctx.DamageMul += -0.2f;
            ctx.SpreadMul += 0.3f;
        }

        public override void OnPlayerUpdate(Player player) {
            _speed = MathHelper.Lerp(_speed, player.velocity.Length(), 0.2f);
        }

        public override void OnBeamAI(CyberTraceBeamProj beam) {
            if (beam.IsDerived || beam.Projectile.owner != Main.myPlayer || _speed < FastThreshold) return;
            if (((int)Main.GameUpdateCount + beam.Projectile.whoAmI) % 22 != 0) return;
            EmitSkim(beam.Projectile, beam.Projectile.velocity, Math.Max((int)(beam.Projectile.damage * 0.7f), 1));
        }

        public override void OnOrbFlyingAI(CyberChargeOrbProj orb) {
            if (orb.Projectile.owner != Main.myPlayer || _speed < FastThreshold) return;
            if (((int)Main.GameUpdateCount + orb.Projectile.whoAmI) % 16 != 0) return;
            EmitSkim(orb.Projectile, orb.Projectile.velocity, Math.Max(orb.Projectile.damage / 4, 1));
        }

        private static void EmitSkim(Projectile src, Vector2 forward, int dmg) {
            //朝运动方向的侧旁甩出副束，带强追踪让它"分节"咬住目标
            int side = Main.rand.NextBool() ? 1 : -1;
            Vector2 dir = forward.SafeNormalize(Vector2.UnitX).RotatedBy(side * MathHelper.PiOver2);
            Vector2 vel = (dir * 9f) + forward.SafeNormalize(Vector2.UnitX) * 4f;
            SHPCNaturalFx.SpawnDerivedBeam(src, src.Center, vel, dmg, 1.8f, 0.42f);
        }
    }
}
