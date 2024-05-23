using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RBarracudaGun : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<BarracudaGun>();
        public override int ProtogenesisID => ModContent.ItemType<BarracudaGunEcType>();
        public override string TargetToolTipItemName => "BarracudaGunEcType";
        public override void SetDefaults(Item item) => item.SetHeldProj<BarracudaGunHeldProj>();
    }
}
