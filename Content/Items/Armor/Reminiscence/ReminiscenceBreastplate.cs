using CalamityMod.Items;
using Terraria.ID;
using Terraria;
using Terraria.ModLoader;
using CalamityOverhaul.Common;

namespace CalamityOverhaul.Content.Items.Armor.Reminiscence
{
    [AutoloadEquip(EquipType.Body)]
    internal class ReminiscenceBreastplate : ModItem
    {
        public override string Texture => CWRConstant.Item + "Armor/Reminiscence/ReminiscenceBreastplate";

        public override void SetDefaults() {
            Item.width = 18;
            Item.height = 18;
            Item.value = Terraria.Item.buyPrice(100, 6, 15, 0);
            Item.rare = ItemRarityID.Orange;
            Item.defense = 177;
        }

        public override void UpdateEquip(Player player) => player.GetCritChance<GenericDamageClass>() += 33;
    }
}
