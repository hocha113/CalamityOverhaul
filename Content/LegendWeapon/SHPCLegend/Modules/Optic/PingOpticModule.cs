using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Optic
{
    /// <summary>标记瞄具：命中上 MarkedforDeath</summary>
    internal sealed class PingOpticModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Optic;
        //标记霓虹粉
        public override Color TintColor => new(255, 100, 200);

        public override void Apply(ref ShootContext ctx) {
            ctx.CritAdd += 4;
        }

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            if (beam.Projectile.owner != Main.myPlayer) return;
            target.AddBuff(CWRID.Buff_MarkedforDeath, 240);
        }

        public override void OnLaserHitNPC(CyberPrismLaserProj laser, NPC target, NPC.HitInfo hit, int damageDone) {
            if (laser.Projectile.owner != Main.myPlayer) return;
            target.AddBuff(CWRID.Buff_MarkedforDeath, 120);
        }
    }
}
