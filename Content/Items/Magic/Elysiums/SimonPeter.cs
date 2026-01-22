using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Items.Magic.Elysiums
{
    /// <summary>
    /// 第一门徒：西门彼得（磐石之盾）
    /// 能力：为玩家生成防护光环
    /// 象征物：天国之钥
    /// </summary>
    internal class SimonPeter : BaseDisciple
    {
        public override int DiscipleIndex => 0;
        public override string DiscipleName => "西门彼得";
        public override Color DiscipleColor => new(255, 215, 0); //金色
        public override int AbilityCooldownTime => 60;

        protected override void ExecuteAbility() {
            //每秒产生一次防护脉冲
            for (int i = 0; i < 12; i++) {
                float angle = MathHelper.TwoPi * i / 12f;
                Vector2 pos = Owner.Center + angle.ToRotationVector2() * 60f;
                Dust d = Dust.NewDustPerfect(pos, DustID.GoldFlame, angle.ToRotationVector2() * 2f, 100, default, 1.5f);
                d.noGravity = true;
            }
            SetCooldown();
        }

        protected override void PassiveEffect() {
            //被动：玩家在附近时获得少量防御加成
            if (Owner.TryGetModPlayer<ElysiumPlayer>(out _)) {
                Owner.statDefense += 5;
            }
        }
    }
}
