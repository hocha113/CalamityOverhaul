using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    /// <summary>
    /// 海啸
    /// </summary>
    internal class RTsunami : ItemOverride
    {
        public override int TargetID => ItemID.Tsunami;
        public override bool IsVanilla => true;
        public override string TargetToolTipItemName => "Wap_Tsunami_Text";
        public override void SetDefaults(Item item) => item.SetHeldProj<TsunamiHeldProj>();
    }
}
