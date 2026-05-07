using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Optic
{
    /// <summary>
    /// 冰霜光学瞄具：追踪光束命中NPC时施加冰霜灼烧减速
    /// 光束速度略降但命中率与暴击同步提升，通过 OnBeamHitNPC 钩子消费
    /// </summary>
    internal sealed class FrostOpticModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Optic;
        //冰霜蓝白
        public override Color TintColor => new(100, 220, 255);

        public override void Apply(ref ShootContext ctx) {
            ctx.BeamSpeedMul += -0.25f;
            ctx.BeamExtraPierce += 1;
            ctx.CritAdd += 4;
            ctx.HomingMul += 0.25f;
        }

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            if (beam.Projectile.owner != Main.myPlayer) return;
            target.AddBuff(BuffID.Frostburn, 180);
        }
    }
}
