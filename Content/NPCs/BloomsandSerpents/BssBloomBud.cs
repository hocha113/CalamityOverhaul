using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BloomsandSerpents
{
    /// <summary>
    /// 带刺花蕾：沙漠地表使用，唤来荒花沙蟒。
    /// 条件：击败克苏鲁之眼 + 沙漠 + 地表 + 场上无沙蟒。贴图借用刺球素材。
    /// </summary>
    internal class BssBloomBud : BssModItem
    {
        public override string Texture => CWRConstant.NPC + "BSS/CactusBall";

        public override void SetStaticDefaults() {
            ItemID.Sets.SortingPriorityBossSpawns[Type] = 3;
        }

        public override void SetDefaults() {
            Item.width = 26;
            Item.height = 26;
            Item.maxStack = Item.CommonMaxStack;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.useAnimation = 45;
            Item.useTime = 45;
            Item.consumable = true;
            Item.rare = ItemRarityID.Blue;
            Item.value = Item.sellPrice(0, 0, 50);
        }

        public override bool CanUseItem(Player player) {
            //克眼后 + 沙漠地表 + 场上无沙蟒
            return NPC.downedBoss1
                && player.ZoneDesert
                && player.position.Y < Main.worldSurface * 16f
                && !NPC.AnyNPCs(ModContent.NPCType<BssHead>());
        }

        public override bool? UseItem(Player player) {
            if (player.whoAmI == Main.myPlayer) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.2f }, player.position);
                int type = ModContent.NPCType<BssHead>();
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
                .AddIngredient(ItemID.Cactus, 12)
                .AddIngredient(ItemID.SandBlock, 20)
                .AddIngredient(ItemID.AntlionMandible, 2)
                .AddTile(TileID.WorkBenches)
                .Register();
        }
    }
}
