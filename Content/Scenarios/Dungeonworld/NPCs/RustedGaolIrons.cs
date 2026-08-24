#if DEBUG
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.NPCs
{
    /// <summary>
    /// 锈蚀的镣铐：深牢怨灵的测试召唤物，仅调试构建存在。
    /// 暂借原版镣铐贴图，无进度门槛，任意世界可召
    /// （M0 独立测试用；接入 Dungeonworld 牢狱层刷新逻辑后另做正式召唤链路）
    /// </summary>
    internal class RustedGaolIrons : ModItem
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.Shackle;

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
            Item.consumable = false;
            Item.rare = ItemRarityID.Blue;
            Item.value = Item.sellPrice(0, 0, 50);
        }

        public override bool CanUseItem(Player player) {
            //场上无怨灵即可召，测试期不设进度与场景门槛
            return !NPC.AnyNPCs(ModContent.NPCType<DeepGaolWraith>());
        }

        public override bool? UseItem(Player player) {
            if (player.whoAmI == Main.myPlayer) {
                SoundEngine.PlaySound(SoundID.Unlock with { Pitch = -0.4f }, player.position);
                int type = ModContent.NPCType<DeepGaolWraith>();
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
    }
}
#endif
