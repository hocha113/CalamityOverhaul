using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Frame
{
    /// <summary>超杀机匣，击杀叠层≤15 每层 +1% 伤，衰减交 SHPCPlayer</summary>
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
