using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using Terraria;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 绝对零度
    /// </summary>
    internal class AbsoluteZeroEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "AbsoluteZero";
        public override void SetDefaults() {
            Item.SetCalamitySD<AbsoluteZero>();
            Item.SetKnifeHeld<AbsoluteZeroHeld>();
        }
    }
}
