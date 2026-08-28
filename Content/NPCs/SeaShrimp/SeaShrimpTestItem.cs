using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.SeaShrimp
{
    /// <summary>
    /// 开发用召唤物：任意位置可用，便于运动学验收。
    /// M2 换成正式召唤物（海洋限定 + 配方 + 贴图）后此物退役
    /// </summary>
    internal class SeaShrimpTestItem : SeaShrimpModItem
    {
        public override string Texture => $"Terraria/Images/Item_{ItemID.Shrimp}";

        public override void SetDefaults() {
            Item.width = 28;
            Item.height = 28;
            Item.maxStack = 1;
            Item.useAnimation = 40;
            Item.useTime = 40;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.consumable = false;
            Item.rare = ItemRarityID.Cyan;
        }

        public override bool CanUseItem(Player player)
            => !NPC.AnyNPCs(ModContent.NPCType<SeaShrimpBoss>());

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
    }
}
