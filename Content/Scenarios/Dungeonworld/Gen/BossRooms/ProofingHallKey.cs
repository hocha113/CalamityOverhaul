#if DEBUG
using CalamityOverhaul.Content.Scenarios.Dungeonworld.NPCs;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.BossRooms
{
    /// <summary>
    /// 验收堂之钥：铸造监工全链路测试物品，仅调试构建存在（镜像 FloodGalleryKey）。
    /// 任意世界一键在玩家所在处就地筑起验收堂并登记看守（蒙尘吊臂随即由 watcher 布置），
    /// 可完整验收：房间观感 → 点检台站立触发 → 80f 仪式 → 换体教学空锤 →
    /// 轨巡三招 → 30% 断轨钟摆 → 对冲活塞反杀 → 死亡演出 → 团灭复位。
    /// 玩家落位在左门内侧（点检台左邻）。联机：世界改写只在权威端执行后整块同步
    /// </summary>
    internal class ProofingHallKey : OverseerModItem
    {
        /// <summary>玩家相对房内的落位列（左门内侧，点检台 9..11 之左）</summary>
        private const int PlayerColumn = 6;
        private const int FloorRow = ProofingHallRoom.FloorRel;

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
            return !NPC.AnyNPCs(ModContent.NPCType<FoundryOverseer>());
        }

        public override bool? UseItem(Player player) {
            if (player.whoAmI == Main.myPlayer) {
                SoundEngine.PlaySound(SoundID.Unlock with { Pitch = -0.3f }, player.position);
            }
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                return true;
            }

            int feetX = (int)(player.Center.X / 16f);
            int feetY = (int)((player.position.Y + player.height) / 16f);
            int originX = feetX - PlayerColumn;
            int originY = feetY - FloorRow;
            originX = Utils.Clamp(originX, 40, Main.maxTilesX - 40 - ProofingHallRoom.Width);
            originY = Utils.Clamp(originY, 40, Main.maxTilesY - 40 - ProofingHallRoom.Height);

            ProofingHallRoom.Place(originX, originY);
            if (Main.netMode == NetmodeID.Server) {
                NetMessage.SendTileSquare(-1, originX - 1, originY - 1,
                    ProofingHallRoom.Width + 2, ProofingHallRoom.Height + 2);
            }
            return true;
        }
    }
}
#endif
