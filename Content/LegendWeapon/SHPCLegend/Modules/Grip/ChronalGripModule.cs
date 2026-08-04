using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Grip
{
    /// <summary>时相握把，命中减速</summary>
    internal sealed class ChronalGripModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Grip;
        //时空蓝紫
        public override Color TintColor => new(100, 80, 220);

        public override void Apply(ref ShootContext ctx) {
            ctx.HomingMul += 0.24f;
            ctx.DamageMul += -0.05f;
        }

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            if (target.TryGetGlobalNPC(out SHPCNPCEffects eff)) {
                eff.ApplyChronalSlow(target, 120);
            }
        }

        public override void OnLaserHitNPC(CyberPrismLaserProj laser, NPC target, NPC.HitInfo hit, int damageDone) {
            if (target.TryGetGlobalNPC(out SHPCNPCEffects eff)) {
                eff.ApplyChronalSlow(target, 60);
            }
        }
    }
}
