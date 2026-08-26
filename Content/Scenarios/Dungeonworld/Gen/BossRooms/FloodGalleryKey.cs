#if DEBUG
using CalamityOverhaul.Content.Scenarios.Dungeonworld.NPCs;
using Terraria;
using Terraria.Audio;
using Terraria.Chat;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.BossRooms
{
    /// <summary>
    /// 泄洪堂之钥：不溺者全链路测试物品，仅调试构建存在（镜像 GaolCellKey）。
    /// 任意世界一键在玩家所在处就地筑起泄洪堂并登记看守（踝水与蛰伏体随即由
    /// watcher 布置），可完整验收：房间观感 → 阀台站立触发 → 96f 仪式 →
    /// 换体入场 → 三相涨水 → 死亡泄洪 → 团灭复位，无需等生成管线。
    /// 玩家落位在左门内侧（阀台左邻，先看清房间再上台点火）。
    /// 联机：世界改写只在权威端执行，随后整块 SendTileSquare 过线
    /// </summary>
    internal class FloodGalleryKey : UndrownedModItem
    {
        /// <summary>玩家相对房内的落位列（左门内侧，阀台 11..13 之左）</summary>
        private const int PlayerColumn = 7;
        /// <summary>室内地板顶行</summary>
        private const int FloorRow = FloodGalleryRoom.FloorRel;

        public override string Texture => "Terraria/Images/Item_" + ItemID.GoldenKey;

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
            return !NPC.AnyNPCs(ModContent.NPCType<Undrowned>());
        }

        public override bool? UseItem(Player player) {
            if (player.whoAmI == Main.myPlayer) {
                SoundEngine.PlaySound(SoundID.Unlock with { Pitch = -0.3f }, player.position);
            }
            //世界改写只在权威端：单人本地执行，联机由服务器执行后整块同步
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                return true;
            }

            int feetX = (int)(player.Center.X / 16f);
            int feetY = (int)((player.position.Y + player.height) / 16f);
            int originX = feetX - PlayerColumn;
            int originY = feetY - FloorRow;
            originX = Utils.Clamp(originX, 40, Main.maxTilesX - 40 - FloodGalleryRoom.Width);
            originY = Utils.Clamp(originY, 40, Main.maxTilesY - 40 - FloodGalleryRoom.Height);

            FloodGalleryRoom.Place(originX, originY);
            //回播走看守的分块口径（纪律单一来源，含 1 格边缘帧余量）
            FloodGalleryWatcher.BroadcastRoom(new Point(originX, originY));

            //落成引导：明说下一步动作（阀台在左，站着别动），涨水规则交给房内告示牌
            LocalizedText placed = this.GetLocalization("Placed");
            if (VaultUtils.isServer) {
                ChatHelper.BroadcastChatMessage(placed.ToNetworkText(), new Color(88, 154, 148));
            }
            else {
                Main.NewText(placed.Value, 88, 154, 148);
            }
            return true;
        }
    }
}
#endif
