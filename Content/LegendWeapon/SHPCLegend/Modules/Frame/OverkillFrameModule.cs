using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Frame
{
    /// <summary>
    /// 超杀机匣：击杀目标时叠加超杀层数（最多15层），每层+2%伤害
    /// 层数衰减与卸件清零统一由 <see cref="SHPCPlayer.PostUpdate"/> 托管，
    /// 保证改件卸下后增伤不会残留
    /// </summary>
    internal sealed class OverkillFrameModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Frame;
        //超杀血金
        public override Color TintColor => new(255, 100, 20);

        public override void Apply(ref ShootContext ctx) { }

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            if (beam.Projectile.owner != Main.myPlayer) return;
            if (target.life > 0) return;
            SHPCPlayer sp = SHPCPlayer.Get(Main.player[beam.Projectile.owner]);
            sp.OverkillStacks = System.Math.Min(sp.OverkillStacks + 1, 15);
            sp.OverkillTimer = 240;
        }
    }
}
