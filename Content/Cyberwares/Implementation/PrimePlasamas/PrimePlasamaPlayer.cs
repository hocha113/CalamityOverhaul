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
