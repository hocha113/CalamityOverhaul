using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    /// <summary>
    /// 满弹霰弹枪
    /// </summary>
    internal class RBulletFilledShotgun : CWRItemOverride
    {
        public override int TargetID => ModContent.ItemType<BulletFilledShotgun>();
        public override void SetDefaults(Item item) => item.SetCartridgeGun<BulletFilledShotgunHeldProj>(8);
    }
}
