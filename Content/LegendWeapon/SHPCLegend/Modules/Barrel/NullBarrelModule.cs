using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel
{
    /// <summary>归零枪管，低伤无限穿，命中数据侵蚀</summary>
    internal sealed class NullBarrelModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Barrel;
        //灰绿
        public override Color TintColor => new(100, 255, 140);

        public override void Apply(ref ShootContext ctx) {
            ctx.DamageMul += -0.6f;
            ctx.BeamExtraPierce += 99;
            ctx.AttackSpeedMul += 0.66f;
            ctx.HomingMul += 0.24f;
        }

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            if (target.TryGetGlobalNPC(out SHPCNPCEffects eff)) {
                eff.ApplyDataErosion(240, Math.Max(3, damageDone / 10));
            }
        }
    }
}
