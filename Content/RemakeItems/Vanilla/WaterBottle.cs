using CalamityOverhaul.Content.RemakeItems.Core;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    internal class WaterBottle : BaseRItem
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
            Main.NewText("你装了一瓶水");
        }
    }
}
