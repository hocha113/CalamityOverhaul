using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Optic
{
    /// <summary>冰霜瞄具：光束命中上冰霜减速 debuff</summary>
    internal sealed class FrostOpticModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Optic;
        //冰霜蓝白
        public override Color TintColor => new(100, 220, 255);

        public override void Apply(ref ShootContext ctx) {
            ctx.BeamSpeedMul += -0.3f;
            ctx.BeamExtraPierce += 1;
            ctx.CritAdd += 4;
            ctx.HomingMul += 0.2f;
        }

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            if (beam.Projectile.owner != Main.myPlayer) return;
            target.AddBuff(BuffID.Frostburn, 180);
        }
    }
}
