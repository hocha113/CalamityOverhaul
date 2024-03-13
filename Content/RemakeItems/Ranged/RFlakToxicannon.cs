using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria.ModLoader;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RFlakToxicannon : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<FlakToxicannon>();
        public override int ProtogenesisID => ModContent.ItemType<FlakToxicannonEcType>();
        public override string TargetToolTipItemName => "FlakToxicannonEcType";
        public override void SetDefaults(Item item) {
            item.SetCartridgeGun<FlakToxicannonHeldProj>(160);
            item.useAmmo = AmmoID.Bullet;
            item.CWR().Scope = true;
        }
    }
}
