using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Grip
{
    /// <summary>
    /// 吸收握把：命中目标时按伤害比例返还少量蓝量，构成自循环的法力补给
    /// 视觉用 <see cref="CombatText"/> 给玩家明确反馈，与 VampireGripModule 的回血形成补给二选一
    /// </summary>
    internal sealed class AbsorberGripModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Grip;
        //吸收暮蓝
        public override Color TintColor => new(80, 140, 220);

        public override void Apply(ref ShootContext ctx) {
            //抬高蓝量消耗：本身回蓝是补偿
            ctx.ManaCostMul += 0.4f;
            ctx.DamageMul += -0.1f;
        }

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            Refund(beam.Projectile.owner, damageDone, 20, 4, 1);
        }

        public override void OnLaserHitNPC(CyberPrismLaserProj laser, NPC target, NPC.HitInfo hit, int damageDone) {
            //激光每次命中给少一点
            Refund(laser.Projectile.owner, damageDone, 20, 2, 1);
        }

        private static void Refund(int owner, int damageDone, int divisor, int cap, int min) {
            Player p = Main.player[owner];
            if (p == null || !p.active) return;
            int refund = System.Math.Clamp(damageDone / divisor, min, cap);
            if (refund <= 0) return;
            int before = p.statMana;
            p.statMana = System.Math.Min(p.statMana + refund, p.statManaMax2);
            int actual = p.statMana - before;
            if (actual <= 0) return;
            //仅本机弹出文字提示
            if (owner == Main.myPlayer) {
                CombatText.NewText(p.getRect(), new Color(120, 180, 255), actual, dramatic: false, dot: false);
            }
        }
    }
}
