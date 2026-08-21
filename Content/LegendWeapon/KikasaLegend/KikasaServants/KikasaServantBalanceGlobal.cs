using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants
{
    /// <summary>
    /// 鬼奴多驻同场的出力平衡：驻影席数越多单只越省力（合计仍是净增）。
    /// 在命中端统一乘 <see cref="KikasaEffigyBoard.ServantDamageScale"/>，
    /// 覆盖鬼奴本体接触伤害与它们派生的一切子弹幕——
    /// 逐个改 18 条鬼奴实现的伤害公式既碎又漏，标记随生成源传染一次即可
    /// </summary>
    internal class KikasaServantBalanceGlobal : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        //鬼奴本体或它派生的子弹幕
        private bool servantSourced;

        public override void OnSpawn(Projectile projectile, IEntitySource source) {
            if (projectile.ModProjectile is IKikasaServant) {
                servantSourced = true;
                return;
            }
            //子弹幕沿父链传染标记（GetSource_FromAI 归于 EntitySource_Parent 族）
            if (source is EntitySource_Parent parentSource
                && parentSource.Entity is Projectile parent
                && parent.TryGetGlobalProjectile(out KikasaServantBalanceGlobal parentGlobal)
                && parentGlobal.servantSourced) {
                servantSourced = true;
            }
        }

        public override void ModifyHitNPC(Projectile projectile, NPC target,
            ref NPC.HitModifiers modifiers) {
            if (!servantSourced) {
                return;
            }
            Player owner = Main.player[projectile.owner];
            if (owner?.active != true) {
                return;
            }
            modifiers.FinalDamage *= KikasaEffigyBoard.ServantDamageScale(owner);
        }
    }
}
