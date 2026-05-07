using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Optic
{
    /// <summary>
    /// 锁定瞄具：命中目标时施加腐化减防效果，配合高频弹幕快速叠加削弱
    /// </summary>
    internal sealed class TargetPainterOpticModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Optic;
        //锁定橙红
        public override Color TintColor => new(255, 130, 30);

        public override void Apply(ref ShootContext ctx) {
            ctx.HomingMul += 0.5f;
            ctx.CritAdd += 3;
        }

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Ichor, 300);
        }

        public override void OnLaserHitNPC(CyberPrismLaserProj laser, NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Ichor, 180);
        }
    }
}
