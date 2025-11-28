using CalamityOverhaul.Content.ADV.Scenarios.Helen.Sulfseas.OldDukes.OldDukeShops;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Helen.Sulfseas.OldDukes.Items
{
    internal class Oceanfragments : ModItem
    {
        public override string Texture => CWRConstant.Item_Other + "Oceanfragments";

        public override bool IsLoadingEnabled(Mod mod) {
            return false;//没做完，禁用
        }

        public override void SetDefaults() {
            Item.useTime = Item.useAnimation = 20;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.width = Item.height = 32;
            Item.value = 6000;
            Item.rare = ItemRarityID.Green;
        }

        public override bool? UseItem(Player player) {
            OldDukeShopUI.Instance.Active = !OldDukeShopUI.Instance.Active;
            return true;
        }
    }
}
