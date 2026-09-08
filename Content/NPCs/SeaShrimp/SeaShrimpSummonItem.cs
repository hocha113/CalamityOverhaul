using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.SeaShrimp
{
    /// <summary>晶化虾饵：海洋使用召唤渊晶海虾</summary>
    internal class SeaShrimpSummonItem : SeaShrimpModItem
    {
        //自绘贴图（原稿 25×27，按项目 2x 约定近邻放大为 50×54）
        public override string Texture => CWRConstant.NPC + "SeaShrimp/SeaShrimpSummonItem";

        public override void SetStaticDefaults() {
            ItemID.Sets.SortingPriorityBossSpawns[Type] = 13;
        }

        public override void SetDefaults() {
            Item.width = 50;
            Item.height = 54;
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
