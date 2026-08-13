using CalamityOverhaul.Content.Scenarios.Dungeonworld.NPCs;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.BossRooms
{
    /// <summary>
    /// 深牢禁室之钥：Boss 房全流程测试物品。任意世界一键在玩家所在处就地筑起
    /// 深牢禁室并登记看守（蛰伏枯颅随即由 watcher 布置），可完整验收
    /// 房间观感 → 接近触发 → 激活演出 → 无缝变身 → 战斗/脱战复位，无需等 A 路接线。
    /// 玩家落位在左门内侧（距祭坛约 22 格，在触发半径外），先看得见蛰伏再走近点火。
    /// 联机：世界改写只在权威端执行，随后整块 SendTileSquare 过线
    /// </summary>
    internal class GaolCellKey : ModItem
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.GoldenKey;

        /// <summary>玩家相对房内的落位列（左门内侧）</summary>
        private const int PlayerColumn = 8;
        /// <summary>室内地板顶行（prefab 第 38 行）</summary>
        private const int FloorRow = 38;

        public override void SetDefaults() {
            Item.width = 22;
            Item.height = 22;
            Item.maxStack = Item.CommonMaxStack;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.useAnimation = 45;
            Item.useTime = 45;
            Item.consumable = false;
            Item.rare = ItemRarityID.Blue;
            Item.value = Item.sellPrice(0, 0, 50);
        }

        public override bool CanUseItem(Player player) {
            //战斗进行中不许在脚下重筑房间
            return !NPC.AnyNPCs(ModContent.NPCType<DeepGaolWraith>());
        }

        public override bool? UseItem(Player player) {
            if (player.whoAmI == Main.myPlayer) {
                SoundEngine.PlaySound(SoundID.Unlock with { Pitch = -0.2f }, player.position);
            }
            //世界改写只在权威端：单人本地执行，联机由服务器执行后整块同步
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                return true;
            }

            int feetX = (int)(player.Center.X / 16f);
            int feetY = (int)((player.position.Y + player.height) / 16f);
            int originX = feetX - PlayerColumn;
            int originY = feetY - FloorRow;
            originX = Utils.Clamp(originX, 40, Main.maxTilesX - 40 - GaolBossRoom.Width);
            originY = Utils.Clamp(originY, 40, Main.maxTilesY - 40 - GaolBossRoom.Height);

            GaolBossRoom.Place(originX, originY);
            if (Main.netMode == NetmodeID.Server) {
                NetMessage.SendTileSquare(-1, originX - 1, originY - 1,
                    GaolBossRoom.Width + 2, GaolBossRoom.Height + 2);
            }
            return true;
        }
    }
}
