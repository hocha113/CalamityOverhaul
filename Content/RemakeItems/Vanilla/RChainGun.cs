using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    internal class RChainGun : BaseRItem
    {
        public override bool IsVanilla => true;
        public override int TargetID => ItemID.ChainGun;
        public override string TargetToolTipItemName => "Wap_ChainGun_Text";
        public override void SetDefaults(Item item) => item.SetCartridgeGun<ChainGunHeldProj>(185);
    }
}
