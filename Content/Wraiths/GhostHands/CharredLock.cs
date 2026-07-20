using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Wraiths.GhostHands
{
    /// <summary>
    /// 焦黑的长命锁：焦黑枯手的可攥之物（规则卡 §1.2）。锁得住命，锁不住火。
    /// 掷于它面前或持锁递出 → 它弃你扑物 → 攥紧蜷缩 → 死机。
    /// 无使用行为：喂食判定在 <see cref="GhostHandActor"/> 侧读世界物品与持有物。
    /// 投放与补给（锁经济恒为 1）见 <see cref="GhostHandSite"/>
    /// </summary>
    internal sealed class CharredLock : ModItem
    {
        //占位贴图沿 WraithDebugTool 惯例走原版 override;专属贴图列待人工项
        public override string Texture => "Terraria/Images/Item_" + ItemID.ShadowKey;

        public override void SetDefaults() {
            Item.width = 26;
            Item.height = 26;
            Item.maxStack = 1;
            Item.rare = ItemRarityID.Blue;
            Item.value = 0;
        }
    }
}
