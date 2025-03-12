using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.MaterialFlow
{
    public abstract class BasePipelineItem : ModItem
    {
        public virtual int CreateTileID => -1;
        public override void SetStaticDefaults() => ItemID.Sets.ItemsThatAllowRepeatedRightClick[Type] = true;
        public override void SetDefaults() {
            Item.width = 32;
            Item.height = 32;
            Item.maxStack = 9999;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.useAnimation = 16;
            Item.useTime = 2;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.consumable = true;
            Item.value = Item.buyPrice(0, 0, 0, 75);
            Item.rare = ItemRarityID.Quest;
            Item.createTile = CreateTileID;
            Item.tileBoost = 12;
            Item.CWR().StorageUE = true;
            Item.CWR().ConsumeUseUE = 20;
        }

        public override void GrabRange(Player player, ref int grabRange) {
            if (player.altFunctionUse == 2) {
                grabRange *= 100;
            }
        }

        public override bool AltFunctionUse(Player player) => true;

        public override bool CanUseItem(Player player) {
            if (player.altFunctionUse == 2) {
                Point16 point = (Main.MouseWorld / 16).ToPoint16();
                Tile tile = Framing.GetTileSafely(point);
                if (!tile.HasTile || tile.TileType != CreateTileID) {
                    return false;
                }
            }
            return base.CanUseItem(player);
        }

        public override bool? UseItem(Player player) {
            if (player.altFunctionUse == 2 && player.whoAmI == Main.myPlayer) {
                Point16 point = (Main.MouseWorld / 16).ToPoint16();
                Tile tile = Framing.GetTileSafely(point);
                if (tile.HasTile && tile.TileType == CreateTileID) {
                    WorldGen.KillTile(point.X, point.Y);
                    if (!VaultUtils.isSinglePlayer) {
                        NetMessage.SendTileSquare(player.whoAmI, point.X, point.Y);
                    }
                }
                return false;
            }
            return base.UseItem(player);
        }
    }
}
