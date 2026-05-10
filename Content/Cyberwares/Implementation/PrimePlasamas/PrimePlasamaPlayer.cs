using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.PrimePlasamas
{
    /// <summary>
    /// 原型等离子皮下护甲的玩家组件
    /// <br/>原版玩家不存在直接的"击退抗性"字段，因此通过 <see cref="ModPlayer.ModifyHurt"/>
    /// 在受击瞬间缩放 <c>modifiers.Knockback</c> 来达成与击退抗性等价的效果
    /// <br/>装备状态自查，无需在装备/卸载事件中显式注册
    /// </summary>
    internal class PrimePlasamaPlayer : ModPlayer
    {
        public override void ModifyHurt(ref Player.HurtModifiers modifiers) {
            //装备本义体后击退量按 (1 - KnockbackResistanceBonus) 倍缩放
            //乘性叠加：本帧若有其他来源也修改 Knockback，互不干扰
            if (PrimePlasama.GetEquipped(Player) == null) {
                return;
            }
            float resist = MathHelper.Clamp(PrimePlasama.KnockbackResistanceBonus, 0f, 1f);
            modifiers.Knockback *= 1f - resist;
        }
    }
}
