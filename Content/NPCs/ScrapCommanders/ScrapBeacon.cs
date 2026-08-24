using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.ScrapCommanders
{
    /// <summary>
    /// 废钢信标：机械三王残料熔铸的召唤物，夜晚举起唤来废钢统帅。
    /// 暂借原版机械骷髅头贴图，专属贴图后续另做
    /// </summary>
    internal class ScrapBeacon : ScrapModItem
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.MechanicalSkull;

        public override void SetStaticDefaults() {
            ItemID.Sets.SortingPriorityBossSpawns[Type] = 12;
        }

        public override void SetDefaults() {
            Item.width = 28;
            Item.height = 28;
            Item.maxStack = Item.CommonMaxStack;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.useAnimation = 45;
            Item.useTime = 45;
            Item.consumable = true;
            Item.rare = ItemRarityID.Pink;
            Item.value = Item.sellPrice(0, 2);
        }

        public override bool CanUseItem(Player player) {
            //夜晚 + 机械三王全灭 + 场上无统帅
            return !Main.dayTime
                && NPC.downedMechBoss1 && NPC.downedMechBoss2 && NPC.downedMechBoss3
                && !NPC.AnyNPCs(ModContent.NPCType<ScrapCommander>());
        }

        public override bool? UseItem(Player player) {
            if (player.whoAmI == Main.myPlayer) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.4f }, player.position);
                int type = ModContent.NPCType<ScrapCommander>();
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
                .AddIngredient(ItemID.SoulofMight, 5)
                .AddIngredient(ItemID.SoulofSight, 5)
                .AddIngredient(ItemID.SoulofFright, 5)
                .AddRecipeGroup(RecipeGroupID.IronBar, 8)
                .AddTile(TileID.MythrilAnvil)
                .Register();
        }
    }
}
