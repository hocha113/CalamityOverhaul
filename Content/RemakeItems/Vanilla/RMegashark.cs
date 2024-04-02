using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    internal class RMegashark : BaseRItem
    {
        public override int TargetID => ItemID.Megashark;
        public override bool IsVanilla => true;
        public override string TargetToolTipItemName => "Wap_Megashark_Text";
        public override void SetDefaults(Item item) => item.SetCartridgeGun<MegasharkHeldProj>(260);
    }
}
