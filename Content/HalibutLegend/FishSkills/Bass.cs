using Terraria.ID;

namespace CalamityOverhaul.Content.HalibutLegend.FishSkills
{
    internal class Bass : FishSkill
    {
        public override int TargetItemID => ItemID.Bass;
        public override bool PreShoot() {
            return base.PreShoot();
        }
    }
}
