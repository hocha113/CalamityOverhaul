using CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs.Vanilla;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    internal class RBubbleGun : BaseRItem
    {
        public override int TargetID => ItemID.BubbleGun;
        public override bool IsVanilla => true;
        public override string TargetToolTipItemName => "Wap_BubbleGun_Text";
        public override void SetDefaults(Item item) {
            item.damage = 52;
            item.SetHeldProj<BubbleGunHeldProj>();
        }
    }
}
