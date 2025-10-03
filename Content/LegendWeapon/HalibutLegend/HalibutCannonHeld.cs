using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.RangedModify.Core;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend
{
    internal class HalibutCannonHeld : BaseHeldRanged
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "HalibutCannon";
        public override int TargetID => ModContent.ItemType<HalibutCannon>();
        public int Level => HalibutCannonOverride.GetLevel(Item);
    }
}
