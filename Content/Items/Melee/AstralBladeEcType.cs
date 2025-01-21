using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using Terraria;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 幻星刃
    /// </summary>
    internal class AstralBladeEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "AstralBlade";
        public override void SetDefaults() {
            Item.SetItemCopySD<AstralBlade>();
            Item.SetKnifeHeld<AstralBladeHeld>();
        }
        public override void ModifyWeaponCrit(Player player, ref float crit) => crit += 10;
    }
}
