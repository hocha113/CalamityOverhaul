using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Frame
{
    /// <summary>
    /// 超杀机匣：击杀目标时叠加超杀层数（最多10层），每层+3%伤害
    /// 层数通过 OnPlayerUpdate 逐帧衰减，打得越快越强
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

        public override void OnPlayerUpdate(Player player) {
            SHPCPlayer sp = SHPCPlayer.Get(player);
            if (sp.OverkillStacks <= 0) return;
            if (sp.OverkillTimer > 0) {
                sp.OverkillTimer--;
                return;
            }
            sp.OverkillStacks--;
            sp.OverkillTimer = 150;
        }
    }
}
