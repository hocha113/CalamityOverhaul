using Terraria.ID;

namespace CalamityOverhaul.Content.HalibutLegend.FishSkills
{
    internal class Goldfish : FishSkill
    {
        public override int TargetItemID => ItemID.Goldfish;
        public override bool PreShoot() => true;
    }
}
