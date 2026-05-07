using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel
{
    /// <summary>
    /// 灼烧激光枪管：激光命中NPC时施加持续灼烧debuff
    /// 单次伤害较低但DoT填补输出缺口，通过 ScorchOnHit 字段消费
    /// </summary>
    internal sealed class ScorchBarrelModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Barrel;
        //灼焰橙红
        public override Color TintColor => new(255, 80, 20);

        public override void Apply(ref ShootContext ctx) {
            ctx.LaserMode = true;
            ctx.LaserScorchOnHit = true;
            ctx.LaserScorchDuration = 240;
            ctx.DamageMul += -0.25f;
            ctx.ManaCostMul += 0.65f;
        }

        public override void OnLaserAI(CyberPrismLaserProj laser) {
            //将光束颜色主题替换为灼烧橙红配色
            laser.ThemeCore = new Color(255, 160, 30);
            laser.ThemeGlow = new Color(220, 80, 5);
            laser.ThemeAura = new Color(140, 30, 0);
            laser.ThemeParticleMain = new Color(255, 140, 20);
            laser.ThemeParticleEdge = new Color(200, 50, 5);
        }
    }
}
