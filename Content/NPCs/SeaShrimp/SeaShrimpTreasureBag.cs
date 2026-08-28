using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.SeaShrimp
{
    /// <summary>渊晶海虾宝藏袋（专属掉落武器另开任务后填充，当前为材料与货币骨架）</summary>
    internal class SeaShrimpTreasureBag : SeaShrimpModItem
    {
        public override string Texture => $"Terraria/Images/Item_{ItemID.FishronBossBag}";

        public override void SetStaticDefaults() {
            ItemID.Sets.BossBag[Type] = true;
            Item.ResearchUnlockCount = 3;
        }

        public override void SetDefaults() {
            Item.width = 32;
            Item.height = 32;
            Item.maxStack = 9999;
            Item.consumable = true;
            Item.rare = ItemRarityID.Cyan;
            Item.expert = true;
        }

        public override bool CanRightClick() => true;

        public override void ModifyItemLoot(ItemLoot itemLoot) {
            itemLoot.Add(ItemDropRule.CoinsBasedOnNPCValue(ModContent.NPCType<SeaShrimpBoss>()));
            itemLoot.Add(ItemDropRule.Common(ItemID.CrystalShard, 1, 24, 40));
            itemLoot.Add(ItemDropRule.Common(ItemID.SoulofMight, 1, 12, 20));
            itemLoot.Add(ItemDropRule.Common(ItemID.BeetleHusk, 1, 6, 10));
        }
    }
}
