using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Optic
{
    /// <summary>
    /// 标记瞄具：命中目标时为其打上 <see cref="BuffID.MarkedforDeath"/> 标记
    /// 后续命中（含其他模块/玩家伤害源）都被增益放大，配合多发场景效果显著
    /// </summary>
    internal sealed class PingOpticModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Optic;
        //标记霓虹粉
        public override Color TintColor => new(255, 100, 200);

        public override void Apply(ref ShootContext ctx) {
            ctx.CritAdd += 5;
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
