using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 硫火短剑
    /// </summary>
    internal class BrimstoneSwordEcType : EctypeItem
    {
        public override string Texture => "CalamityMod/Items/Weapons/Melee/BrimstoneSword";
        public override void SetDefaults() {
            Item.SetItemCopySD<BrimstoneSword>();
            Item.SetKnifeHeld<BrimstoneSwordHeld>();
        }

        public override bool MeleePrefix() => true;
    }
}
