using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    /// <summary>
    /// 满弹霰弹枪
    /// </summary>
    internal class RBulletFilledShotgun : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<BulletFilledShotgun>();
        public override int ProtogenesisID => ModContent.ItemType<BulletFilledShotgunEcType>();
        public override string TargetToolTipItemName => "BulletFilledShotgunEcType";
        public override void SetDefaults(Item item) => item.SetCartridgeGun<BulletFilledShotgunHeldProj>(8);
    }
}
