using CalamityOverhaul.Content.NPCs.BloomsandSerpents;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.FestersandSerpents
{
    /// <summary>
    /// 脓化花蕾：荒花沙蟒召唤物被暗影之魂沁染的变异体。沙漠地表使用，唤来脓蕾沙蟒。
    /// 条件：困难模式 + 沙漠 + 地表 + 场上无本体。贴图暂借刺球素材（与带刺花蕾同源）。
    /// </summary>
    internal class FssFesterBud : FssModItem
    {
        public override string Texture => CWRConstant.NPC + "BSS/CactusBall";

        public override void SetStaticDefaults() {
            ItemID.Sets.SortingPriorityBossSpawns[Type] = 9;
        }

        public override void SetDefaults() {
            Item.width = 26;
            Item.height = 26;
            Item.maxStack = Item.CommonMaxStack;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.useAnimation = 45;
            Item.useTime = 45;
            Item.consumable = true;
            Item.rare = ItemRarityID.LightRed;
            Item.value = Item.sellPrice(0, 1);
        }

        public override bool CanUseItem(Player player) {
            //肉山后 + 沙漠地表 + 场上无脓蕾沙蟒
            return Main.hardMode
                && player.ZoneDesert
                && player.position.Y < Main.worldSurface * 16f
                && !NPC.AnyNPCs(ModContent.NPCType<FssHead>());
        }

        public override bool? UseItem(Player player) {
            if (player.whoAmI == Main.myPlayer) {
                //召唤即变异体自己的湿哑吼声
                SoundEngine.PlaySound(CalamityOverhaul.Common.CWRSound.SendRoar with { Pitch = -0.45f }, player.position);
                int type = ModContent.NPCType<FssHead>();
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
            //荒花沙蟒树被门闸下线时不注册配方（原召唤物不存在）
            if (!BssGate.Enabled) {
                return;
            }
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<BssBloomBud>())
                .AddIngredient(ItemID.SoulofNight, 8)
                .AddTile(TileID.MythrilAnvil)
                .Register();
        }
    }
}
