using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel
{
    /// <summary>灼烧激光枪管，命中灼烧 DoT</summary>
    internal sealed class ScorchBarrelModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Barrel;
        //橙红
        public override Color TintColor => new(255, 80, 20);

        public override void Apply(ref ShootContext ctx) {
            ctx.LaserMode = true;
            ctx.LaserScorchOnHit = true;
            ctx.LaserScorchDuration = 40;
            ctx.DamageMul += -0.36f;
            ctx.ManaCostMul += 0.78f;
        }

        public override void OnLaserAI(CyberPrismLaserProj laser) {
            //主题换灼烧橙红
            laser.ThemeCore = new Color(255, 160, 30);
            laser.ThemeGlow = new Color(220, 80, 5);
            laser.ThemeAura = new Color(140, 30, 0);
            laser.ThemeParticleMain = new Color(255, 140, 20);
            laser.ThemeParticleEdge = new Color(200, 50, 5);
        }
    }
}
