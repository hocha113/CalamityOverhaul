using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.SeaShrimp
{
    /// <summary>晶化虾饵：海洋使用召唤渊晶海虾（贴图待自绘，暂借原版虾）</summary>
    internal class SeaShrimpSummonItem : SeaShrimpModItem
    {
        public override string Texture => $"Terraria/Images/Item_{ItemID.Shrimp}";

        public override void SetStaticDefaults() {
            ItemID.Sets.SortingPriorityBossSpawns[Type] = 13;
        }

        public override void SetDefaults() {
            Item.width = 28;
            Item.height = 28;
            Item.maxStack = 20;
            Item.useAnimation = 40;
            Item.useTime = 40;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.consumable = true;
            Item.rare = ItemRarityID.Lime;
            Item.value = Item.sellPrice(0, 0, 50);
        }

        public override bool CanUseItem(Player player)
            => player.ZoneBeach && !NPC.AnyNPCs(ModContent.NPCType<SeaShrimpBoss>());

        public override bool? UseItem(Player player) {
            if (player.whoAmI == Main.myPlayer) {
                int type = ModContent.NPCType<SeaShrimpBoss>();
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    NPC.SpawnOnPlayer(player.whoAmI, type);
                }
                else {
                    NetMessage.SendData(MessageID.SpawnBossUseLicenseStartEvent,
                        number: player.whoAmI, number2: type);
                }
            }
            return true;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ItemID.BeetleHusk, 4)
                .AddIngredient(ItemID.CrystalShard, 12)
                .AddIngredient(ItemID.SoulofLight, 5)
                .AddTile(TileID.MythrilAnvil)
                .Register();
        }
    }
}
