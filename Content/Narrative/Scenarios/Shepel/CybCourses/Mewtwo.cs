using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Narrative.Scenarios.Shepel.CybCourses
{
    //超梦接入凭证，消耗后进入CybCourse子世界
    internal class Mewtwo : ModItem
    {
        public override string Texture => CWRConstant.Item_Tools + "Mewtwo";
        public override void SetDefaults() {
            Item.width = 28;
            Item.height = 28;
            Item.maxStack = 1;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.useTime = 30;
            Item.useAnimation = 30;
            Item.rare = ItemRarityID.Cyan;
            Item.value = 0;
        }

        public override bool? UseItem(Player player) {
            if (player.whoAmI == Main.myPlayer) {
                if (CybCourse.IsActive) {
                    CybCourseCompletePanel.Show();
                }
                else {
                    CybCourse.Enter();
                }
            }
            return true;
        }
    }
}

