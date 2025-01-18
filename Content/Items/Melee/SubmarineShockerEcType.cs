using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 渊海短剑
    /// </summary>
    internal class SubmarineShockerEcType : EctypeItem
    {
        public override string Texture => "CalamityMod/Items/Weapons/Melee/SubmarineShocker";
        internal static bool canShoot;
        public override void SetDefaults() {
            Item.SetItemCopySD<SubmarineShocker>();
            Item.shootSpeed = 8;
            Item.SetKnifeHeld<SubmarineShockerHeld>();
        }
        public override bool MeleePrefix() => true;
    }
}
