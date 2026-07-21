using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.PrimePlasamas
{
    /// <summary>
    /// 原型等离子 ModPlayer，ModifyHurt 缩放击退
    /// <br/>modifiers.Knockback *= (1 - KnockbackResistanceBonus)，乘性叠加
    /// </summary>
    internal class PrimePlasamaPlayer : ModPlayer
    {
        public override void ModifyHurt(ref Player.HurtModifiers modifiers) {
            //乘性缩放 Knockback，与其他来源不互踩
            if (PrimePlasama.GetEquipped(Player) == null) {
                return;
            }
            float resist = MathHelper.Clamp(PrimePlasama.KnockbackResistanceBonus, 0f, 1f);
            modifiers.Knockback *= 1f - resist;
        }
    }
}
