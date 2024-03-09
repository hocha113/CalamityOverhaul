using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RBulletFilledShotgun : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<BulletFilledShotgun>();
        public override int ProtogenesisID => ModContent.ItemType<BulletFilledShotgunEcType>();
        public override string TargetToolTipItemName => "BulletFilledShotgunEcType";
        public override void SetDefaults(Item item) => item.SetCartridgeGun<BulletFilledShotgunHeldProj>(8);
    }
}
