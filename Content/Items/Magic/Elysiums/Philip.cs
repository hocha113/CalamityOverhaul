using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Items.Magic.Elysiums
{
    /// <summary>
    /// 第五门徒：腓力（圣光引导）
    /// 能力：为玩家指引攻击方向
    /// 象征物：面包（五饼二鱼）
    /// </summary>
    internal class Philip : BaseDisciple
    {
        public override int DiscipleIndex => 4;
        public override string DiscipleName => "腓力";
        public override Color DiscipleColor => new(255, 255, 200); //引导浅金
        public override int AbilityCooldownTime => 5; //高频率但弱效果

        protected override void ExecuteAbility() {
            //持续发出引导光线指向鼠标
            Vector2 toMouse = Main.MouseWorld - Owner.Center;
            if (toMouse.Length() > 100f) {
                Vector2 guidePos = Owner.Center + toMouse.SafeNormalize(Vector2.UnitX) * 80f;
                Dust d = Dust.NewDustPerfect(guidePos, DustID.GoldFlame, toMouse.SafeNormalize(Vector2.Zero) * 2f, 150, default, 0.8f);
                d.noGravity = true;
            }
            SetCooldown();
        }

        protected override void PassiveEffect() {
            //被动：增加移动速度
            Owner.moveSpeed += 0.1f;
        }
    }
}
