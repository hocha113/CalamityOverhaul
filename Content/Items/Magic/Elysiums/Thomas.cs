using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.Elysiums
{
    /// <summary>
    /// 第七门徒：多马（怀疑之触）
    /// 能力：给予玩家必定暴击状态
    /// 象征物：长枪
    /// </summary>
    internal class Thomas : BaseDisciple
    {
        public override int DiscipleIndex => 6;
        public override string DiscipleName => "多马";
        public override Color DiscipleColor => new(255, 165, 0); //怀疑橙
        public override int AbilityCooldownTime => 200;

        //多马是怀疑者，运动有犹豫感，时近时远
        protected override bool UsePulseMotion => true;
        protected override float MovementSmoothness => 0.06f;

        protected override void ExecuteAbility() {
            //给予玩家一个短暂的必定暴击状态
            Owner.GetCritChance(DamageClass.Generic) += 100;
            CombatText.NewText(Owner.Hitbox, Color.Orange, "怀疑验证");
            SetCooldown(180);
        }

        protected override void PassiveEffect() {
            //被动：小幅增加暴击伤害
            Owner.GetCritChance(DamageClass.Generic) += 3;
        }
    }
}
