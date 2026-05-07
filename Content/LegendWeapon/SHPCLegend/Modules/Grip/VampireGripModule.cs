using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Grip
{
    /// <summary>
    /// 虬血握把：光束与激光命中时按伤害比例吸血回复生命值
    /// 持久战场景下提供一定的续战能力
    /// </summary>
    internal sealed class VampireGripModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Grip;
        //暗红血色
        public override Color TintColor => new(200, 40, 60);

        public override void Apply(ref ShootContext ctx) {
            ctx.DamageMul += -0.5f;
        }

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {

            Player owner = Main.player[beam.Projectile.owner];
            if (owner == null || !owner.active) return;
            int heal = System.Math.Min(damageDone / 100, 3);
            if (heal <= 0) return;
            owner.statLife = System.Math.Min(owner.statLife + heal, owner.statLifeMax2);
            CombatText.NewText(owner.getRect(), new Color(80, 230, 80), heal, dramatic: false, dot: true);
        }

        public override void OnLaserHitNPC(CyberPrismLaserProj laser, NPC target, NPC.HitInfo hit, int damageDone) {
            Player owner = Main.player[laser.Projectile.owner];
            if (owner == null || !owner.active) return;
            int heal = System.Math.Min(damageDone / 100, 2);
            if (heal <= 0) return;
            owner.statLife = System.Math.Min(owner.statLife + heal, owner.statLifeMax2);
            CombatText.NewText(owner.getRect(), new Color(80, 230, 80), heal, dramatic: false, dot: true);
        }
    }
}
