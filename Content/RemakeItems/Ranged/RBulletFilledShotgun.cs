using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    /// <summary>
    /// 满弹霰弹枪
    /// </summary>
    internal class RBulletFilledShotgun : CWRItemOverride
    {
        public override void SetDefaults(Item item) => item.SetCartridgeGun<BulletFilledShotgunHeld>(8);
    }
}
