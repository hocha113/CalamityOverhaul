using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    internal class RDartPistol : BaseRItem
    {
        public override int TargetID => ItemID.DartPistol;
        public override bool IsVanilla => true;
        public override string TargetToolTipItemName => "Wap_DartPistol_Text";
        public override void SetDefaults(Item item) {
            item.SetCartridgeGun<DartPistolHeldProj>(12);
            item.damage = 30;
        }
    }
}
