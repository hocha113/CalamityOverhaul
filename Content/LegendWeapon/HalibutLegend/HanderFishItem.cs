using CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend
{
    internal class HanderFishItem
    {
        public static FishDisplayUI TargetFish = new FishDisplayUI();

        public static void HanderItemText(ref string textContent) {
            Item item = ItemDisplayUI.Instance.Item;
            if (item.type > ItemID.None) {
                textContent = "我不知道这是什么东西，我需要鱼或者像鱼的东西，先生";
                foreach (var skill in FishSkillLoader.fishSkills) {
                    if (skill.Item.type == item.type) {
                        textContent = skill.Explain;
                        break;
                    }
                }
            }
        }

        public static void HanderPressed() {
            Item item = ItemDisplayUI.Instance.Item;
            foreach (var skill in FishSkillLoader.fishSkills) {
                if (skill.Item.type == item.type) {
                    if (FishList.Instance.HasFish(skill)) {

                    }
                    else {
                        FishList.Instance.AddFish(skill);
                    }
                    break;
                }
            }
            SoundEngine.PlaySound(SoundID.Grab);
        }
    }
}
