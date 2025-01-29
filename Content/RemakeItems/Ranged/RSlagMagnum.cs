using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RSlagMagnum : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<SlagMagnum>();
        public override int ProtogenesisID => ModContent.ItemType<SlagMagnumEcType>();
        public override string TargetToolTipItemName => "SlagMagnumEcType";
        public override void SetDefaults(Item item) {
            item.damage = 58;
            item.UseSound = CWRSound.Gun_Magnum_Shoot with { Volume = 0.35f };
            item.SetCartridgeGun<SlagMagnumHeldProj>(8);
        }
    }
}
