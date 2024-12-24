using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.HalibutLegend
{
    internal class FishSkillLoader : ICWRLoader
    {
        public static List<FishSkill> fishSkills = [];
        void ICWRLoader.SetupData() {
            fishSkills = VaultUtils.GetSubclassInstances<FishSkill>();
            for (int i = 0; i < fishSkills.Count; i++) {
                fishSkills[i].Item = new Item(fishSkills[i].TargetItemID);
            }
        }
    }
}
