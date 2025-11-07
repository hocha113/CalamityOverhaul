using CalamityOverhaul.Common;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Draedons.PQCDs
{
    //便携式量子通讯装置
    internal class PQCD : ModItem
    {
        public override string Texture => CWRConstant.Item + "Tools/DraedonsRemote";

        public override void SetDefaults() {
            Item.width = 32;
            Item.height = 32;
            Item.useTime = Item.useAnimation = 30;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.UseSound = CWRSound.ButtonZero;
            Item.rare = ItemRarityID.Cyan;
            Item.value = Item.buyPrice(gold: 50);
        }

        public override bool? UseItem(Player player) {
            if (player.whoAmI == Main.myPlayer) {
                //打开嘉登商店界面
                DraedonShopUI.Instance.Active = !DraedonShopUI.Instance.Active;
            }
            return true;
        }
    }
}
