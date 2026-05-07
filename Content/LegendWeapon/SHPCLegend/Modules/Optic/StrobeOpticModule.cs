using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Optic
{
    /// <summary>
    /// 频闪瞄具：光束每16帧交替切换穿墙状态，模拟数据频闪穿透
    /// 命中时有30%概率使目标混乱
    /// </summary>
    internal sealed class StrobeOpticModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Optic;
        //频闪白蓝
        public override Color TintColor => new(180, 220, 255);

        public override void Apply(ref ShootContext ctx) {
            ctx.BeamExtraPierce += 1;
            ctx.AttackSpeedMul += 0.08f;
        }

        public override void OnBeamAI(CyberTraceBeamProj beam) {
            //以游戏帧为周期切换穿墙，使光束在墙体内外间歇穿越
            beam.Projectile.tileCollide = Main.GameUpdateCount % 16 < 8;
        }

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.rand.NextFloat() < 0.3f) {
                target.AddBuff(BuffID.Confused, 60);
            }
        }
    }
}
