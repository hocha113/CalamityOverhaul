using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Tools
{
    internal class HandmadeDoll : ModItem
    {
        public override string Texture => CWRConstant.Item + "Tools/HandmadeDoll";
        public override void SetStaticDefaults() {
            Item.ResearchUnlockCount = 1;
            ItemID.Sets.ShimmerTransformToItem[Type] = ItemID.GuideVoodooDoll;
        }
        public override void SetDefaults() {
            Item.width = 20;
            Item.height = 20;
            Item.value = Item.buyPrice(0, 1, 50, 50);
            Item.rare = ItemRarityID.Purple;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.useAnimation = 20;
            Item.useTime = 20;
            Item.autoReuse = false;
            Item.consumable = false;//还是不要消耗的好
        }

        public override bool CanUseItem(Player player)
            => player.ZoneUnderworldHeight && !NPC.AnyNPCs(NPCID.WallofFlesh) && !NPC.AnyNPCs(NPCID.WallofFleshEye);

        public override bool? UseItem(Player player) {
            if (!VaultUtils.isClient) {
                foreach (var npc in Main.ActiveNPCs) {
                    if (npc.type == NPCID.Guide) {
                        npc.StrikeInstantKill();
                    }
                }
                NPC.SpawnWOF(player.position);
            }
            return true;
        }

        public override void AddRecipes() {
            _ = CreateRecipe()
                 .AddIngredient(ItemID.BlackThread, 2)
                 .AddIngredient(ItemID.Hay, 4)
                 .AddIngredient(ItemID.SoulofLight, 2)
                 .AddIngredient(ItemID.SoulofNight, 2)
                 .AddTile(TileID.Loom)
                 .Register();
        }
    }
}
