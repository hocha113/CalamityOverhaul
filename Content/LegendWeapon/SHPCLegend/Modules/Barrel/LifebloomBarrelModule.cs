using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel
{
    /// <summary>生命叶枪管，光束播芽，延迟抽血少量回血</summary>
    internal sealed class LifebloomBarrelModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Barrel;
        public override Color TintColor => new(85, 240, 120);

        public override void Apply(ref ShootContext ctx) {
            ctx.DamageMul += -0.2f;
            ctx.HomingMul += 0.22f;
            ctx.ChargeTimeMul += -0.08f;
            ctx.ManaCostMul += 0.24f;
        }

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            if (target.TryGetGlobalNPC(out SHPCNPCEffects eff)) {
                eff.ApplyLifebloom(target, 360, System.Math.Max(damageDone / 9, 2),
                    beam.Projectile.owner);
            }
        }
    }
}
