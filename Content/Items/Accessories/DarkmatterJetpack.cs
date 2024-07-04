using Terraria.ID;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories
{
    [AutoloadEquip(EquipType.Wings)]
    internal class DarkmatterJetpack : ModItem
    {
        public override string Texture => CWRConstant.Item + "DarkmatterJetpack";
        public override void SetDefaults() {
            Item.width = 32;
            Item.height = 24;
            Item.value = Item.sellPrice(0, 8, 0, 0);
            Item.rare = ItemRarityID.Purple;
            Item.accessory = true;
        }

        public override void UpdateAccessory(Player player, bool hideVisual) {
            player.wingTimeMax = 200;
        }

        public override void VerticalWingSpeeds(Player player, ref float ascentWhenFalling, ref float ascentWhenRising,
            ref float maxCanAscendMultiplier, ref float maxAscentMultiplier, ref float constantAscend) {
            ascentWhenFalling = 0.95f;
            ascentWhenRising = 0.15f;
            maxCanAscendMultiplier = 1f;
            maxAscentMultiplier = 4f;
            constantAscend = 0.17f;
        }

        public override void HorizontalWingSpeeds(Player player, ref float speed, ref float acceleration) {
            speed = 10f;
            acceleration *= 3f;
        }

        public override bool WingUpdate(Player player, bool inUse) {
            if (inUse) {
                player.wingFrameCounter++;
                if (player.wingFrameCounter >= 6) {
                    player.wingFrameCounter = 0;
                }
                player.wingFrame = 1 + player.wingFrameCounter / 2;
            }
            else {
                player.wingFrame = 0;
            }
            return true;
        }
    }
}
