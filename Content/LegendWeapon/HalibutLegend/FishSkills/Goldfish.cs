using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    internal class Goldfish : FishSkill
    {
        public override int TargetItemID => ItemID.Goldfish;
        public override bool PreShoot() => true;
    }
}
