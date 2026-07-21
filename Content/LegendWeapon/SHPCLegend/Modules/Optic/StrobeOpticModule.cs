using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Optic
{
    /// <summary>频闪瞄具，每 16 帧交替穿墙，命中 30% 混乱</summary>
    internal sealed class StrobeOpticModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Optic;
        //频闪白蓝
        public override Color TintColor => new(180, 220, 255);

        public override void Apply(ref ShootContext ctx) {
            ctx.BeamExtraPierce += 1;
            ctx.AttackSpeedMul += 0.06f;
        }

        public override void OnBeamAI(CyberTraceBeamProj beam) {
            //16 帧半周期穿墙
            beam.Projectile.tileCollide = Main.GameUpdateCount % 16 < 8;
        }

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.rand.NextFloat() < 0.3f) {
                target.AddBuff(BuffID.Confused, 60);
            }
        }
    }
}
