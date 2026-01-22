using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Items.Magic.Elysiums
{
    /// <summary>
    /// 第八门徒：圣马修（财富祝福）
    /// 能力：增加敌怪掉落
    /// 象征物：钱袋（原为税吏）
    /// </summary>
    internal class Matthew : BaseDisciple
    {
        public override int DiscipleIndex => 7;
        public override string DiscipleName => "圣马修";
        public override Color DiscipleColor => new(255, 215, 100); //财富金
        public override int AbilityCooldownTime => 120;

        protected override void ExecuteAbility() {
            //金币视觉效果
            for (int i = 0; i < 8; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.GoldCoin, Main.rand.NextVector2Circular(3f, 3f), 100, default, 1.2f);
                d.noGravity = true;
            }
            SetCooldown();
        }

        protected override void PassiveEffect() {
            //被动：增加金币掉落(通过增加玩家幸运值模拟)
            Owner.luck += 0.1f;
        }
    }
}
