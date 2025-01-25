using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    internal class Bass : FishSkill
    {
        public override int TargetItemID => ItemID.Bass;
        public override bool PreShoot() {
            return base.PreShoot();
        }
    }
}
