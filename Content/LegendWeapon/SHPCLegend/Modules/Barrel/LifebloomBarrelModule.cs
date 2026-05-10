using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel
{
    /// <summary>
    /// 生命叶枪管：光束播下生命芽，延迟抽取生命并将少量治疗回流给玩家。
    /// </summary>
    internal sealed class LifebloomBarrelModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Barrel;
        public override Color TintColor => new(85, 240, 120);

        public override void Apply(ref ShootContext ctx) {
            ctx.DamageMul += -0.16f;
            ctx.HomingMul += 0.28f;
            ctx.ChargeTimeMul += -0.1f;
            ctx.ManaCostMul += 0.2f;
        }

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            if (target.TryGetGlobalNPC(out SHPCNPCEffects eff)) {
                eff.ApplyLifebloom(360, System.Math.Max(damageDone / 9, 2), beam.Projectile.owner);
            }
        }
    }
}
