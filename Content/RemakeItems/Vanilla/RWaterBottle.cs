using CalamityOverhaul.Content.RemakeItems.Core;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    internal class RWaterBottle : BaseRItem
    {
        public override int TargetID => ItemID.BottledWater;
        public override bool FormulaSubstitution => false;
        public override void OnConsumeItem(Item item, Player player) {
            //player.QuickSpawnItem(player.parent(), ItemID.Bottle);
        }

        public static void OnUse(Item item, Player player) {
            if (item.useStyle == ItemUseStyleID.DrinkLiquid
                && (item.buffType != 0 || item.type == ItemID.BottledWater)
                && item.consumable
                && item.UseSound == SoundID.Item3) {
                player.QuickSpawnItem(player.parent(), ItemID.Bottle);
            }
        }

        public static void OnRecipeBottle(Recipe recipe, Item item, List<Item> consumedItems, Item destinationStack) {
            //Main.NewText("你装了一瓶水");
            //Vector2 pos = Main.LocalPlayer.position / 16 - new Vector2(50, 50);
            //for (int i = 0; i < 100; i++) {
            //    for (int j = 0; j < 100; j++) {
            //        Vector2 pos2 = pos + new Vector2(i, j);
            //        Tile tile = CWRUtils.GetTile(pos2);
            //        if (tile.LiquidAmount > 0 && tile.LiquidType == 0) {
            //            if (tile.LiquidAmount >= 1)
            //                tile.LiquidAmount -= 1;
            //            else {
            //                tile.LiquidAmount = 0;
            //            }
            //            if (Main.zenithWorld) {
            //                tile.LiquidAmount = 1;
            //            }
            //        }
            //    }
            //}
        }
    }
}
