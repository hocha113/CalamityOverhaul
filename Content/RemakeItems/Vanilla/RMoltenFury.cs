using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    internal class RMoltenFury : ItemOverride
    {
        public override int TargetID => ItemID.MoltenFury;
        public override bool IsVanilla => true;
        public override string TargetToolTipItemName => "Wap_MoltenFury_Text";
        public override void SetDefaults(Item item) => item.SetHeldProj<MoltenFuryHeldProj>();
    }
}
