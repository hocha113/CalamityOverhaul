using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.NPCs
{
    /// <summary>
    /// 监工的记录仪：信息饰品（33%）。佩戴获得危险感知（原版 dangerSense：
    /// 陷阱/机关高亮），与 L6 机关层天然联动。机器触发倒计时的读数显示
    /// 待 Wave-3 机器总线暴露读口后接入（不越权改 IMPL-B 的 DungeonworldMachines）。
    /// 贴图借原版金怀表（零新画像素）
    /// </summary>
    internal class OverseerLogger : OverseerModItem
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.GoldWatch;

        public override void SetDefaults() {
            Item.width = 24;
            Item.height = 24;
            Item.accessory = true;
            Item.rare = ItemRarityID.Green;
            Item.value = Item.sellPrice(0, 1, 50);
        }

        public override void UpdateAccessory(Player player, bool hideVisual) {
            player.dangerSense = true;
        }
    }
}
